<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { RouterLink, useRoute } from 'vue-router'
import AppCard from '@/components/ui/AppCard.vue'
import AppField from '@/components/ui/AppField.vue'
import AppButton from '@/components/ui/AppButton.vue'
import AppIcon from '@/components/ui/AppIcon.vue'
import AppBadge from '@/components/ui/AppBadge.vue'
import MoneyAmount from '@/components/ui/MoneyAmount.vue'
import StatusViews from '@/components/ui/StatusViews.vue'
import { beneficiariesApi, helpApi, meApi, paymentsApi } from '@/api/bank'
import { ApiError, newIdempotencyKey } from '@/api/http'
import { useReauthStore } from '@/stores/reauth'
import { errorMessage } from '@/composables/useAsync'
import { paymentStatusLabels } from '@/utils/labels'
import { formatAccountNumber, formatDate, formatDateTime, toIsoDate } from '@/utils/format'
import { isValidAccountNumber, normalizeAccountNumber } from '@/utils/validation'
import type { Account, Beneficiary, PaymentOrder, PaymentQuote, PaymentRequest, ReferenceData } from '@/types/api'

type Step = 'form' | 'review' | 'done'

const route = useRoute()
const reauth = useReauthStore()

const step = ref<Step>('form')
const accounts = ref<Account[]>([])
const beneficiaries = ref<Beneficiary[]>([])
const referenceData = ref<ReferenceData | null>(null)
const isLoading = ref(true)
const loadError = ref<string | null>(null)
const isSubmitting = ref(false)
const formError = ref<string | null>(null)
const fieldErrors = reactive<Record<string, string | null>>({})
const quote = ref<PaymentQuote | null>(null)
const order = ref<PaymentOrder | null>(null)
const idempotencyKey = ref(newIdempotencyKey())
const schedule = ref(false)
const saveBeneficiary = ref(false)

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
  scheduledFor: '',
  beneficiaryNickname: '',
})

const activeAccounts = computed(() => accounts.value.filter((account) => account.status === 'Aktivan' && account.kind === 'Tekuci'))
const fromAccount = computed(() => accounts.value.find((account) => account.id === form.fromAccountId) ?? null)
const accountOptions = computed(() =>
  activeAccounts.value.map((account) => ({ value: account.id, label: `${formatAccountNumber(account.accountNumber)} (${account.currency})` })),
)
const paymentCodeOptions = computed(() => (referenceData.value?.paymentCodes ?? []).map((item) => ({ value: item.code, label: `${item.code} ${item.description}` })))
const modelOptions = computed(() => [{ value: '', label: 'Bez modela' }, ...(referenceData.value?.referenceModels ?? []).map((model) => ({ value: model, label: `Model ${model}` }))])
const minScheduleDate = computed(() => {
  const tomorrow = new Date()
  tomorrow.setDate(tomorrow.getDate() + 1)
  return toIsoDate(tomorrow)
})

function applyBeneficiary(beneficiary: Beneficiary): void {
  form.recipientName = beneficiary.recipientName
  form.recipientAccountNumber = beneficiary.accountNumber
  form.recipientAddress = beneficiary.recipientAddress ?? ''
  form.paymentCode = beneficiary.paymentCode ?? form.paymentCode
  form.purpose = beneficiary.purpose ?? form.purpose
  form.referenceModel = beneficiary.referenceModel ?? ''
  form.referenceNumber = beneficiary.referenceNumber ?? ''
  saveBeneficiary.value = false
}

