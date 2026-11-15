<script setup lang="ts">
import AppBadge from '@/components/ui/AppBadge.vue'
import { cardStatusLabels } from '@/utils/labels'
import type { PaymentCard } from '@/types/api'

defineProps<{ card: PaymentCard; selected?: boolean }>()
</script>

<template>
  <div class="payment-card" :class="[`brand-${card.brand.toLowerCase()}`, { inactive: card.status !== 'Aktivna', selected }]">
    <div class="top">
      <span class="brand">{{ card.brand }}</span>
      <AppBadge v-if="card.status !== 'Aktivna' || card.isExpired" :tone="card.isExpired ? 'danger' : cardStatusLabels[card.status].tone">
        {{ card.isExpired ? 'Istekla' : cardStatusLabels[card.status].label }}
      </AppBadge>
      <span v-else class="kind">debitna · {{ card.currency }}</span>
    </div>
    <p class="number mono">{{ card.maskedNumber }}</p>
    <div class="bottom">
      <span class="holder">{{ card.cardholderName }}</span>
      <span class="expiry mono">{{ card.expiry }}</span>
    </div>
  </div>
</template>

<style scoped>
.payment-card {
  display: flex;
  flex-direction: column;
  justify-content: space-between;
  gap: var(--space-4);
  aspect-ratio: 1.586;
  max-width: 340px;
  width: 100%;
  padding: var(--space-5);
  border-radius: var(--radius-lg);
  color: #fff;
  background: linear-gradient(135deg, #1f3d2b, #2f6b47);
  box-shadow: var(--shadow-md);
  border: 2px solid transparent;
  transition: transform 0.15s ease;
}

.payment-card.brand-visa {
  background: linear-gradient(135deg, #1a2a5e, #3150a8);
}

.payment-card.brand-mastercard {
  background: linear-gradient(135deg, #3b1d14, #b5452a);
}

.payment-card.inactive {
  filter: grayscale(0.85);
  opacity: 0.8;
}

.payment-card.selected {
  border-color: var(--color-lime-400);
  transform: translateY(-2px);
}

.top,
.bottom {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: var(--space-2);
}

.brand {
  font-weight: 700;
  font-size: var(--font-size-lg);
  letter-spacing: 0.04em;
}

.kind {
  font-size: var(--font-size-xs);
  opacity: 0.8;
}

.number {
  font-size: var(--font-size-lg);
  letter-spacing: 0.08em;
}

.holder {
  font-size: var(--font-size-sm);
  text-transform: uppercase;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.expiry {
  font-size: var(--font-size-sm);
}
</style>
