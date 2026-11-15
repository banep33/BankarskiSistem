<script setup lang="ts">
import { computed } from 'vue'
import { useUiStore } from '@/stores/ui'
import { formatMoney } from '@/utils/format'

const props = withDefaults(
  defineProps<{ amount: number; currency?: string; sensitive?: boolean; sign?: 'income' | 'expense' | null }>(),
  { currency: 'RSD', sensitive: true, sign: null },
)

const ui = useUiStore()
const hidden = computed(() => props.sensitive && ui.hideBalances)
const text = computed(() => {
  const value = formatMoney(Math.abs(props.amount), props.currency)
  if (props.sign === 'income') return `+${value}`
  if (props.sign === 'expense') return `−${value}`
  return formatMoney(props.amount, props.currency)
})
</script>

<template>
  <span class="money mono" :class="{ income: sign === 'income' }">
    <template v-if="hidden">
      <span aria-hidden="true">•••••• {{ currency }}</span>
      <span class="visually-hidden">Iznos je sakriven</span>
    </template>
    <template v-else>{{ text }}</template>
  </span>
</template>

<style scoped>
.money {
  white-space: nowrap;
}

.income {
  color: var(--color-success);
}
</style>
