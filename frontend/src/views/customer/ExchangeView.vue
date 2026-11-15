<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import AppCard from '@/components/ui/AppCard.vue'
import AppField from '@/components/ui/AppField.vue'
import AppButton from '@/components/ui/AppButton.vue'
import AppIcon from '@/components/ui/AppIcon.vue'
import AppPagination from '@/components/ui/AppPagination.vue'
import MoneyAmount from '@/components/ui/MoneyAmount.vue'
import StatusViews from '@/components/ui/StatusViews.vue'
import ExchangeRatesTable from '@/components/banking/ExchangeRatesTable.vue'
import { exchangeApi, meApi } from '@/api/bank'
import { ApiError, newIdempotencyKey } from '@/api/http'
import { useUiStore } from '@/stores/ui'
import { errorMessage } from '@/composables/useAsync'
import { formatAccountNumber, formatDateTime, formatNumber } from '@/utils/format'
import type { Account, CurrencyExchange, ExchangeQuote, ExchangeRate, PagedResponse } from '@/types/api'

const ui = useUiStore()
const accounts = ref<Account[]>([])
const rates = ref<ExchangeRate[]>([])
const history = ref<PagedResponse<CurrencyExchange> | null>(null)
const historyPage = ref(1)
const isLoading = ref(true)
const loadError = ref<string | null>(null)

const fromAccountId = ref<number | null>(null)
const toAccountId = ref<number | null>(null)
const amount = ref<string | number>('')
const amountIsTarget = ref(false)
const quote = ref<ExchangeQuote | null>(null)
const quoteError = ref<string | null>(null)
const isQuoting = ref(false)
const isSubmitting = ref(false)
const idempotencyKey = ref(newIdempotencyKey())
const lastResult = ref<CurrencyExchange | null>(null)
let quoteTimer: number | undefined

const activeAccounts = computed(() => accounts.value.filter((account) => account.status === 'Aktivan'))
const fromAccount = computed(() => accounts.value.find((account) => account.id === fromAccountId.value) ?? null)
const toAccount = computed(() => accounts.value.find((account) => account.id === toAccountId.value) ?? null)
const option = (account: Account) => ({ value: account.id, label: `${account.currency} · ${formatAccountNumber(account.accountNumber)}` })
const fromOptions = computed(() => activeAccounts.value.map(option))
const toOptions = computed(() =>
  activeAccounts.value
    .filter((account) => fromAccount.value && account.id !== fromAccount.value.id && account.currency !== fromAccount.value.currency)
    .filter((account) => fromAccount.value!.currency === 'RSD' || account.currency === 'RSD')
    .map(option),
)
const amountCurrency = computed(() => (amountIsTarget.value ? toAccount.value?.currency : fromAccount.value?.currency) ?? '')
const hasForeignAccount = computed(() => activeAccounts.value.some((account) => account.currency !== 'RSD'))
const parsedAmount = computed(() => Number(String(amount.value).replace(',', '.')))

function swap(): void {
  const previousFrom = fromAccountId.value
  fromAccountId.value = toAccountId.value
  toAccountId.value = previousFrom
}

function scheduleQuote(): void {
  window.clearTimeout(quoteTimer)
  quote.value = null
  quoteError.value = null
  if (!fromAccountId.value || !toAccountId.value || !(parsedAmount.value > 0)) return
  quoteTimer = window.setTimeout(requestQuote, 350)
}

async function requestQuote(): Promise<void> {
  if (!fromAccountId.value || !toAccountId.value) return
  isQuoting.value = true
  try {
    quote.value = await exchangeApi.quote({
      fromAccountId: fromAccountId.value,
      toAccountId: toAccountId.value,
      amount: parsedAmount.value,
      amountIsTarget: amountIsTarget.value,
      expectedRateId: null,
    })
    idempotencyKey.value = newIdempotencyKey()
  } catch (caught) {
    quoteError.value = errorMessage(caught)
  } finally {
    isQuoting.value = false
  }
}

