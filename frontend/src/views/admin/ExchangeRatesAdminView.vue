<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import AppCard from '@/components/ui/AppCard.vue'
import AppButton from '@/components/ui/AppButton.vue'
import AppField from '@/components/ui/AppField.vue'
import StatusViews from '@/components/ui/StatusViews.vue'
import ExchangeRatesTable from '@/components/banking/ExchangeRatesTable.vue'
import { exchangeApi } from '@/api/bank'
import { useUiStore } from '@/stores/ui'
import { errorMessage } from '@/composables/useAsync'
import { formatDateTime, formatNumber } from '@/utils/format'
import type { ExchangeRate } from '@/types/api'

const ui = useUiStore()
const rates = ref<ExchangeRate[]>([])
const history = ref<ExchangeRate[]>([])
const historyCurrency = ref('EUR')
const isLoading = ref(true)
const error = ref<string | null>(null)
const isSaving = ref(false)
const formError = ref<string | null>(null)
const form = reactive<Record<string, { buy: string | number; middle: string | number; sell: string | number }>>({})

async function load(): Promise<void> {
  isLoading.value = true
  error.value = null
  try {
    rates.value = await exchangeApi.rates()
    for (const rate of rates.value) form[rate.currency] = { buy: rate.buyRate, middle: rate.middleRate, sell: rate.sellRate }
    history.value = await exchangeApi.history(historyCurrency.value, 90)
  } catch (caught) {
    error.value = errorMessage(caught)
  } finally {
    isLoading.value = false
  }
}

function toNumber(value: string | number): number {
  return Number(String(value).replace(',', '.'))
}

async function publish(): Promise<void> {
  formError.value = null
  const payload = Object.entries(form).map(([currency, values]) => ({
    currency,
    buyRate: toNumber(values.buy),
    middleRate: toNumber(values.middle),
    sellRate: toNumber(values.sell),
  }))
  const invalid = payload.find((rate) => !(rate.buyRate > 0 && rate.buyRate < rate.middleRate && rate.middleRate < rate.sellRate))
  if (invalid) {
    formError.value = `${invalid.currency}: mora važiti kupovni < srednji < prodajni.`
    return
  }
  isSaving.value = true
  try {
    await exchangeApi.publish(payload)
    ui.notify('Nova kursna lista je objavljena i odmah važi.')
    await load()
  } catch (caught) {
    formError.value = errorMessage(caught)
  } finally {
    isSaving.value = false
  }
}

async function changeHistory(currency: string): Promise<void> {
  historyCurrency.value = currency
  history.value = await exchangeApi.history(currency, 90)
}

onMounted(load)
</script>

<template>
  <div class="page">
    <header class="page-header">
      <div>
        <h1 class="page-title">Kursna lista</h1>
        <p class="page-subtitle">Objava kursne liste banke. Nova lista važi odmah za sve konverzije, a prethodne ostaju u istoriji.</p>
      </div>
    </header>

    <StatusViews v-if="isLoading" state="loading" :rows="4" />
    <StatusViews v-else-if="error" state="error" :message="error" @retry="load" />

    <template v-else>
      <div class="grid-2 align-start">
        <AppCard title="Važeća lista">
          <ExchangeRatesTable :rates="rates" />
        </AppCard>
        <AppCard title="Nova kursna lista" subtitle="Kupovni < srednji < prodajni; razlika do 10% srednjeg kursa; promena srednjeg kursa najviše 20%.">
          <form class="stack" @submit.prevent="publish">
            <div v-for="(values, currency) in form" :key="currency" class="rate-row">
              <strong class="currency">{{ currency }}</strong>
              <AppField v-model="values.buy" label="Kupovni" inputmode="decimal" />
              <AppField v-model="values.middle" label="Srednji" inputmode="decimal" />
              <AppField v-model="values.sell" label="Prodajni" inputmode="decimal" />
            </div>
            <p v-if="formError" class="form-error" role="alert">{{ formError }}</p>
            <AppButton type="submit" :loading="isSaving">Objavi kursnu listu</AppButton>
          </form>
        </AppCard>
      </div>

      <AppCard title="Istorija kurseva (90 dana)">
        <template #actions>
          <div class="segmented" role="group" aria-label="Valuta">
            <button v-for="rate in rates" :key="rate.currency" type="button" :class="{ active: historyCurrency === rate.currency }" @click="changeHistory(rate.currency)">
              {{ rate.currency }}
            </button>
          </div>
        </template>
        <div class="table-scroll">
          <table class="data-table">
            <thead>
              <tr>
                <th scope="col">Važi od</th>
                <th scope="col" class="numeric">Kupovni</th>
                <th scope="col" class="numeric">Srednji</th>
                <th scope="col" class="numeric">Prodajni</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="rate in history" :key="rate.id">
                <td>{{ formatDateTime(rate.validFrom) }}</td>
                <td class="numeric mono">{{ formatNumber(rate.buyRate, 4) }}</td>
                <td class="numeric mono">{{ formatNumber(rate.middleRate, 4) }}</td>
                <td class="numeric mono">{{ formatNumber(rate.sellRate, 4) }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </AppCard>
    </template>
  </div>
</template>

<style scoped>
.align-start {
  align-items: start;
}

.rate-row {
  display: grid;
  grid-template-columns: 48px repeat(3, minmax(0, 1fr));
  gap: var(--space-3);
  align-items: end;
}

.currency {
  padding-bottom: var(--space-3);
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
  font-weight: 600;
  font-size: var(--font-size-sm);
  cursor: pointer;
}

.segmented button.active {
  background: var(--color-surface);
  box-shadow: var(--shadow-sm);
}

.form-error {
  color: var(--color-danger);
  font-size: var(--font-size-sm);
}
</style>
