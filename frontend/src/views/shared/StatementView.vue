<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute } from 'vue-router'
import AppCard from '@/components/ui/AppCard.vue'
import AppButton from '@/components/ui/AppButton.vue'
import AppField from '@/components/ui/AppField.vue'
import StatusViews from '@/components/ui/StatusViews.vue'
import { statementsApi } from '@/api/bank'
import { useAuthStore } from '@/stores/auth'
import { useUiStore } from '@/stores/ui'
import { errorMessage } from '@/composables/useAsync'
import { transactionTypeLabels } from '@/utils/labels'
import { formatAccountNumber, formatDate, formatDateTime, formatNumber, toIsoDate } from '@/utils/format'
import type { AccountStatement } from '@/types/api'

type Preset = 'this-month' | 'last-month' | 'last-90' | 'custom'

const route = useRoute()
const auth = useAuthStore()
const ui = useUiStore()
const accountId = computed(() => Number(route.params.id))
const statement = ref<AccountStatement | null>(null)
const isLoading = ref(false)
const isDownloading = ref(false)
const error = ref<string | null>(null)
const preset = ref<Preset>('this-month')
const from = ref('')
const to = ref('')

const presetOptions = [
  { value: 'this-month', label: 'Ovaj mesec' },
  { value: 'last-month', label: 'Prethodni mesec' },
  { value: 'last-90', label: 'Poslednjih 90 dana' },
  { value: 'custom', label: 'Izaberi period' },
]
const backLink = computed(() => (auth.isCustomer ? `/racuni/${accountId.value}` : statement.value ? `/bankar/klijenti/${statement.value.ownerUserId}` : '/bankar/klijenti'))

function applyPreset(): void {
  const now = new Date()
  if (preset.value === 'this-month') {
    from.value = toIsoDate(new Date(now.getFullYear(), now.getMonth(), 1))
    to.value = toIsoDate(now)
  } else if (preset.value === 'last-month') {
    from.value = toIsoDate(new Date(now.getFullYear(), now.getMonth() - 1, 1))
    to.value = toIsoDate(new Date(now.getFullYear(), now.getMonth(), 0))
  } else if (preset.value === 'last-90') {
    from.value = toIsoDate(new Date(now.getFullYear(), now.getMonth(), now.getDate() - 89))
    to.value = toIsoDate(now)
  }
}

async function load(): Promise<void> {
  if (!from.value || !to.value) return
  isLoading.value = true
  error.value = null
  try {
    statement.value = await statementsApi.get(accountId.value, from.value, to.value)
  } catch (caught) {
    error.value = errorMessage(caught)
  } finally {
    isLoading.value = false
  }
}

async function choosePreset(value: Preset): Promise<void> {
  preset.value = value
  if (value !== 'custom') {
    applyPreset()
    await load()
  }
}

async function downloadCsv(): Promise<void> {
  isDownloading.value = true
  try {
    await statementsApi.csv(accountId.value, from.value, to.value)
  } catch (caught) {
    ui.notify(errorMessage(caught), 'danger')
  } finally {
    isDownloading.value = false
  }
}

function printStatement(): void {
  window.print()
}

function amount(value: number): string {
  return value === 0 ? '' : formatNumber(value, 2)
}

onMounted(async () => {
  applyPreset()
  await load()
})
</script>

