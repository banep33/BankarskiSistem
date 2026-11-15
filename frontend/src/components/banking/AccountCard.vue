<script setup lang="ts">
import { RouterLink } from 'vue-router'
import AppBadge from '@/components/ui/AppBadge.vue'
import MoneyAmount from '@/components/ui/MoneyAmount.vue'
import { accountStatusLabels } from '@/utils/labels'
import { formatAccountNumber } from '@/utils/format'
import type { Account } from '@/types/api'

const props = withDefaults(defineProps<{ account: Account; to?: string; variant?: 'coral' | 'forest' | 'cream' }>(), { variant: 'forest' })
</script>

<template>
  <RouterLink :to="to ?? `/racuni/${props.account.id}`" class="account-card" :class="`variant-${variant}`">
    <div class="top">
      <span class="currency">{{ account.currency }}</span>
      <AppBadge v-if="account.status !== 'Aktivan'" :tone="accountStatusLabels[account.status].tone">
        {{ accountStatusLabels[account.status].label }}
      </AppBadge>
    </div>
    <div class="balance">
      <span class="caption">Raspoloživo</span>
      <MoneyAmount class="amount" :amount="account.balance" :currency="account.currency" />
    </div>
    <span class="number mono">{{ formatAccountNumber(account.accountNumber) }}</span>
  </RouterLink>
</template>

<style scoped>
.account-card {
  position: relative;
  overflow: hidden;
  display: flex;
  flex-direction: column;
  justify-content: space-between;
  gap: var(--space-6);
  min-height: 176px;
  padding: var(--space-5);
  border-radius: var(--radius-lg);
  text-decoration: none;
  transition:
    transform var(--transition-fast),
    box-shadow var(--transition-fast);
}

.account-card:hover {
  transform: translateY(-2px);
  box-shadow: var(--shadow-md);
}

.account-card::after {
  content: '';
  position: absolute;
  right: -40px;
  bottom: -60px;
  width: 160px;
  height: 160px;
  border-radius: 50%;
  border: 1px solid currentColor;
  opacity: 0.18;
}

.variant-forest {
  background: var(--color-forest-900);
  color: #fff;
}

.variant-coral {
  background: linear-gradient(140deg, var(--color-coral-400), var(--color-coral-600));
  color: #fff;
}

.variant-cream {
  background: var(--color-cream);
  color: var(--color-forest-900);
}

.top {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.currency {
  padding: 0.2rem 0.65rem;
  border-radius: var(--radius-pill);
  background: rgb(255 255 255 / 0.18);
  font-size: var(--font-size-xs);
  font-weight: 700;
  letter-spacing: 0.06em;
}

.variant-cream .currency {
  background: rgb(11 35 32 / 0.08);
}

.variant-forest .currency {
  color: var(--color-lime-400);
}

.caption {
  display: block;
  font-size: var(--font-size-xs);
  opacity: 0.75;
}

.amount {
  font-size: var(--font-size-2xl);
  font-weight: 600;
  letter-spacing: -0.02em;
}

.number {
  font-size: var(--font-size-sm);
  opacity: 0.8;
}
</style>
