<script setup lang="ts">
import { computed } from 'vue'
import AppIcon from '@/components/ui/AppIcon.vue'
import AppBadge from '@/components/ui/AppBadge.vue'
import MoneyAmount from '@/components/ui/MoneyAmount.vue'
import { counterpartyLabel, isIncoming, transactionTypeLabels } from '@/utils/labels'
import { formatDateTime } from '@/utils/format'
import type { Transaction } from '@/types/api'

const props = withDefaults(defineProps<{ transactions: Transaction[]; ownAccountIds?: number[]; perspective?: 'customer' | 'staff' }>(), {
  ownAccountIds: () => [],
  perspective: 'customer',
})
defineEmits<{ select: [transaction: Transaction] }>()

const ownIds = computed(() => new Set(props.ownAccountIds))

const rows = computed(() =>
  props.transactions.map((transaction) => {
    const incoming = props.perspective === 'customer' ? isIncoming(transaction, ownIds.value) : transaction.type === 'Uplata'
    const title =
      props.perspective === 'customer'
        ? counterpartyLabel(transaction, incoming)
        : `${transaction.fromAccountNumber ?? 'Šalter'} → ${transaction.toAccountNumber ?? transaction.counterpartyAccountNumber ?? 'Isplata'}`
    return { transaction, incoming, title }
  }),
)
</script>

<template>
  <ul class="transaction-list">
    <li v-for="row in rows" :key="row.transaction.id">
      <button type="button" class="transaction" @click="$emit('select', row.transaction)">
        <span class="icon" :class="{ incoming: row.incoming }">
          <AppIcon :name="row.incoming ? 'arrow-down' : 'arrow-up'" :size="18" />
        </span>
        <span class="details">
          <span class="title">{{ row.title }}</span>
          <span class="meta">
            {{ transactionTypeLabels[row.transaction.type] }} · {{ formatDateTime(row.transaction.createdAt) }}
            <template v-if="row.transaction.description"> · {{ row.transaction.description }}</template>
          </span>
        </span>
        <span class="amount">
          <MoneyAmount :amount="row.transaction.amount" :currency="row.transaction.currency" :sign="row.incoming ? 'income' : 'expense'" />
          <AppBadge v-if="row.transaction.status !== 'Uspesna'" :tone="row.transaction.status === 'NaCekanju' ? 'warning' : 'danger'">
            {{ row.transaction.status === 'NaCekanju' ? 'Na čekanju' : 'Neuspešna' }}
          </AppBadge>
          <span v-else-if="row.transaction.feeAmount > 0 && !row.incoming" class="fee">
            naknada <MoneyAmount :amount="row.transaction.feeAmount" :currency="row.transaction.currency" />
          </span>
        </span>
      </button>
    </li>
  </ul>
</template>

<style scoped>
.transaction-list {
  list-style: none;
  margin: 0;
  padding: 0;
}

.transaction-list li + li {
  border-top: 1px solid var(--color-border);
}

.transaction {
  display: flex;
  align-items: center;
  gap: var(--space-3);
  width: 100%;
  padding: var(--space-3) var(--space-2);
  border: none;
  border-radius: var(--radius-md);
  background: transparent;
  text-align: left;
  cursor: pointer;
}

.transaction:hover {
  background: var(--color-surface-muted);
}

.icon {
  display: grid;
  place-items: center;
  width: 40px;
  height: 40px;
  flex-shrink: 0;
  border-radius: 50%;
  background: var(--color-primary-soft);
  color: var(--color-primary-hover);
}

.icon.incoming {
  background: var(--color-success-soft);
  color: var(--color-success);
}

.details {
  display: flex;
  flex-direction: column;
  min-width: 0;
  flex: 1;
}

.title {
  font-weight: 600;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.meta {
  font-size: var(--font-size-xs);
  color: var(--color-text-subtle);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.amount {
  display: flex;
  flex-direction: column;
  align-items: flex-end;
  gap: 2px;
  font-weight: 600;
}

.fee {
  font-size: var(--font-size-xs);
  font-weight: 400;
  color: var(--color-text-subtle);
}
</style>
