<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { RouterLink } from 'vue-router'
import AppModal from '@/components/ui/AppModal.vue'
import AppBadge from '@/components/ui/AppBadge.vue'
import AppButton from '@/components/ui/AppButton.vue'
import AppField from '@/components/ui/AppField.vue'
import MoneyAmount from '@/components/ui/MoneyAmount.vue'
import { reversalsApi } from '@/api/bank'
import { useUiStore } from '@/stores/ui'
import { errorMessage } from '@/composables/useAsync'
import { transactionTypeLabels } from '@/utils/labels'
import { formatAccountNumber, formatDateTime } from '@/utils/format'
import type { Transaction } from '@/types/api'

const props = defineProps<{ transaction: Transaction | null; allowComplaint?: boolean; allowReversalRequest?: boolean }>()
const emit = defineEmits<{ close: []; changed: [] }>()

const ui = useUiStore()
const reversalOpen = ref(false)
const reason = ref('')
const reversalError = ref<string | null>(null)
const isRequesting = ref(false)

const canRequestReversal = computed(
  () =>
    props.allowReversalRequest &&
    props.transaction !== null &&
    props.transaction.status === 'Uspesna' &&
    !props.transaction.reversedAt &&
    ['Uplata', 'Isplata', 'Transfer'].includes(props.transaction.type),
)

const rows = computed(() => {
  const transaction = props.transaction
  if (!transaction) return []
  return [
    { label: 'Referenca', value: transaction.referenceNumber, mono: true },
    { label: 'Vrsta', value: transactionTypeLabels[transaction.type] },
    { label: 'Datum i vreme', value: formatDateTime(transaction.createdAt) },
    { label: 'Sa računa', value: transaction.fromAccountNumber ? formatAccountNumber(transaction.fromAccountNumber) : null, mono: true },
    {
      label: 'Na račun',
      value: transaction.toAccountNumber
        ? formatAccountNumber(transaction.toAccountNumber)
        : transaction.counterpartyAccountNumber
          ? formatAccountNumber(transaction.counterpartyAccountNumber)
          : null,
      mono: true,
    },
    { label: 'Primalac', value: transaction.counterpartyName },
    { label: 'Šifra plaćanja', value: transaction.paymentCode },
    {
      label: 'Poziv na broj',
      value: transaction.paymentReferenceNumber
        ? `${transaction.paymentReferenceModel ? `(${transaction.paymentReferenceModel}) ` : ''}${transaction.paymentReferenceNumber}`
        : null,
      mono: true,
    },
    { label: 'Opis', value: transaction.description },
    { label: 'Izvršio', value: transaction.performedBy },
    { label: 'Stornirana', value: transaction.reversedAt ? formatDateTime(transaction.reversedAt) : null },
    { label: 'Storno transakcije', value: transaction.reversalOfTransactionId ? `#${transaction.reversalOfTransactionId}` : null },
  ].filter((row) => row.value)
})

watch(
  () => props.transaction,
  () => {
    reversalOpen.value = false
    reason.value = ''
    reversalError.value = null
  },
)

async function requestReversal(): Promise<void> {
  if (!props.transaction) return
  if (reason.value.trim().length < 10) {
    reversalError.value = 'Razlog mora imati najmanje 10 karaktera.'
    return
  }
  isRequesting.value = true
  reversalError.value = null
  try {
    const created = await reversalsApi.request(props.transaction.id, reason.value.trim())
    ui.notify(`Zahtev ${created.referenceNumber} je poslat administratoru na odobrenje.`)
    emit('changed')
    emit('close')
  } catch (caught) {
    reversalError.value = errorMessage(caught)
  } finally {
    isRequesting.value = false
  }
}
</script>

<template>
  <AppModal :open="transaction !== null" title="Detalji transakcije" width="560px" @close="emit('close')">
    <template v-if="transaction">
      <div class="hero">
        <MoneyAmount class="hero-amount" :amount="transaction.amount" :currency="transaction.currency" />
        <AppBadge v-if="transaction.reversedAt" tone="neutral">Stornirana</AppBadge>
        <AppBadge v-else :tone="transaction.status === 'Uspesna' ? 'success' : transaction.status === 'NaCekanju' ? 'warning' : 'danger'">
          {{ transaction.status === 'Uspesna' ? 'Uspešna' : transaction.status === 'NaCekanju' ? 'Na čekanju' : 'Neuspešna' }}
        </AppBadge>
        <p v-if="transaction.feeAmount > 0" class="subtle">
          Naknada <MoneyAmount :amount="transaction.feeAmount" :currency="transaction.currency" />
        </p>
      </div>
      <dl class="details">
        <div v-for="row in rows" :key="row.label" class="detail-row">
          <dt>{{ row.label }}</dt>
          <dd :class="{ mono: row.mono }">{{ row.value }}</dd>
        </div>
      </dl>
      <form v-if="reversalOpen" class="reversal stack" @submit.prevent="requestReversal">
        <AppField v-model="reason" label="Razlog storniranja" multiline :rows="3" :maxlength="500" hint="Zahtev odobrava administrator koji nije podneo zahtev. Klijent dobija obaveštenje." :error="reversalError" />
        <div class="row-between">
          <AppButton variant="ghost" @click="reversalOpen = false">Odustani</AppButton>
          <AppButton type="submit" variant="danger" :loading="isRequesting">Pošalji zahtev</AppButton>
        </div>
      </form>
    </template>
    <template v-if="transaction && (allowComplaint || canRequestReversal) && !reversalOpen" #footer>
      <RouterLink v-if="allowComplaint" :to="{ path: '/prigovori', query: { transakcija: transaction.id } }" class="complaint-link" @click="emit('close')">
        Nešto nije u redu? Podnesite prigovor
      </RouterLink>
      <AppButton v-if="canRequestReversal" variant="secondary" icon="refresh" @click="reversalOpen = true">Zatraži storniranje</AppButton>
    </template>
  </AppModal>
</template>

<style scoped>
.hero {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: var(--space-2);
  padding: var(--space-5);
  margin-bottom: var(--space-4);
  border-radius: var(--radius-lg);
  background: var(--color-cream);
}

.hero-amount {
  font-size: var(--font-size-3xl);
  font-weight: 600;
  color: var(--color-forest-900);
  letter-spacing: -0.03em;
}

.details {
  margin: 0;
}

.detail-row {
  display: flex;
  justify-content: space-between;
  gap: var(--space-4);
  padding: var(--space-3) 0;
  border-bottom: 1px solid var(--color-border);
}

dt {
  color: var(--color-text-muted);
  font-size: var(--font-size-sm);
}

dd {
  margin: 0;
  font-weight: 500;
  text-align: right;
  word-break: break-word;
}

.reversal {
  margin-top: var(--space-4);
  padding: var(--space-4);
  border-radius: var(--radius-md);
  background: var(--color-warning-soft);
}

.complaint-link {
  font-size: var(--font-size-sm);
  font-weight: 600;
  color: var(--color-primary-hover);
}
</style>
