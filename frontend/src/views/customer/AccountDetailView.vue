<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import AppCard from '@/components/ui/AppCard.vue'
import AppBadge from '@/components/ui/AppBadge.vue'
import AppButton from '@/components/ui/AppButton.vue'
import AppField from '@/components/ui/AppField.vue'
import AppModal from '@/components/ui/AppModal.vue'
import MoneyAmount from '@/components/ui/MoneyAmount.vue'
import StatusViews from '@/components/ui/StatusViews.vue'
import CashFlowChart from '@/components/banking/CashFlowChart.vue'
import TransactionList from '@/components/banking/TransactionList.vue'
import TransactionDetailModal from '@/components/banking/TransactionDetailModal.vue'
import { accountsApi } from '@/api/bank'
import { newIdempotencyKey } from '@/api/http'
import { useReauthStore } from '@/stores/reauth'
import { useUiStore } from '@/stores/ui'
import { errorMessage } from '@/composables/useAsync'
import { accountStatusLabels } from '@/utils/labels'
import { formatAccountNumber, formatDate } from '@/utils/format'
import type { Account, AccountStatistics, StatisticsPeriod, Transaction } from '@/types/api'

const route = useRoute()
const ui = useUiStore()
const reauth = useReauthStore()
const accountId = computed(() => Number(route.params.id))

const account = ref<Account | null>(null)
const statistics = ref<AccountStatistics | null>(null)
const period = ref<StatisticsPeriod>('Month')
const transactions = ref<Transaction[]>([])
const cursor = ref<string | null>(null)
const hasMore = ref(false)
const isLoading = ref(true)
const isLoadingMore = ref(false)
const error = ref<string | null>(null)
const selected = ref<Transaction | null>(null)
const withdrawal = reactive({ open: false, amount: '' as string | number, description: '', key: '', error: null as string | null, saving: false })

const periods: { value: StatisticsPeriod; label: string }[] = [
  { value: 'Week', label: '7 dana' },
  { value: 'Month', label: '30 dana' },
  { value: 'Year', label: '12 meseci' },
]

const chartBuckets = computed(() =>
  (statistics.value?.buckets ?? []).map((bucket) => {
    const date = new Date(bucket.periodStart)
    const label =
      period.value === 'Year'
        ? new Intl.DateTimeFormat('sr-Latn-RS', { month: 'short' }).format(date)
        : new Intl.DateTimeFormat('sr-Latn-RS', { day: '2-digit', month: '2-digit' }).format(date)
    return { label, income: bucket.income, expense: bucket.expense }
  }),
)

async function loadStatistics(): Promise<void> {
  statistics.value = await accountsApi.statistics(accountId.value, period.value)
}

async function load(): Promise<void> {
  isLoading.value = true
  error.value = null
  try {
    const [loadedAccount, page] = await Promise.all([accountsApi.get(accountId.value), accountsApi.transactions(accountId.value, {}), loadStatistics()])
    account.value = loadedAccount
    transactions.value = page.items
    cursor.value = page.nextCursor
    hasMore.value = page.hasMore
  } catch (caught) {
    error.value = errorMessage(caught)
  } finally {
    isLoading.value = false
  }
}

async function loadMore(): Promise<void> {
  isLoadingMore.value = true
  try {
    const page = await accountsApi.transactions(accountId.value, {}, cursor.value)
    transactions.value.push(...page.items)
    cursor.value = page.nextCursor
    hasMore.value = page.hasMore
  } catch (caught) {
    ui.notify(errorMessage(caught), 'danger')
  } finally {
    isLoadingMore.value = false
  }
}

async function copyNumber(): Promise<void> {
  if (!account.value) return
  try {
    await navigator.clipboard.writeText(account.value.accountNumber)
    ui.notify('Broj računa je kopiran.', 'info')
  } catch {
    ui.notify('Kopiranje nije uspelo. Označite broj ručno.', 'danger')
  }
}

