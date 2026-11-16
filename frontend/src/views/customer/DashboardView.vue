<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { RouterLink } from 'vue-router'
import AppCard from '@/components/ui/AppCard.vue'
import AppIcon from '@/components/ui/AppIcon.vue'
import MoneyAmount from '@/components/ui/MoneyAmount.vue'
import StatusViews from '@/components/ui/StatusViews.vue'
import AccountCard from '@/components/banking/AccountCard.vue'
import TransactionList from '@/components/banking/TransactionList.vue'
import TransactionDetailModal from '@/components/banking/TransactionDetailModal.vue'
import FeedbackPrompt from '@/components/banking/FeedbackPrompt.vue'
import { exchangeApi, meApi } from '@/api/bank'
import { useUiStore } from '@/stores/ui'
import { errorMessage } from '@/composables/useAsync'
import { gridColumnsClass, pluralSr } from '@/utils/format'
import type { Account, CustomerDashboard, ExchangeRate, Transaction } from '@/types/api'

const ui = useUiStore()
const dashboard = ref<CustomerDashboard | null>(null)
const accounts = ref<Account[]>([])
const rates = ref<ExchangeRate[]>([])
const isLoading = ref(true)
const error = ref<string | null>(null)
const selected = ref<Transaction | null>(null)

const primaryBalance = computed(() => dashboard.value?.balancesByCurrency.find((item) => item.currency === 'RSD') ?? dashboard.value?.balancesByCurrency[0])
const otherBalances = computed(() => dashboard.value?.balancesByCurrency.filter((item) => item !== primaryBalance.value) ?? [])
const totalInDinars = computed(() => {
  const balances = dashboard.value?.balancesByCurrency ?? []
  if (balances.length < 2 || !rates.value.length) return null
  return balances.reduce((sum, item) => {
    if (item.currency === 'RSD') return sum + item.amount
    const rate = rates.value.find((entry) => entry.currency === item.currency)
    return rate ? sum + item.amount * rate.middleRate : sum
  }, 0)
})
const ownIds = computed(() => accounts.value.map((account) => account.id))
const variants = ['forest', 'coral', 'cream'] as const

const quickActions = [
  { to: '/placanja/novo', label: 'Plati', icon: 'send' },
  { to: '/primaoci', label: 'Primaoci', icon: 'users' },
  { to: '/transakcije', label: 'Istorija', icon: 'list' },
  { to: '/pomoc', label: 'Cenovnik', icon: 'file' },
]

async function load(): Promise<void> {
  isLoading.value = true
  error.value = null
  try {
    ;[dashboard.value, accounts.value, rates.value] = await Promise.all([meApi.dashboard(), meApi.accounts(), exchangeApi.rates().catch(() => [])])
  } catch (caught) {
    error.value = errorMessage(caught)
  } finally {
    isLoading.value = false
  }
}

onMounted(load)
</script>

