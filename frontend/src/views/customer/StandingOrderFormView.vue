<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import AppCard from '@/components/ui/AppCard.vue'
import AppField from '@/components/ui/AppField.vue'
import AppButton from '@/components/ui/AppButton.vue'
import AppIcon from '@/components/ui/AppIcon.vue'
import MoneyAmount from '@/components/ui/MoneyAmount.vue'
import StatusViews from '@/components/ui/StatusViews.vue'
import { beneficiariesApi, helpApi, meApi, standingOrdersApi } from '@/api/bank'
import { ApiError } from '@/api/http'
import { useReauthStore } from '@/stores/reauth'
import { useUiStore } from '@/stores/ui'
import { errorMessage } from '@/composables/useAsync'
import { frequencyLabels } from '@/utils/labels'
import { formatAccountNumber, formatDate, toIsoDate } from '@/utils/format'
import { isValidAccountNumber, normalizeAccountNumber } from '@/utils/validation'
import { previewDates, totalPlannedExecutions } from '@/utils/schedule'
import type { Account, Beneficiary, ReferenceData, StandingOrderFrequency } from '@/types/api'

type EndMode = 'none' | 'date' | 'count'

const route = useRoute()
const router = useRouter()
const reauth = useReauthStore()
const ui = useUiStore()

const accounts = ref<Account[]>([])
const beneficiaries = ref<Beneficiary[]>([])
const referenceData = ref<ReferenceData | null>(null)
const isLoading = ref(true)
const loadError = ref<string | null>(null)
const isSubmitting = ref(false)
const formError = ref<string | null>(null)
const fieldErrors = reactive<Record<string, string | null>>({})
const today = toIsoDate(new Date())

const form = reactive({
  fromAccountId: null as number | null,
  recipientName: '',
  recipientAccountNumber: '',
  recipientAddress: '',
  amount: '' as string | number,
  paymentCode: '289',
  purpose: '',
  referenceModel: '',
  referenceNumber: '',
  frequency: 'Mesecno' as StandingOrderFrequency,
  startDate: today,
  endMode: 'none' as EndMode,
  endDate: '',
  maxExecutions: '' as string | number,
})

const activeAccounts = computed(() => accounts.value.filter((account) => account.status === 'Aktivan' && account.kind === 'Tekuci'))
const fromAccount = computed(() => accounts.value.find((account) => account.id === form.fromAccountId) ?? null)
const accountOptions = computed(() => activeAccounts.value.map((account) => ({ value: account.id, label: `${formatAccountNumber(account.accountNumber)} (${account.currency})` })))
const paymentCodeOptions = computed(() => (referenceData.value?.paymentCodes ?? []).map((item) => ({ value: item.code, label: `${item.code} ${item.description}` })))
const modelOptions = computed(() => [{ value: '', label: 'Bez modela' }, ...(referenceData.value?.referenceModels ?? []).map((model) => ({ value: model, label: `Model ${model}` }))])
const frequencyOptions = Object.entries(frequencyLabels).map(([value, item]) => ({ value, label: item.label }))
const endOptions = [
  { value: 'none', label: 'Bez kraja, dok ga ne otkažem' },
  { value: 'date', label: 'Do određenog datuma' },
  { value: 'count', label: 'Određen broj plaćanja' },
]
const amountValue = computed(() => Number(String(form.amount).replace(',', '.')))
const endDate = computed(() => (form.endMode === 'date' && form.endDate ? form.endDate : null))
const maxExecutions = computed(() => (form.endMode === 'count' && Number(form.maxExecutions) > 0 ? Number(form.maxExecutions) : null))
const preview = computed(() => (form.startDate ? previewDates(form.startDate, form.frequency, endDate.value, maxExecutions.value, 4) : []))
const planned = computed(() => (form.startDate ? totalPlannedExecutions(form.startDate, form.frequency, endDate.value, maxExecutions.value) : null))

function applyBeneficiary(beneficiary: Beneficiary): void {
  form.recipientName = beneficiary.recipientName
  form.recipientAccountNumber = beneficiary.accountNumber
  form.recipientAddress = beneficiary.recipientAddress ?? ''
  form.paymentCode = beneficiary.paymentCode ?? form.paymentCode
  form.purpose = beneficiary.purpose ?? form.purpose
  form.referenceModel = beneficiary.referenceModel ?? ''
  form.referenceNumber = beneficiary.referenceNumber ?? ''
}

