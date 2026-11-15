<script setup lang="ts">
import { computed } from 'vue'
import { RouterLink } from 'vue-router'
import AppModal from '@/components/ui/AppModal.vue'
import AppBadge from '@/components/ui/AppBadge.vue'
import MoneyAmount from '@/components/ui/MoneyAmount.vue'
import { transactionTypeLabels } from '@/utils/labels'
import { formatAccountNumber, formatDateTime } from '@/utils/format'
import type { Transaction } from '@/types/api'

const props = defineProps<{ transaction: Transaction | null; allowComplaint?: boolean }>()
defineEmits<{ close: [] }>()

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
  ].filter((row) => row.value)
})
</script>

<template>
  <AppModal :open="transaction !== null" title="Detalji transakcije" width="560px" @close="$emit('close')">
    <template v-if="transaction">
      <div class="hero">
        <MoneyAmount class="hero-amount" :amount="transaction.amount" :currency="transaction.currency" />
        <AppBadge :tone="transaction.status === 'Uspesna' ? 'success' : transaction.status === 'NaCekanju' ? 'warning' : 'danger'">
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
    </template>
    <template v-if="transaction && allowComplaint" #footer>
      <RouterLink :to="{ path: '/prigovori', query: { transakcija: transaction.id } }" class="complaint-link" @click="$emit('close')">
        Nešto nije u redu? Podnesite prigovor
      </RouterLink>
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

.complaint-link {
  font-size: var(--font-size-sm);
  font-weight: 600;
  color: var(--color-primary-hover);
}
</style>