<template>
  <div class="page">
    <StatusViews v-if="isLoading" state="loading" :rows="5" />
    <StatusViews v-else-if="error" state="error" :message="error" @retry="load" />

    <template v-else-if="dashboard">
      <section class="hero">
        <div class="hero-main">
          <div class="row-between">
            <p class="hero-label">Ukupno raspoloživo</p>
            <button type="button" class="hide-toggle" :aria-pressed="ui.hideBalances" @click="ui.toggleBalances()">
              <AppIcon :name="ui.hideBalances ? 'eye-off' : 'eye'" :size="16" />
              {{ ui.hideBalances ? 'Prikaži' : 'Sakrij' }}
            </button>
          </div>
          <p class="hero-amount amount-fit">
            <MoneyAmount v-if="primaryBalance" :amount="primaryBalance.amount" :currency="primaryBalance.currency" />
            <span v-else>Nemate otvorenih računa</span>
          </p>
          <p v-if="otherBalances.length" class="hero-other">
            <span v-for="balance in otherBalances" :key="balance.currency" class="chip">
              <MoneyAmount :amount="balance.amount" :currency="balance.currency" />
            </span>
          </p>
          <p v-if="totalInDinars !== null" class="hero-equivalent">
            Ukupno u dinarskoj protivvrednosti: <MoneyAmount :amount="totalInDinars" currency="RSD" /> <span>(po srednjem kursu)</span>
          </p>
          <p class="hero-meta">{{ dashboard.activeAccounts }} {{ pluralSr(dashboard.activeAccounts, 'aktivan', 'aktivna', 'aktivnih') }} od {{ dashboard.totalAccounts }} {{ pluralSr(dashboard.totalAccounts, 'računa', 'računa', 'računa') }}</p>
        </div>
        <nav class="quick-actions" aria-label="Brze radnje">
          <RouterLink v-for="action in quickActions" :key="action.to" :to="action.to" class="quick-action">
            <span class="quick-icon"><AppIcon :name="action.icon" /></span>
            <span>{{ action.label }}</span>
          </RouterLink>
        </nav>
      </section>

      <div :class="gridColumnsClass(dashboard.monthCashFlow.length)">
        <AppCard v-for="flow in dashboard.monthCashFlow" :key="flow.currency" :title="`Ovaj mesec, ${flow.currency}`">
          <div class="flow">
            <div>
              <p class="subtle">Priliv</p>
              <MoneyAmount class="flow-value income" :amount="flow.income" :currency="flow.currency" />
            </div>
            <div>
              <p class="subtle">Odliv</p>
              <MoneyAmount class="flow-value" :amount="flow.expense" :currency="flow.currency" />
            </div>
          </div>
          <div class="flow-bar" aria-hidden="true">
            <span class="flow-in" :style="{ flexGrow: flow.income || 0.0001 }" />
            <span class="flow-out" :style="{ flexGrow: flow.expense || 0.0001 }" />
          </div>
        </AppCard>
        <AppCard v-if="dashboard.monthCashFlow.length === 0" title="Ovaj mesec">
          <p class="muted">Još nema prometa u ovom mesecu.</p>
        </AppCard>
      </div>

      <section class="stack">
        <div class="row-between">
          <h2 class="section-title">Moji računi</h2>
          <RouterLink to="/racuni" class="see-all">Svi računi</RouterLink>
        </div>
        <div v-if="accounts.length" :class="gridColumnsClass(accounts.length)">
          <AccountCard v-for="(account, index) in accounts.slice(0, 3)" :key="account.id" :account="account" :variant="variants[index % 3]" />
        </div>
        <StatusViews
          v-if="accounts.length === 0"
          state="empty"
          icon="wallet"
          title="Nemate otvoren račun"
          message="Račun otvara bankar nakon provere identiteta. Obratite se banci putem kontakta u odeljku Pomoć."
        />
      </section>

      <div class="dashboard-bottom">
        <AppCard title="Poslednje transakcije">
          <template #actions>
            <RouterLink to="/transakcije" class="see-all">Sve transakcije</RouterLink>
          </template>
          <TransactionList v-if="dashboard.recentTransactions.length" :transactions="dashboard.recentTransactions" :own-account-ids="ownIds" @select="selected = $event" />
          <StatusViews v-else state="empty" icon="list" title="Još nema transakcija" message="Ovde će se pojaviti vaše uplate i plaćanja." />
        </AppCard>
        <div class="stack">
          <AppCard tone="dark" title="Bezbednost naloga">
            <p class="security-copy">Proverite uređaje na kojima ste prijavljeni i odjavite one koje ne prepoznajete.</p>
            <RouterLink to="/bezbednost" class="security-link">Pregled sesija <AppIcon name="arrow-right" :size="16" /></RouterLink>
          </AppCard>
          <FeedbackPrompt context="pocetna" />
        </div>
      </div>
    </template>

    <TransactionDetailModal :transaction="selected" allow-complaint @close="selected = null" />
  </div>
</template>