function validate(): boolean {
  Object.keys(fieldErrors).forEach((key) => (fieldErrors[key] = null))
  const amount = Number(String(form.amount).replace(',', '.'))
  if (!form.fromAccountId) fieldErrors.fromAccountId = 'Izaberite račun sa kog plaćate.'
  if (form.recipientName.trim().length < 2) fieldErrors.recipientName = 'Unesite naziv primaoca.'
  if (!isValidAccountNumber(form.recipientAccountNumber)) fieldErrors.recipientAccountNumber = 'Broj računa nije ispravan. Proverite kontrolni broj (18 cifara).'
  if (!Number.isFinite(amount) || amount <= 0) fieldErrors.amount = 'Unesite iznos veći od nule.'
  else if (Math.round(amount * 100) !== amount * 100) fieldErrors.amount = 'Iznos može imati najviše dve decimale.'
  if (form.purpose.trim().length < 3) fieldErrors.purpose = 'Unesite svrhu plaćanja.'
  if (schedule.value && !form.scheduledFor) fieldErrors.scheduledFor = 'Izaberite datum izvršenja.'
  if (saveBeneficiary.value && form.beneficiaryNickname.trim().length < 2) fieldErrors.beneficiaryNickname = 'Unesite naziv pod kojim čuvate primaoca.'
  return Object.values(fieldErrors).every((value) => !value)
}

function buildRequest(): PaymentRequest {
  return {
    fromAccountId: form.fromAccountId!,
    recipientName: form.recipientName.trim(),
    recipientAccountNumber: normalizeAccountNumber(form.recipientAccountNumber),
    recipientAddress: form.recipientAddress.trim() || null,
    amount: Number(String(form.amount).replace(',', '.')),
    paymentCode: form.paymentCode,
    purpose: form.purpose.trim(),
    referenceModel: form.referenceModel || null,
    referenceNumber: form.referenceNumber.trim() || null,
    scheduledFor: schedule.value ? form.scheduledFor : null,
    saveAsBeneficiary: saveBeneficiary.value ? form.beneficiaryNickname.trim() : null,
  }
}

async function requestQuote(): Promise<void> {
  formError.value = null
  if (!validate()) return
  isSubmitting.value = true
  try {
    quote.value = await paymentsApi.quote(buildRequest())
    idempotencyKey.value = newIdempotencyKey()
    step.value = 'review'
  } catch (error) {
    formError.value = errorMessage(error)
    if (error instanceof ApiError && error.errors.length) formError.value = `${error.message} ${error.errors.join(' ')}`
  } finally {
    isSubmitting.value = false
  }
}

async function confirmPayment(): Promise<void> {
  formError.value = null
  isSubmitting.value = true
  try {
    const result = await reauth.runWithReauth(
      () => paymentsApi.create(buildRequest(), idempotencyKey.value),
      'Za ovaj iznos potrebna je dodatna potvrda lozinkom.',
    )
    if (!result) return
    order.value = result
    step.value = 'done'
    const fresh = await meApi.accounts()
    accounts.value = fresh
  } catch (error) {
    formError.value = errorMessage(error)
  } finally {
    isSubmitting.value = false
  }
}

function startOver(): void {
  Object.assign(form, {
    recipientName: '',
    recipientAccountNumber: '',
    recipientAddress: '',
    amount: '',
    purpose: '',
    referenceModel: '',
    referenceNumber: '',
    scheduledFor: '',
    beneficiaryNickname: '',
  })
  schedule.value = false
  saveBeneficiary.value = false
  quote.value = null
  order.value = null
  step.value = 'form'
}

function printReceipt(): void {
  window.print()
}

async function load(): Promise<void> {
  isLoading.value = true
  loadError.value = null
  try {
    ;[accounts.value, beneficiaries.value, referenceData.value] = await Promise.all([meApi.accounts(), beneficiariesApi.list(), helpApi.referenceData()])
    const requested = Number(route.query.racun)
    form.fromAccountId = activeAccounts.value.find((account) => account.id === requested)?.id ?? activeAccounts.value[0]?.id ?? null
    const beneficiaryId = Number(route.query.primalac)
    const beneficiary = beneficiaries.value.find((item) => item.id === beneficiaryId)
    if (beneficiary) applyBeneficiary(beneficiary)
  } catch (error) {
    loadError.value = errorMessage(error)
  } finally {
    isLoading.value = false
  }
}

watch(schedule, (value) => {
  if (value && !form.scheduledFor) form.scheduledFor = minScheduleDate.value
})

onMounted(load)
</script>