async function confirm(): Promise<void> {
  if (!quote.value || !fromAccountId.value || !toAccountId.value) return
  isSubmitting.value = true
  try {
    lastResult.value = await exchangeApi.execute(
      {
        fromAccountId: fromAccountId.value,
        toAccountId: toAccountId.value,
        amount: parsedAmount.value,
        amountIsTarget: amountIsTarget.value,
        expectedRateId: quote.value.rateId,
      },
      idempotencyKey.value,
    )
    ui.notify(`Zamenjeno ${formatNumber(lastResult.value.fromAmount, 2)} ${lastResult.value.fromCurrency} za ${formatNumber(lastResult.value.toAmount, 2)} ${lastResult.value.toCurrency}.`)
    amount.value = ''
    quote.value = null
    await Promise.all([refreshAccounts(), loadHistory()])
  } catch (caught) {
    if (caught instanceof ApiError && caught.status === 409) {
      ui.notify('Kurs je promenjen. Prikazan je novi obračun, proverite ga i potvrdite ponovo.', 'info')
      rates.value = await exchangeApi.rates()
      await requestQuote()
    } else {
      quoteError.value = errorMessage(caught)
    }
  } finally {
    isSubmitting.value = false
  }
}

async function refreshAccounts(): Promise<void> {
  accounts.value = await meApi.accounts()
}

async function loadHistory(): Promise<void> {
  history.value = await exchangeApi.mine(historyPage.value)
}

async function load(): Promise<void> {
  isLoading.value = true
  loadError.value = null
  try {
    ;[accounts.value, rates.value] = await Promise.all([meApi.accounts(), exchangeApi.rates()])
    await loadHistory()
    fromAccountId.value = activeAccounts.value.find((account) => account.currency === 'RSD')?.id ?? activeAccounts.value[0]?.id ?? null
    toAccountId.value = (toOptions.value[0]?.value as number | undefined) ?? null
  } catch (caught) {
    loadError.value = errorMessage(caught)
  } finally {
    isLoading.value = false
  }
}

watch(fromAccountId, () => {
  if (!toOptions.value.some((item) => item.value === toAccountId.value)) toAccountId.value = (toOptions.value[0]?.value as number | undefined) ?? null
})
watch([fromAccountId, toAccountId, amount, amountIsTarget], scheduleQuote)
watch(historyPage, loadHistory)
onMounted(load)
</script>

<template>
  <div class="page">
    <header class="page-header">
      <div>
        <h1 class="page-title">Menjačnica</h1>
        <p class="page-subtitle">Konverzija između vaših dinarskih i deviznih računa po kursnoj listi banke, odmah i bez dodatne naknade.</p>
      </div>
    </header>

    <StatusViews v-if="isLoading" state="loading" :rows="5" />
    <StatusViews v-else-if="loadError" state="error" :message="loadError" @retry="load" />
    <StatusViews
      v-else-if="!hasForeignAccount"
      state="empty"
      icon="exchange"
      title="Nemate devizni račun"
      message="Za konverziju su potrebni dinarski i devizni račun (EUR ili USD). Devizni račun otvara bankar."
    />

    <template v-else>
      <div class="layout">
        <AppCard title="Konverzija">
          <div class="pair">
            <AppField v-model="fromAccountId" label="Sa računa" :options="fromOptions" />
            <button type="button" class="swap" aria-label="Zameni smer" title="Zameni smer" @click="swap"><AppIcon name="exchange" /></button>
            <AppField v-model="toAccountId" label="Na račun" :options="toOptions" />
          </div>
          <p v-if="fromAccount" class="subtle balance">Raspoloživo: <MoneyAmount :amount="fromAccount.balance" :currency="fromAccount.currency" /></p>

          <div class="segmented" role="group" aria-label="Šta unosite">
            <button type="button" :class="{ active: !amountIsTarget }" :aria-pressed="!amountIsTarget" @click="amountIsTarget = false">Iznos koji menjam</button>
            <button type="button" :class="{ active: amountIsTarget }" :aria-pressed="amountIsTarget" @click="amountIsTarget = true">Iznos koji želim da dobijem</button>
          </div>
          <AppField v-model="amount" class="amount" :label="`Iznos (${amountCurrency})`" inputmode="decimal" placeholder="0,00" />

          <div v-if="isQuoting" class="subtle">Računam...</div>
          <div v-else-if="quoteError" class="error-banner" role="alert"><AppIcon name="alert" :size="18" /> {{ quoteError }}</div>
          <div v-else-if="quote" class="quote">
            <div class="quote-row big">
              <span>Dajete</span>
              <MoneyAmount :amount="quote.fromAmount" :currency="quote.fromCurrency" :sensitive="false" />
            </div>
            <div class="quote-row big income">
              <span>Dobijate</span>
              <MoneyAmount :amount="quote.toAmount" :currency="quote.toCurrency" :sensitive="false" />
            </div>
            <div class="quote-row">
              <span>{{ quote.rateType === 'Prodajni' ? 'Prodajni' : 'Kupovni' }} kurs</span>
              <span class="mono">{{ formatNumber(quote.appliedRate, 4) }}</span>
            </div>
            <div class="quote-row subtle">
              <span>Srednji kurs (za poređenje)</span>
              <span class="mono">{{ formatNumber(quote.middleRate, 4) }}</span>
            </div>
            <div class="quote-row subtle">
              <span>Razlika u odnosu na srednji kurs</span>
              <MoneyAmount :amount="quote.spreadRsd" currency="RSD" :sensitive="false" />
            </div>
            <div class="quote-row subtle">
              <span>Stanje posle</span>
              <MoneyAmount :amount="quote.fromBalanceAfter" :currency="quote.fromCurrency" />
            </div>
            <AppButton block size="lg" icon="check" :loading="isSubmitting" @click="confirm">Potvrdi konverziju</AppButton>
            <p class="subtle tiny">Obračun važi po kursnoj listi od {{ formatDateTime(quote.rateValidFrom) }}. Ako se kurs promeni pre potvrde, prikazaćemo novi obračun.</p>
          </div>
        </AppCard>

        <AppCard title="Kursna lista">
          <ExchangeRatesTable :rates="rates" />
        </AppCard>
      </div>

      <AppCard title="Istorija konverzija" :padded="false">
        <div class="card-body">
          <StatusViews v-if="!history?.items.length" state="empty" icon="exchange" title="Još nema konverzija" />
          <div v-else class="table-scroll">
            <table class="data-table">
              <thead>
                <tr>
                  <th scope="col">Vreme</th>
                  <th scope="col">Referenca</th>
                  <th scope="col" class="numeric">Dato</th>
                  <th scope="col" class="numeric">Dobijeno</th>
                  <th scope="col" class="numeric">Kurs</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="item in history.items" :key="item.id">
                  <td class="subtle">{{ formatDateTime(item.createdAt) }}</td>
                  <td class="mono">{{ item.referenceNumber }}</td>
                  <td class="numeric"><MoneyAmount :amount="item.fromAmount" :currency="item.fromCurrency" /></td>
                  <td class="numeric"><MoneyAmount :amount="item.toAmount" :currency="item.toCurrency" /></td>
                  <td class="numeric mono">{{ formatNumber(item.appliedRate, 4) }}</td>
                </tr>
              </tbody>
            </table>
          </div>
          <AppPagination v-if="history" :page="history.page" :total-pages="history.totalPages" :total-count="history.totalCount" @change="historyPage = $event" />
        </div>
      </AppCard>
    </template>
  </div>