<template>
  <div class="page">
    <header class="page-header no-print">
      <div>
        <RouterLink :to="backLink" class="back">← Nazad</RouterLink>
        <h1 class="page-title">Izvod po računu</h1>
        <p class="page-subtitle">Sve promene i stanja za izabrani period. Izvod se pravi iz glavne knjige banke.</p>
      </div>
      <div class="row">
        <AppButton variant="secondary" icon="download" :loading="isDownloading" :disabled="!statement" @click="downloadCsv">Preuzmi CSV</AppButton>
        <AppButton icon="file" :disabled="!statement" @click="printStatement">Štampaj ili sačuvaj PDF</AppButton>
      </div>
    </header>

    <AppCard class="no-print">
      <div class="filters">
        <AppField :model-value="preset" label="Period" :options="presetOptions" @update:model-value="choosePreset($event as Preset)" />
        <template v-if="preset === 'custom'">
          <AppField v-model="from" label="Od" type="date" />
          <AppField v-model="to" label="Do" type="date" />
          <AppButton icon="search" @click="load">Prikaži</AppButton>
        </template>
      </div>
      <p class="subtle hint">Za PDF izaberite „Sačuvaj kao PDF“ u prozoru za štampu. CSV se otvara u Excel-u sa srpskim formatom brojeva.</p>
    </AppCard>

    <StatusViews v-if="isLoading" state="loading" :rows="6" />
    <StatusViews v-else-if="error" state="error" :message="error" @retry="load" />

    <article v-else-if="statement" class="statement">
      <header class="statement-head">
        <div>
          <p class="bank">Bankarski Sistem</p>
          <h2>Izvod po računu</h2>
          <p class="muted">Period {{ formatDate(statement.from) }} do {{ formatDate(statement.to) }}</p>
        </div>
        <dl class="meta">
          <div><dt>Vlasnik</dt><dd>{{ statement.ownerName }}</dd></div>
          <div><dt>Račun</dt><dd class="mono">{{ formatAccountNumber(statement.accountNumber) }}</dd></div>
          <div><dt>Valuta</dt><dd>{{ statement.currency }}</dd></div>
          <div><dt>Izrađen</dt><dd>{{ formatDateTime(statement.generatedAt) }}</dd></div>
        </dl>
      </header>

      <div class="totals">
        <div><span>Početno stanje</span><strong class="mono">{{ formatNumber(statement.openingBalance, 2) }}</strong></div>
        <div><span>Ukupno potražuje</span><strong class="mono credit">{{ formatNumber(statement.totalCredits, 2) }}</strong></div>
        <div><span>Ukupno duguje</span><strong class="mono">{{ formatNumber(statement.totalDebits, 2) }}</strong></div>
        <div><span>Krajnje stanje</span><strong class="mono">{{ formatNumber(statement.closingBalance, 2) }}</strong></div>
      </div>

      <StatusViews v-if="!statement.lines.length" state="empty" icon="file" title="Nema promena u izabranom periodu" />
      <div v-else class="table-scroll">
        <table class="data-table statement-table">
          <thead>
            <tr>
              <th scope="col">Datum</th>
              <th scope="col">Opis i protivstrana</th>
              <th scope="col">Referenca</th>
              <th scope="col" class="numeric">Duguje</th>
              <th scope="col" class="numeric">Potražuje</th>
              <th scope="col" class="numeric">Stanje</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="(line, index) in statement.lines" :key="index">
              <td class="nowrap">{{ formatDateTime(line.date) }}</td>
              <td>
                <strong>{{ line.counterparty }}</strong>
                <div class="subtle">{{ line.type ? transactionTypeLabels[line.type] : '' }}<template v-if="line.description">, {{ line.description }}</template></div>
              </td>
              <td class="mono small">{{ line.reference }}</td>
              <td class="numeric mono">{{ amount(line.debit) }}</td>
              <td class="numeric mono credit">{{ amount(line.credit) }}</td>
              <td class="numeric mono">{{ formatNumber(line.balanceAfter, 2) }}</td>
            </tr>
          </tbody>
        </table>
      </div>
      <p class="footnote subtle">Iznosi su u valuti računa ({{ statement.currency }}). Duguje umanjuje, a potražuje uvećava stanje. Izvod je informativan i izrađen elektronski.</p>
    </article>
  </div>
</template>

<style scoped>
.back {
  font-size: var(--font-size-sm);
  color: var(--color-text-muted);
  text-decoration: none;
}

.filters {
  display: flex;
  align-items: flex-end;
  gap: var(--space-3);
  flex-wrap: wrap;
}

.filters > * {
  min-width: 180px;
}

.hint {
  margin-top: var(--space-3);
}

.statement {
  padding: var(--space-8);
  border-radius: var(--radius-lg);
  background: var(--color-surface);
  border: 1px solid var(--color-border);
}

.statement-head {
  display: flex;
  justify-content: space-between;
  gap: var(--space-6);
  flex-wrap: wrap;
  padding-bottom: var(--space-5);
  border-bottom: 2px solid var(--color-text);
}

.bank {
  font-weight: 700;
  color: var(--color-link);
}

.meta {
  margin: 0;
  display: grid;
  grid-template-columns: auto auto;
  gap: var(--space-1) var(--space-4);
  font-size: var(--font-size-sm);
}

.meta div {
  display: contents;
}

.meta dt {
  color: var(--color-text-muted);
}

.meta dd {
  margin: 0;
  font-weight: 600;
}

.totals {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: var(--space-4);
  margin: var(--space-5) 0;
}

.totals div {
  display: flex;
  flex-direction: column;
  padding: var(--space-3);
  border-radius: var(--radius-md);
  background: var(--color-surface-muted);
  font-size: var(--font-size-sm);
}

.totals strong {
  font-size: var(--font-size-lg);
}

.credit {
  color: var(--color-success);
}

.nowrap {
  white-space: nowrap;
}

.small {
  font-size: var(--font-size-xs);
}

.footnote {
  margin-top: var(--space-4);
}

@media (max-width: 720px) {
  .totals {
    grid-template-columns: repeat(2, 1fr);
  }

  .statement {
    padding: var(--space-4);
  }
}

@media print {
  .statement {
    border: none;
    padding: 0;
  }

  .statement-table {
    font-size: 10pt;
  }

  .totals div {
    background: none;
    border: 1px solid #999;
  }
}
</style>
