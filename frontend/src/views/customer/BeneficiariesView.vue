<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { RouterLink } from 'vue-router'
import AppCard from '@/components/ui/AppCard.vue'
import AppButton from '@/components/ui/AppButton.vue'
import AppField from '@/components/ui/AppField.vue'
import AppModal from '@/components/ui/AppModal.vue'
import AppIcon from '@/components/ui/AppIcon.vue'
import StatusViews from '@/components/ui/StatusViews.vue'
import { beneficiariesApi, helpApi } from '@/api/bank'
import { useUiStore } from '@/stores/ui'
import { errorMessage } from '@/composables/useAsync'
import { formatAccountNumber, formatDate, initials } from '@/utils/format'
import { isValidAccountNumber, normalizeAccountNumber } from '@/utils/validation'
import type { Beneficiary, BeneficiaryRequest, ReferenceData } from '@/types/api'

const ui = useUiStore()
const beneficiaries = ref<Beneficiary[]>([])
const referenceData = ref<ReferenceData | null>(null)
const isLoading = ref(true)
const error = ref<string | null>(null)
const editing = ref<Beneficiary | null>(null)
const isFormOpen = ref(false)
const isSaving = ref(false)
const formError = ref<string | null>(null)
const toDelete = ref<Beneficiary | null>(null)
const query = ref('')

const form = reactive<BeneficiaryRequest>({
  nickname: '',
  recipientName: '',
  accountNumber: '',
  recipientAddress: '',
  paymentCode: '289',
  purpose: '',
  referenceModel: '',
  referenceNumber: '',
})

const filtered = computed(() => {
  const term = query.value.trim().toLowerCase()
  if (!term) return beneficiaries.value
  return beneficiaries.value.filter((item) => `${item.nickname} ${item.recipientName} ${item.accountNumber}`.toLowerCase().includes(term))
})
const paymentCodeOptions = computed(() => [
  { value: '', label: 'Bez podrazumevane šifre' },
  ...(referenceData.value?.paymentCodes ?? []).map((item) => ({ value: item.code, label: `${item.code} ${item.description}` })),
])
const modelOptions = computed(() => [{ value: '', label: 'Bez modela' }, ...(referenceData.value?.referenceModels ?? []).map((model) => ({ value: model, label: `Model ${model}` }))])

function openForm(beneficiary: Beneficiary | null): void {
  editing.value = beneficiary
  formError.value = null
  Object.assign(form, {
    nickname: beneficiary?.nickname ?? '',
    recipientName: beneficiary?.recipientName ?? '',
    accountNumber: beneficiary?.accountNumber ?? '',
    recipientAddress: beneficiary?.recipientAddress ?? '',
    paymentCode: beneficiary?.paymentCode ?? '289',
    purpose: beneficiary?.purpose ?? '',
    referenceModel: beneficiary?.referenceModel ?? '',
    referenceNumber: beneficiary?.referenceNumber ?? '',
  })
  isFormOpen.value = true
}

async function save(): Promise<void> {
  formError.value = null
  if (!isValidAccountNumber(form.accountNumber ?? '')) {
    formError.value = 'Broj računa nije ispravan.'
    return
  }
  const request: BeneficiaryRequest = {
    nickname: form.nickname.trim(),
    recipientName: form.recipientName.trim(),
    accountNumber: normalizeAccountNumber(form.accountNumber),
    recipientAddress: form.recipientAddress?.trim() || null,
    paymentCode: form.paymentCode || null,
    purpose: form.purpose?.trim() || null,
    referenceModel: form.referenceModel || null,
    referenceNumber: form.referenceNumber?.trim() || null,
  }
  isSaving.value = true
  try {
    if (editing.value) await beneficiariesApi.update(editing.value.id, request)
    else await beneficiariesApi.create(request)
    isFormOpen.value = false
    ui.notify(editing.value ? 'Primalac je izmenjen.' : 'Primalac je sačuvan.')
    await load()
  } catch (caught) {
    formError.value = errorMessage(caught)
  } finally {
    isSaving.value = false
  }
}

async function remove(): Promise<void> {
  if (!toDelete.value) return
  try {
    await beneficiariesApi.remove(toDelete.value.id)
    ui.notify('Primalac je obrisan.')
    toDelete.value = null
    await load()
  } catch (caught) {
    ui.notify(errorMessage(caught), 'danger')
  }
}