function validate(): boolean {
  Object.keys(fieldErrors).forEach((key) => (fieldErrors[key] = null))
  if (!form.fromAccountId) fieldErrors.fromAccountId = 'Izaberite račun.'
  if (form.recipientName.trim().length < 2) fieldErrors.recipientName = 'Unesite naziv primaoca.'
  if (!isValidAccountNumber(form.recipientAccountNumber)) fieldErrors.recipientAccountNumber = 'Broj računa nije ispravan.'
  if (!Number.isFinite(amountValue.value) || amountValue.value <= 0) fieldErrors.amount = 'Unesite iznos veći od nule.'
  if (form.purpose.trim().length < 3) fieldErrors.purpose = 'Unesite svrhu plaćanja.'
  if (!form.startDate || form.startDate < today) fieldErrors.startDate = 'Datum početka ne može biti u prošlosti.'
  if (form.endMode === 'date' && (!form.endDate || form.endDate < form.startDate)) fieldErrors.endDate = 'Datum završetka mora biti posle datuma početka.'
  if (form.endMode === 'count' && !(Number(form.maxExecutions) >= 1 && Number(form.maxExecutions) <= 600)) fieldErrors.maxExecutions = 'Unesite broj od 1 do 600.'
  return Object.values(fieldErrors).every((value) => !value)
}

async function submit(): Promise<void> {
  formError.value = null
  if (!validate()) return
  isSubmitting.value = true
  try {
    const created = await reauth.runWithReauth(
      () =>
        standingOrdersApi.create({
          fromAccountId: form.fromAccountId!,
          recipientName: form.recipientName.trim(),
          recipientAccountNumber: normalizeAccountNumber(form.recipientAccountNumber),
          recipientAddress: form.recipientAddress.trim() || null,
          amount: amountValue.value,
          paymentCode: form.paymentCode,
          purpose: form.purpose.trim(),
          referenceModel: form.referenceModel || null,
          referenceNumber: form.referenceNumber.trim() || null,
          frequency: form.frequency,
          startDate: form.startDate,
          endDate: endDate.value,
          maxExecutions: maxExecutions.value,
        }),
      'Trajni nalog je trajno ovlašćenje banci. Potvrdite lozinkom.',
    )
    if (!created) return
    ui.notify(`Trajni nalog ${created.referenceNumber} je aktiviran.`)
    await router.push(`/trajni-nalozi/${created.id}`)
  } catch (caught) {
    formError.value = caught instanceof ApiError && caught.errors.length ? `${caught.message} ${caught.errors.join(' ')}` : errorMessage(caught)
  } finally {
    isSubmitting.value = false
  }
}

async function load(): Promise<void> {
  isLoading.value = true
  loadError.value = null
  try {
    ;[accounts.value, beneficiaries.value, referenceData.value] = await Promise.all([meApi.accounts(), beneficiariesApi.list(), helpApi.referenceData()])
    form.fromAccountId = activeAccounts.value.find((account) => account.currency === 'RSD')?.id ?? activeAccounts.value[0]?.id ?? null
    const beneficiary = beneficiaries.value.find((item) => item.id === Number(route.query.primalac))
    if (beneficiary) applyBeneficiary(beneficiary)
    if (typeof route.query.naRacun === 'string') {
      form.recipientAccountNumber = route.query.naRacun
      form.recipientName = 'Sopstveni štedni račun'
      form.purpose = typeof route.query.naziv === 'string' ? `Štednja: ${route.query.naziv}`.slice(0, 140) : 'Štednja'
    }
  } catch (caught) {
    loadError.value = errorMessage(caught)
  } finally {
    isLoading.value = false
  }
}

onMounted(load)
</script>