</template>

<style scoped>
.layout {
  display: grid;
  grid-template-columns: minmax(0, 1.4fr) minmax(0, 1fr);
  gap: var(--space-5);
  align-items: start;
}

.pair {
  display: grid;
  grid-template-columns: 1fr auto 1fr;
  gap: var(--space-3);
  align-items: end;
}

.swap {
  display: grid;
  place-items: center;
  width: 44px;
  height: 44px;
  margin-bottom: 2px;
  border: 1px solid var(--color-border-strong);
  border-radius: 50%;
  background: var(--color-surface);
  cursor: pointer;
  transform: rotate(90deg);
}

.balance {
  margin: var(--space-2) 0 var(--space-4);
}

.segmented {
  display: inline-flex;
  padding: 3px;
  border-radius: var(--radius-pill);
  background: var(--color-surface-muted);
}

.segmented button {
  padding: 0.4rem 0.9rem;
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

.amount {
  margin: var(--space-4) 0;
}

.quote {
  display: flex;
  flex-direction: column;
  gap: var(--space-2);
  padding: var(--space-4);
  border-radius: var(--radius-lg);
  background: var(--color-cream);
}

.quote-row {
  display: flex;
  justify-content: space-between;
  gap: var(--space-3);
  font-size: var(--font-size-sm);
}

.quote-row.big {
  font-size: var(--font-size-lg);
  font-weight: 600;
}

.quote-row.income :deep(.money) {
  color: var(--color-success);
}

.tiny {
  font-size: var(--font-size-xs);
}

.error-banner {
  display: flex;
  gap: var(--space-2);
  padding: var(--space-3) var(--space-4);
  border-radius: var(--radius-md);
  background: var(--color-danger-soft);
  color: var(--color-danger);
  font-size: var(--font-size-sm);
}

.numeric {
  text-align: right;
}

@media (max-width: 1000px) {
  .layout {
    grid-template-columns: minmax(0, 1fr);
  }

  .pair {
    grid-template-columns: minmax(0, 1fr);
  }

  .swap {
    justify-self: center;
  }
}
</style>