async function load(): Promise<void> {
  isLoading.value = true
  error.value = null
  try {
    ;[beneficiaries.value, referenceData.value] = await Promise.all([beneficiariesApi.list(), referenceData.value ?? helpApi.referenceData()])
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
    <header class="page-header">
      <div>
        <h1 class="page-title">Primaoci</h1>
        <p class="page-subtitle">Sačuvani primaoci za brže i sigurnije plaćanje računa.</p>
      </div>
      <AppButton icon="plus" @click="openForm(null)">Dodaj primaoca</AppButton>
    </header>

    <AppField v-if="beneficiaries.length > 4" v-model="query" label="Pretraga primalaca" type="search" placeholder="Naziv ili broj računa" />

    <StatusViews v-if="isLoading" state="loading" :rows="4" />
    <StatusViews v-else-if="error" state="error" :message="error" @retry="load" />
    <StatusViews v-else-if="!beneficiaries.length" state="empty" icon="users" title="Nemate sačuvane primaoce" message="Primaoca možete sačuvati ovde ili prilikom plaćanja." />

    <div v-else class="grid-3">
      <AppCard v-for="beneficiary in filtered" :key="beneficiary.id">
        <div class="beneficiary">
          <span class="avatar">{{ initials(beneficiary.nickname.split(' ')[0], beneficiary.nickname.split(' ')[1]) }}</span>
          <div class="info">
            <h3>{{ beneficiary.nickname }}</h3>
            <p class="muted">{{ beneficiary.recipientName }}</p>
            <p class="mono subtle">{{ formatAccountNumber(beneficiary.accountNumber) }}</p>
            <p v-if="beneficiary.lastUsedAt" class="subtle small">Poslednji put {{ formatDate(beneficiary.lastUsedAt) }}</p>
          </div>
        </div>
        <div class="actions">
          <RouterLink :to="{ path: '/placanja/novo', query: { primalac: beneficiary.id } }" class="pay">
            <AppIcon name="send" :size="16" /> Plati
          </RouterLink>
          <AppButton variant="ghost" size="sm" icon="edit" @click="openForm(beneficiary)">Izmeni</AppButton>
          <AppButton variant="ghost" size="sm" icon="trash" :aria-label="`Obriši ${beneficiary.nickname}`" @click="toDelete = beneficiary">Obriši</AppButton>
        </div>
      </AppCard>
    </div>

    <AppModal :open="isFormOpen" :title="editing ? 'Izmena primaoca' : 'Novi primalac'" width="600px" @close="isFormOpen = false">
      <form id="beneficiary-form" class="stack" @submit.prevent="save">
        <div class="grid-2">
          <AppField v-model="form.nickname" label="Naziv za prikaz" required :maxlength="60" placeholder="npr. Struja" />
          <AppField v-model="form.recipientName" label="Naziv primaoca" required :maxlength="150" />
        </div>
        <AppField v-model="form.accountNumber" label="Račun primaoca" required inputmode="numeric" placeholder="265-0000000123456-78" />
        <AppField v-model="form.recipientAddress" label="Adresa (nije obavezno)" :maxlength="200" />
        <div class="grid-2">
          <AppField v-model="form.paymentCode" label="Šifra plaćanja" :options="paymentCodeOptions" />
          <AppField v-model="form.purpose" label="Svrha" :maxlength="140" />
        </div>
        <div class="grid-2">
          <AppField v-model="form.referenceModel" label="Model" :options="modelOptions" />
          <AppField v-model="form.referenceNumber" label="Poziv na broj" :maxlength="23" />
        </div>
        <p v-if="formError" class="form-error" role="alert">{{ formError }}</p>
      </form>
      <template #footer>
        <AppButton variant="ghost" @click="isFormOpen = false">Odustani</AppButton>
        <AppButton type="submit" form="beneficiary-form" :loading="isSaving">Sačuvaj</AppButton>
      </template>
    </AppModal>

    <AppModal :open="toDelete !== null" title="Obrisati primaoca?" width="420px" @close="toDelete = null">
      <p class="muted">Primalac „{{ toDelete?.nickname }}“ biće uklonjen. Izvršena plaćanja ostaju u istoriji.</p>
      <template #footer>
        <AppButton variant="ghost" @click="toDelete = null">Odustani</AppButton>
        <AppButton variant="danger" @click="remove">Obriši</AppButton>
      </template>
    </AppModal>
  </div>
</template>

<style scoped>
.beneficiary {
  display: flex;
  gap: var(--space-3);
}

.avatar {
  display: grid;
  place-items: center;
  width: 46px;
  height: 46px;
  flex-shrink: 0;
  border-radius: 50%;
  background: var(--color-cream);
  color: var(--color-coral-700);
  font-weight: 700;
}

.info {
  min-width: 0;
}

.info h3 {
  font-size: var(--font-size-lg);
}

.small {
  font-size: var(--font-size-xs);
}

.actions {
  display: flex;
  align-items: center;
  gap: var(--space-1);
  flex-wrap: wrap;
  margin-top: var(--space-4);
  padding-top: var(--space-3);
  border-top: 1px solid var(--color-border);
}

.pay {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  margin-right: auto;
  padding: 0.4rem 0.9rem;
  border-radius: var(--radius-pill);
  background: var(--color-primary);
  color: #fff;
  font-size: var(--font-size-sm);
  font-weight: 600;
  text-decoration: none;
}

.form-error {
  color: var(--color-danger);
  font-size: var(--font-size-sm);
}
</style>