function openWithdrawal(): void {
  Object.assign(withdrawal, { open: true, amount: '', description: '', key: newIdempotencyKey(), error: null })
}

async function submitWithdrawal(): Promise<void> {
  if (!account.value) return
  const amount = Number(String(withdrawal.amount).replace(',', '.'))
  if (!Number.isFinite(amount) || amount <= 0) {
    withdrawal.error = 'Unesite iznos veći od nule.'
    return
  }
  if (amount > account.value.balance) {
    withdrawal.error = 'Iznos je veći od raspoloživog stanja.'
    return
  }
  withdrawal.saving = true
  withdrawal.error = null
  try {
    const result = await reauth.runWithReauth(
      () => accountsApi.withdraw(account.value!.id, amount, withdrawal.description.trim() || null, withdrawal.key),
      'Za ovu isplatu potrebna je potvrda lozinkom.',
    )
    if (!result) return
    withdrawal.open = false
    ui.notify(`${result.message} Referenca ${result.transactionReference}.`)
    await load()
  } catch (caught) {
    withdrawal.error = errorMessage(caught)
  } finally {
    withdrawal.saving = false
  }
}

watch(period, () => loadStatistics().catch((caught) => ui.notify(errorMessage(caught), 'danger')))
watch(accountId, load)
onMounted(load)
</script>

<template>
  <div class="page">
    <StatusViews v-if="isLoading" state="loading" :rows="5" />
    <StatusViews v-else-if="error" state="error" :message="error" @retry="load" />

    <template v-else-if="account">
      <header class="page-header">
        <div>
          <RouterLink to="/racuni" class="back">← Svi računi</RouterLink>
          <h1 class="page-title">{{ account.kind === 'Stednja' ? 'Štedni račun' : 'Račun' }} {{ account.currency }}</h1>
          <p class="page-subtitle mono">{{ formatAccountNumber(account.accountNumber) }}</p>
        </div>
        <div class="row">
          <AppButton variant="secondary" icon="file" @click="copyNumber">Kopiraj broj</AppButton>
          <RouterLink :to="`/racuni/${account.id}/izvod`" class="statement-link">Izvod</RouterLink>
          <AppButton v-if="account.status === 'Aktivan' && account.kind === 'Tekuci'" variant="secondary" icon="arrow-up" @click="openWithdrawal">Isplata gotovine</AppButton>
          <RouterLink v-if="account.kind === 'Tekuci'" :to="{ path: '/placanja/novo', query: { racun: account.id } }" class="pay-link">Plati sa ovog računa</RouterLink>
          <RouterLink v-else to="/stednja" class="pay-link">Štednja</RouterLink>
        </div>
      </header>

      <div class="summary">
        <AppCard tone="dark">
          <p class="caption">Raspoloživo stanje</p>
          <MoneyAmount class="big" :amount="account.balance" :currency="account.currency" />
          <div class="row summary-meta">
            <AppBadge :tone="accountStatusLabels[account.status].tone">{{ accountStatusLabels[account.status].label }}</AppBadge>
            <span>Otvoren {{ formatDate(account.createdAt) }}</span>
          </div>
        </AppCard>
        <AppCard v-if="statistics">
          <p class="subtle">Priliv u periodu</p>
          <MoneyAmount class="metric income" :amount="statistics.totalIncome" :currency="account.currency" />
          <p class="subtle metric-gap">Odliv u periodu</p>
          <MoneyAmount class="metric" :amount="statistics.totalExpense" :currency="account.currency" />
        </AppCard>
      </div>

      <AppCard title="Promet">
        <template #actions>
          <div class="segmented" role="group" aria-label="Period">
            <button v-for="option in periods" :key="option.value" type="button" :aria-pressed="period === option.value" :class="{ active: period === option.value }" @click="period = option.value">
              {{ option.label }}
            </button>
          </div>
        </template>
        <CashFlowChart :buckets="chartBuckets" :currency="account.currency" />
      </AppCard>

      <AppCard title="Transakcije na računu">
        <TransactionList v-if="transactions.length" :transactions="transactions" :own-account-ids="[account.id]" @select="selected = $event" />
        <StatusViews v-else state="empty" icon="list" title="Nema transakcija" />
        <div v-if="hasMore" class="more">
          <AppButton variant="secondary" :loading="isLoadingMore" @click="loadMore">Učitaj još</AppButton>
        </div>
      </AppCard>
    </template>

    <TransactionDetailModal :transaction="selected" allow-complaint @close="selected = null" />

    <AppModal :open="withdrawal.open" title="Isplata gotovine" width="460px" @close="withdrawal.open = false">
      <form id="withdrawal-form" class="stack" @submit.prevent="submitWithdrawal">
        <p v-if="account" class="muted">
          Raspoloživo: <MoneyAmount :amount="account.balance" :currency="account.currency" :sensitive="false" />
        </p>
        <AppField v-model="withdrawal.amount" :label="`Iznos (${account?.currency ?? 'RSD'})`" inputmode="decimal" required />
        <AppField v-model="withdrawal.description" label="Opis (nije obavezno)" :maxlength="200" placeholder="npr. Bankomat" />
        <p class="subtle">Simulacija podizanja gotovine. Isplata se odmah knjiži i ulazi u dnevni limit.</p>
        <p v-if="withdrawal.error" class="form-error" role="alert">{{ withdrawal.error }}</p>
      </form>
      <template #footer>
        <AppButton variant="ghost" @click="withdrawal.open = false">Odustani</AppButton>
        <AppButton type="submit" form="withdrawal-form" :loading="withdrawal.saving">Potvrdi isplatu</AppButton>
      </template>
    </AppModal>
  </div>