<template>
  <div class="page">
    <header class="page-header">
      <div>
        <RouterLink to="/trajni-nalozi" class="back">← Trajni nalozi</RouterLink>
        <h1 class="page-title">Novi trajni nalog</h1>
        <p class="page-subtitle">Banka će plaćanje izvršavati automatski, na dan dospeća, dok nalog ne otkažete ili ne istekne.</p>
      </div>
    </header>

    <StatusViews v-if="isLoading" state="loading" :rows="6" />
    <StatusViews v-else-if="loadError" state="error" :message="loadError" @retry="load" />
    <StatusViews v-else-if="!activeAccounts.length" state="empty" icon="wallet" title="Nemate aktivan račun" />

    <form v-else class="layout" novalidate @submit.prevent="submit">
      <div class="stack">
        <AppCard title="Platilac i primalac">
          <AppField v-model="form.fromAccountId" label="Sa računa" :options="accountOptions" required :error="fieldErrors.fromAccountId" />
          <p v-if="fromAccount" class="available">Raspoloživo: <MoneyAmount :amount="fromAccount.balance" :currency="fromAccount.currency" /></p>
          <div v-if="beneficiaries.length" class="chips">
            <span class="subtle">Sačuvani primaoci:</span>
            <button v-for="beneficiary in beneficiaries.slice(0, 8)" :key="beneficiary.id" type="button" class="chip" @click="applyBeneficiary(beneficiary)">
              {{ beneficiary.nickname }}
            </button>
          </div>
          <div class="grid-2 fields">
            <AppField v-model="form.recipientName" label="Naziv primaoca" required :maxlength="150" :error="fieldErrors.recipientName" />
            <AppField v-model="form.recipientAccountNumber" label="Račun primaoca" required inputmode="numeric" placeholder="265-0000000123456-78" :error="fieldErrors.recipientAccountNumber" />
          </div>
        </AppCard>

        <AppCard title="Plaćanje">
          <div class="grid-2">
            <AppField v-model="form.amount" :label="`Iznos (${fromAccount?.currency ?? 'RSD'})`" required inputmode="decimal" :error="fieldErrors.amount" />
            <AppField v-model="form.paymentCode" label="Šifra plaćanja" :options="paymentCodeOptions" />
          </div>
          <AppField v-model="form.purpose" class="fields" label="Svrha" required :maxlength="140" placeholder="npr. Stanarina" :error="fieldErrors.purpose" />
          <div class="grid-2 fields">
            <AppField v-model="form.referenceModel" label="Model" :options="modelOptions" />
            <AppField v-model="form.referenceNumber" label="Poziv na broj" :maxlength="23" />
          </div>
        </AppCard>

        <AppCard title="Raspored">
          <div class="grid-2">
            <AppField v-model="form.frequency" label="Učestalost" :options="frequencyOptions" />
            <AppField v-model="form.startDate" label="Prvo plaćanje" type="date" :min="today" :error="fieldErrors.startDate" />
          </div>
          <AppField v-model="form.endMode" class="fields" label="Kraj" :options="endOptions" />
          <AppField v-if="form.endMode === 'date'" v-model="form.endDate" class="fields" label="Poslednje plaćanje najkasnije" type="date" :min="form.startDate" :error="fieldErrors.endDate" />
          <AppField v-if="form.endMode === 'count'" v-model="form.maxExecutions" class="fields" label="Broj plaćanja" type="number" min="1" max="600" :error="fieldErrors.maxExecutions" />
          <p class="subtle hint">Ako je dan u mesecu veći od broja dana u mesecu (na primer 31.), plaćanje ide poslednjeg dana tog meseca.</p>
        </AppCard>
      </div>

      <aside class="side">
        <AppCard tone="dark" title="Pregled">
          <p class="summary-line">
            <MoneyAmount :amount="amountValue || 0" :currency="fromAccount?.currency ?? 'RSD'" :sensitive="false" />
            {{ frequencyLabels[form.frequency].adverb }}
          </p>
          <p class="subtle-inverse">Prva plaćanja:</p>
          <ol class="dates">
            <li v-for="date in preview" :key="date">{{ formatDate(date) }}</li>
          </ol>
          <p class="subtle-inverse">{{ planned === null ? 'Bez unapred određenog kraja.' : `Ukupno plaćanja: ${planned}.` }}</p>
          <p class="subtle-inverse">Naknada se obračunava za svako plaćanje prema cenovniku, kao kod običnog naloga.</p>
        </AppCard>
        <div v-if="formError" class="error-banner" role="alert"><AppIcon name="alert" :size="18" /> {{ formError }}</div>
        <AppButton type="submit" size="lg" block icon="check" :loading="isSubmitting">Aktiviraj trajni nalog</AppButton>
      </aside>
    </form>
  </div>
</template>

<style scoped>
.back {
  font-size: var(--font-size-sm);
  color: var(--color-text-muted);
  text-decoration: none;
}

.layout {
  display: grid;
  grid-template-columns: minmax(0, 1.6fr) minmax(280px, 1fr);
  gap: var(--space-5);
  align-items: start;
}

.side {
  position: sticky;
  top: calc(var(--topbar-height) + var(--space-4));
  display: flex;
  flex-direction: column;
  gap: var(--space-3);
}

.fields {
  margin-top: var(--space-4);
}

.available {
  margin-top: var(--space-2);
  font-size: var(--font-size-sm);
  color: var(--color-text-muted);
}

.chips {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: var(--space-2);
  margin-top: var(--space-4);
}

.chip {
  padding: 0.35rem 0.85rem;
  border: 1px solid var(--color-border-strong);
  border-radius: var(--radius-pill);
  background: var(--color-surface);
  font-size: var(--font-size-sm);
  cursor: pointer;
}

.chip:hover {
  border-color: var(--color-primary);
  background: var(--color-primary-soft);
}

.hint {
  margin-top: var(--space-3);
}

.summary-line {
  font-size: var(--font-size-xl);
  font-weight: 600;
  margin-bottom: var(--space-4);
}

.subtle-inverse {
  font-size: var(--font-size-sm);
  color: var(--color-emphasis-text-muted);
  margin-top: var(--space-2);
}

.dates {
  margin: var(--space-2) 0;
  padding-left: var(--space-5);
  color: var(--color-emphasis-accent);
  font-weight: 600;
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

@media (max-width: 1000px) {
  .layout {
    grid-template-columns: minmax(0, 1fr);
  }

  .side {
    position: static;
  }
}
</style>