<template>
  <div class="page">
    <header class="page-header">
      <div>
        <h1 class="page-title">Novo plaćanje</h1>
        <p class="page-subtitle">Nalog za prenos po standardu platnog prometa u Republici Srbiji.</p>
      </div>
      <ol class="steps" aria-label="Koraci plaćanja">
        <li :class="{ active: step === 'form', done: step !== 'form' }"><span>1</span> Podaci</li>
        <li :class="{ active: step === 'review', done: step === 'done' }"><span>2</span> Provera</li>
        <li :class="{ active: step === 'done' }"><span>3</span> Potvrda</li>
      </ol>
    </header>

    <StatusViews v-if="isLoading" state="loading" :rows="6" />
    <StatusViews v-else-if="loadError" state="error" :message="loadError" @retry="load" />
    <StatusViews
      v-else-if="activeAccounts.length === 0"
      state="empty"
      icon="wallet"
      title="Nemate aktivan račun"
      message="Plaćanje je moguće samo sa aktivnog računa. Obratite se banci."
    />

    <form v-else-if="step === 'form'" class="payment-layout" novalidate @submit.prevent="requestQuote">
      <div class="stack">
        <AppCard title="Platilac">
          <AppField v-model="form.fromAccountId" label="Sa računa" :options="accountOptions" required :error="fieldErrors.fromAccountId" />
          <p v-if="fromAccount" class="available">
            Raspoloživo: <MoneyAmount :amount="fromAccount.balance" :currency="fromAccount.currency" />
          </p>
        </AppCard>

        <AppCard title="Primalac">
          <div v-if="beneficiaries.length" class="beneficiaries">
            <p class="subtle">Sačuvani primaoci</p>
            <div class="beneficiary-chips">
              <button v-for="beneficiary in beneficiaries.slice(0, 8)" :key="beneficiary.id" type="button" class="beneficiary-chip" @click="applyBeneficiary(beneficiary)">
                {{ beneficiary.nickname }}
              </button>
            </div>
          </div>
          <div class="grid-2 fields">
            <AppField v-model="form.recipientName" label="Naziv primaoca" required :maxlength="150" autocomplete="off" :error="fieldErrors.recipientName" />
            <AppField
              v-model="form.recipientAccountNumber"
              label="Račun primaoca"
              required
              inputmode="numeric"
              placeholder="265-0000000123456-78"
              hint="18 cifara, crtice nisu obavezne"
              :error="fieldErrors.recipientAccountNumber"
            />
          </div>
          <AppField v-model="form.recipientAddress" class="fields" label="Adresa primaoca (nije obavezno)" :maxlength="200" />
        </AppCard>

        <AppCard title="Podaci o plaćanju">
          <div class="grid-2">
            <AppField
              v-model="form.amount"
              :label="`Iznos (${fromAccount?.currency ?? 'RSD'})`"
              required
              inputmode="decimal"
              placeholder="0,00"
              :error="fieldErrors.amount"
            />
            <AppField v-model="form.paymentCode" label="Šifra plaćanja" :options="paymentCodeOptions" required />
          </div>
          <AppField v-model="form.purpose" class="fields" label="Svrha plaćanja" required :maxlength="140" :error="fieldErrors.purpose" />
          <div class="grid-2 fields">
            <AppField v-model="form.referenceModel" label="Model" :options="modelOptions" />
            <AppField v-model="form.referenceNumber" label="Poziv na broj" :maxlength="23" :error="fieldErrors.referenceNumber" />
          </div>
        </AppCard>

        <AppCard title="Dodatne opcije">
          <label class="toggle">
            <input v-model="schedule" type="checkbox" />
            <span>
              <strong>Zakaži plaćanje</strong>
              <small>Nalog se izvršava na izabrani datum, a do tada ga možete otkazati.</small>
            </span>
          </label>
          <AppField v-if="schedule" v-model="form.scheduledFor" class="fields" label="Datum izvršenja" type="date" :min="minScheduleDate" :error="fieldErrors.scheduledFor" />
          <label class="toggle">
            <input v-model="saveBeneficiary" type="checkbox" />
            <span>
              <strong>Sačuvaj primaoca</strong>
              <small>Sledeći put podaci se popunjavaju jednim klikom.</small>
            </span>
          </label>
          <AppField v-if="saveBeneficiary" v-model="form.beneficiaryNickname" class="fields" label="Naziv za primaoca" placeholder="npr. Struja, Stanarina" :maxlength="60" :error="fieldErrors.beneficiaryNickname" />
        </AppCard>
      </div>

      <aside class="side">
        <AppCard tone="cream" title="Pre slanja">
          <ul class="tips">
            <li><AppIcon name="check" :size="16" /> Naknada i ukupan iznos prikazuju se u sledećem koraku, pre potvrde.</li>
            <li><AppIcon name="check" :size="16" /> Plaćanje u okviru banke izvršava se odmah i besplatno je.</li>
            <li><AppIcon name="shield" :size="16" /> Banka vas nikada neće tražiti da hitno platite zbog „zaštite računa“.</li>
          </ul>
        </AppCard>
        <div v-if="formError" class="error-banner" role="alert">
          <AppIcon name="alert" :size="18" />
          <span>{{ formError }}</span>
        </div>
        <AppButton type="submit" size="lg" block icon-right="arrow-right" :loading="isSubmitting">Nastavi na proveru</AppButton>
      </aside>
    </form>

    <div v-else-if="step === 'review' && quote" class="payment-layout">
      <AppCard title="Proverite nalog">
        <dl class="review">
          <div><dt>Sa računa</dt><dd class="mono">{{ fromAccount ? formatAccountNumber(fromAccount.accountNumber) : '' }}</dd></div>
          <div><dt>Primalac</dt><dd>{{ form.recipientName }}</dd></div>
          <div><dt>Račun primaoca</dt><dd class="mono">{{ formatAccountNumber(normalizeAccountNumber(form.recipientAccountNumber)) }}</dd></div>
          <div><dt>Svrha</dt><dd>{{ form.purpose }}</dd></div>
          <div><dt>Šifra plaćanja</dt><dd>{{ form.paymentCode }}</dd></div>
          <div v-if="form.referenceNumber"><dt>Poziv na broj</dt><dd class="mono">{{ form.referenceModel ? `(${form.referenceModel}) ` : '' }}{{ form.referenceNumber }}</dd></div>
          <div><dt>Vrsta</dt><dd>{{ quote.isInternal ? 'Plaćanje u okviru banke' : 'Plaćanje drugoj banci (kliring)' }}</dd></div>
          <div><dt>Izvršenje</dt><dd>{{ quote.isScheduled ? `Zakazano za ${formatDate(form.scheduledFor)}` : 'Odmah' }}</dd></div>
        </dl>
        <div v-if="quote.warnings.length || !quote.isKnownRecipient" class="warnings">
          <p v-if="!quote.isKnownRecipient" class="warning"><AppIcon name="alert" :size="18" /> Prvi put plaćate ovom primaocu. Proverite broj računa i naziv.</p>
          <p v-for="warning in quote.warnings" :key="warning" class="warning"><AppIcon name="alert" :size="18" /> {{ warning }}</p>
        </div>
      </AppCard>

      <aside class="side">
        <AppCard tone="dark" title="Obračun">
          <dl class="calc">
            <div><dt>Iznos</dt><dd><MoneyAmount :amount="quote.amount" :currency="quote.currency" :sensitive="false" /></dd></div>
            <div>
              <dt>Naknada<small v-if="quote.feeName">{{ quote.feeName }}</small></dt>
              <dd><MoneyAmount :amount="quote.fee" :currency="quote.currency" :sensitive="false" /></dd>
            </div>
            <div class="total"><dt>Ukupno sa računa</dt><dd><MoneyAmount :amount="quote.totalDebit" :currency="quote.currency" :sensitive="false" /></dd></div>
            <div><dt>Stanje posle</dt><dd><MoneyAmount :amount="quote.balanceAfter" :currency="quote.currency" /></dd></div>
            <div><dt>Preostali dnevni limit</dt><dd><MoneyAmount :amount="quote.remainingDailyLimit" :currency="quote.currency" :sensitive="false" /></dd></div>
          </dl>
          <p v-if="quote.requiresStrongConfirmation" class="strong-note">
            <AppIcon name="lock" :size="16" /> Za ovaj iznos tražićemo potvrdu lozinkom.
          </p>
        </AppCard>
        <div v-if="formError" class="error-banner" role="alert">
          <AppIcon name="alert" :size="18" />
          <span>{{ formError }}</span>
        </div>
        <AppButton size="lg" block icon="check" :loading="isSubmitting" @click="confirmPayment">
          {{ quote.isScheduled ? 'Potvrdi zakazivanje' : 'Potvrdi i plati' }}
        </AppButton>
        <AppButton variant="ghost" block icon="arrow-left" :disabled="isSubmitting" @click="step = 'form'">Izmeni podatke</AppButton>
      </aside>
    </div>

    <div v-else-if="step === 'done' && order" class="done">
      <AppCard class="receipt">
        <div class="receipt-head">
          <span class="success-icon"><AppIcon name="check" :size="30" /></span>
          <h2>{{ order.status === 'Zakazan' ? 'Plaćanje je zakazano' : order.status === 'UObradi' ? 'Nalog je poslat' : 'Plaćanje je izvršeno' }}</h2>
          <AppBadge :tone="paymentStatusLabels[order.status].tone">{{ paymentStatusLabels[order.status].label }}</AppBadge>
          <p v-if="order.isReplay" class="subtle">Ovaj nalog je već bio poslat. Prikazujemo postojeći rezultat, novac nije skinut dva puta.</p>
        </div>
        <dl class="review">
          <div><dt>Primalac</dt><dd>{{ order.recipientName }}</dd></div>
          <div><dt>Račun primaoca</dt><dd class="mono">{{ formatAccountNumber(order.recipientAccountNumber) }}</dd></div>
          <div><dt>Iznos</dt><dd><MoneyAmount :amount="order.amount" :currency="order.currency" :sensitive="false" /></dd></div>
          <div><dt>Naknada</dt><dd><MoneyAmount :amount="order.feeAmount" :currency="order.currency" :sensitive="false" /></dd></div>
          <div><dt>Ukupno</dt><dd><MoneyAmount :amount="order.totalDebit" :currency="order.currency" :sensitive="false" /></dd></div>
          <div v-if="order.transactionReference"><dt>Referenca</dt><dd class="mono">{{ order.transactionReference }}</dd></div>
          <div v-if="order.scheduledFor"><dt>Datum izvršenja</dt><dd>{{ formatDate(order.scheduledFor) }}</dd></div>
          <div><dt>Kreiran</dt><dd>{{ formatDateTime(order.createdAt) }}</dd></div>
        </dl>
        <div class="row receipt-actions">
          <AppButton variant="secondary" icon="download" @click="printReceipt">Štampaj potvrdu</AppButton>
          <AppButton icon="plus" @click="startOver">Novo plaćanje</AppButton>
          <RouterLink to="/placanja" class="link">Svi nalozi</RouterLink>
        </div>
      </AppCard>
    </div>
  </div>