</template>

<style scoped>
.back {
  font-size: var(--font-size-sm);
  color: var(--color-text-muted);
  text-decoration: none;
}

.statement-link {
  padding: 0.65rem 1.2rem;
  border: 1px solid var(--color-border-strong);
  border-radius: var(--radius-pill);
  background: var(--color-surface);
  font-weight: 600;
  text-decoration: none;
}

.pay-link {
  padding: 0.65rem 1.2rem;
  border-radius: var(--radius-pill);
  background: var(--color-primary);
  color: #fff;
  font-weight: 600;
  text-decoration: none;
}

.summary {
  display: grid;
  grid-template-columns: minmax(0, 1.5fr) minmax(0, 1fr);
  gap: var(--space-5);
}

.caption {
  color: rgb(255 255 255 / 0.7);
  font-size: var(--font-size-sm);
}

.big {
  display: block;
  margin: var(--space-2) 0 var(--space-4);
  font-size: var(--font-size-display);
  font-weight: 600;
  letter-spacing: -0.04em;
}

.summary-meta {
  font-size: var(--font-size-sm);
  color: rgb(255 255 255 / 0.7);
}

.metric {
  display: block;
  font-size: var(--font-size-xl);
  font-weight: 600;
}

.metric.income {
  color: var(--color-success);
}

.metric-gap {
  margin-top: var(--space-4);
}

.segmented {
  display: inline-flex;
  padding: 3px;
  border-radius: var(--radius-pill);
  background: var(--color-surface-muted);
}

.segmented button {
  padding: 0.35rem 0.85rem;
  border: none;
  border-radius: var(--radius-pill);
  background: transparent;
  font-size: var(--font-size-sm);
  font-weight: 600;
  color: var(--color-text-muted);
  cursor: pointer;
}

.segmented button.active {
  background: var(--color-surface);
  color: var(--color-text);
  box-shadow: var(--shadow-sm);
}

.form-error {
  color: var(--color-danger);
  font-size: var(--font-size-sm);
}

.more {
  display: flex;
  justify-content: center;
  padding-top: var(--space-4);
}

@media (max-width: 960px) {
  .summary {
    grid-template-columns: minmax(0, 1fr);
  }
}
</style>