<style scoped>
.hero {
  display: grid;
  grid-template-columns: minmax(0, 1.4fr) minmax(0, 1fr);
  gap: var(--space-6);
  padding: var(--space-8);
  border: 1px solid var(--color-emphasis-border);
  border-radius: var(--radius-xl);
  color: var(--color-emphasis-text);
  background:
    radial-gradient(circle at 100% 0%, rgb(200 241 105 / 0.18), transparent 40%),
    radial-gradient(circle at 0% 100%, rgb(240 90 60 / 0.22), transparent 45%),
    var(--color-emphasis-bg);
}

.hero-label {
  font-size: var(--font-size-sm);
  color: var(--color-emphasis-text-muted);
}

.hide-toggle {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 0.3rem 0.75rem;
  border: 1px solid var(--color-emphasis-chip-border);
  border-radius: var(--radius-pill);
  background: transparent;
  color: var(--color-emphasis-text);
  font-size: var(--font-size-xs);
  cursor: pointer;
}

.hero-main {
  min-width: 0;
}

.hero-amount {
  --amount-max: var(--font-size-display);
  --amount-min: 1.5rem;
  margin-top: var(--space-2);
  font-weight: 600;
  letter-spacing: -0.04em;
}

.hero-other {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-2);
  margin-top: var(--space-3);
}

.chip {
  padding: 0.3rem 0.8rem;
  border-radius: var(--radius-pill);
  background: var(--color-emphasis-chip);
  font-size: var(--font-size-sm);
}

.hero-equivalent {
  margin-top: var(--space-3);
  font-size: var(--font-size-sm);
  color: var(--color-emphasis-text-muted);
}

.hero-equivalent span {
  color: var(--color-emphasis-text-muted);
}

.hero-meta {
  margin-top: var(--space-4);
  font-size: var(--font-size-sm);
  color: var(--color-emphasis-accent);
}

.quick-actions {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: var(--space-3);
  align-self: center;
}

.quick-action {
  display: flex;
  align-items: center;
  gap: var(--space-3);
  padding: var(--space-4);
  border-radius: var(--radius-lg);
  background: var(--color-emphasis-chip);
  border: 1px solid var(--color-emphasis-chip-border);
  color: var(--color-emphasis-text);
  text-decoration: none;
  font-weight: 600;
  transition: background var(--transition-fast);
}

.quick-action:hover {
  background: var(--color-emphasis-chip-border);
}

.quick-icon {
  display: grid;
  place-items: center;
  width: 38px;
  height: 38px;
  border-radius: 50%;
  background: var(--color-lime-400);
  color: var(--color-forest-900);
}

.flow {
  display: flex;
  flex-wrap: wrap;
  justify-content: space-between;
  gap: var(--space-3) var(--space-4);
}

.flow-value {
  font-size: var(--font-size-lg);
  font-weight: 600;
}

.flow-value.income {
  color: var(--color-success);
}

.flow-bar {
  display: flex;
  gap: 4px;
  height: 8px;
  margin-top: var(--space-4);
}

.flow-bar span {
  border-radius: var(--radius-pill);
}

.flow-in {
  background: var(--color-chart-income);
}

.flow-out {
  background: var(--color-chart-expense);
}

.section-title {
  font-size: var(--font-size-xl);
}

.see-all {
  font-size: var(--font-size-sm);
  font-weight: 600;
  color: var(--color-link);
}

.dashboard-bottom {
  display: grid;
  grid-template-columns: minmax(0, 1.7fr) minmax(0, 1fr);
  gap: var(--space-5);
  align-items: start;
}

.security-copy {
  color: var(--color-emphasis-text-muted);
  font-size: var(--font-size-sm);
}

.security-link {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  margin-top: var(--space-4);
  color: var(--color-emphasis-accent);
  font-weight: 600;
  text-decoration: none;
}

@media (max-width: 1100px) {
  .hero,
  .dashboard-bottom {
    grid-template-columns: minmax(0, 1fr);
  }
}

@media (max-width: 720px) {
  .hero {
    padding: var(--space-6);
    border-radius: var(--radius-lg);
  }

  .quick-action {
    padding: var(--space-3);
    gap: var(--space-2);
  }
}

@media (max-width: 360px) {
  .quick-actions {
    grid-template-columns: minmax(0, 1fr);
  }
}
</style>