</template>

<style scoped>
.steps {
  display: flex;
  gap: var(--space-2);
  list-style: none;
  margin: 0;
  padding: 0;
}

.steps li {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  padding: 0.35rem 0.85rem 0.35rem 0.35rem;
  border-radius: var(--radius-pill);
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  font-size: var(--font-size-sm);
  font-weight: 600;
  color: var(--color-text-muted);
}

.steps span {
  display: grid;
  place-items: center;
  width: 24px;
  height: 24px;
  border-radius: 50%;
  background: var(--color-surface-muted);
  font-size: var(--font-size-xs);
}

.steps li.active {
  border-color: var(--color-primary);
  color: var(--color-text);
}

.steps li.active span {
  background: var(--color-primary);
  color: #fff;
}

.steps li.done span {
  background: var(--color-lime-400);
  color: var(--color-forest-900);
}

.payment-layout {
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
  margin-top: var(--space-3);
  font-size: var(--font-size-sm);
  color: var(--color-text-muted);
}

.beneficiaries {
  margin-bottom: var(--space-2);
}

.beneficiary-chips {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-2);
  margin-top: var(--space-2);
}

.beneficiary-chip {
  padding: 0.4rem 0.9rem;
  border: 1px solid var(--color-border-strong);
  border-radius: var(--radius-pill);
  background: var(--color-surface);
  font-size: var(--font-size-sm);
  font-weight: 500;
  cursor: pointer;
}

.beneficiary-chip:hover {
  border-color: var(--color-primary);
  background: var(--color-primary-soft);
}

.toggle {
  display: flex;
  gap: var(--space-3);
  align-items: flex-start;
  padding: var(--space-3) 0;
  cursor: pointer;
}

.toggle input {
  width: 18px;
  height: 18px;
  margin-top: 3px;
  accent-color: var(--color-primary);
}

.toggle small {
  display: block;
  color: var(--color-text-muted);
  font-size: var(--font-size-xs);
}

.tips {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: var(--space-3);
  font-size: var(--font-size-sm);
}

.tips li {
  display: flex;
  gap: var(--space-2);
}

.tips :deep(svg) {
  margin-top: 3px;
  color: var(--color-forest-700);
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

.review {
  margin: 0;
}

.review div {
  display: flex;
  justify-content: space-between;
  gap: var(--space-4);
  padding: var(--space-3) 0;
  border-bottom: 1px solid var(--color-border);
}

.review dt {
  color: var(--color-text-muted);
  font-size: var(--font-size-sm);
}

.review dd {
  margin: 0;
  font-weight: 600;
  text-align: right;
  word-break: break-word;
}

.warnings {
  display: flex;
  flex-direction: column;
  gap: var(--space-2);
  margin-top: var(--space-4);
}

.warning {
  display: flex;
  gap: var(--space-2);
  padding: var(--space-3) var(--space-4);
  border-radius: var(--radius-md);
  background: var(--color-warning-soft);
  color: var(--color-warning);
  font-size: var(--font-size-sm);
  font-weight: 500;
}

.calc {
  margin: 0;
}

.calc div {
  display: flex;
  justify-content: space-between;
  gap: var(--space-3);
  padding: var(--space-2) 0;
  color: var(--color-emphasis-text-muted);
}

.calc dt small {
  display: block;
  font-size: var(--font-size-xs);
  color: var(--color-emphasis-text-muted);
}

.calc dd {
  margin: 0;
  font-weight: 600;
  color: var(--color-emphasis-text);
}

.calc .total {
  margin: var(--space-2) 0;
  padding: var(--space-3) 0;
  border-top: 1px solid var(--color-emphasis-chip-border);
  border-bottom: 1px solid var(--color-emphasis-chip-border);
}

.calc .total dd {
  color: var(--color-emphasis-accent);
  font-size: var(--font-size-xl);
}

.strong-note {
  display: flex;
  gap: var(--space-2);
  align-items: center;
  margin-top: var(--space-3);
  font-size: var(--font-size-sm);
  color: var(--color-emphasis-accent);
}

.done {
  display: flex;
  justify-content: center;
}

.receipt {
  width: 100%;
  max-width: 620px;
}

.receipt-head {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: var(--space-2);
  text-align: center;
  margin-bottom: var(--space-4);
}

.success-icon {
  display: grid;
  place-items: center;
  width: 64px;
  height: 64px;
  border-radius: 50%;
  background: var(--color-lime-400);
  color: var(--color-forest-900);
}

.receipt-actions {
  justify-content: center;
  margin-top: var(--space-6);
  flex-wrap: wrap;
}

.link {
  font-weight: 600;
  color: var(--color-link);
}

@media (max-width: 1000px) {
  .payment-layout {
    grid-template-columns: minmax(0, 1fr);
  }

  .side {
    position: static;
  }
}

@media print {
  .receipt-actions {
    display: none;
  }
}
</style>
