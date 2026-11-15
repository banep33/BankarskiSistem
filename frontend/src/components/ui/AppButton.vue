<script setup lang="ts">
import AppIcon from './AppIcon.vue'

withDefaults(
  defineProps<{
    variant?: 'primary' | 'secondary' | 'ghost' | 'danger' | 'dark' | 'lime'
    size?: 'sm' | 'md' | 'lg'
    type?: 'button' | 'submit'
    icon?: string
    iconRight?: string
    loading?: boolean
    disabled?: boolean
    block?: boolean
  }>(),
  { variant: 'primary', size: 'md', type: 'button' },
)
</script>

<template>
  <button
    :type="type"
    class="app-button"
    :class="[`variant-${variant}`, `size-${size}`, { block, loading }]"
    :disabled="disabled || loading"
    :aria-busy="loading || undefined"
  >
    <span v-if="loading" class="spinner" aria-hidden="true" />
    <AppIcon v-else-if="icon" :name="icon" :size="size === 'sm' ? 16 : 18" />
    <span class="label"><slot /></span>
    <AppIcon v-if="iconRight && !loading" :name="iconRight" :size="size === 'sm' ? 16 : 18" />
  </button>
</template>

<style scoped>
.app-button {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: var(--space-2);
  border: 1px solid transparent;
  border-radius: var(--radius-pill);
  font-weight: 600;
  cursor: pointer;
  transition:
    background var(--transition-fast),
    border-color var(--transition-fast),
    transform var(--transition-fast);
  white-space: nowrap;
}

.app-button:active:not(:disabled) {
  transform: translateY(1px);
}

.app-button:disabled {
  opacity: 0.55;
  cursor: not-allowed;
}

.size-sm {
  padding: 0.4rem 0.85rem;
  font-size: var(--font-size-sm);
}

.size-md {
  padding: 0.65rem 1.2rem;
  font-size: var(--font-size-md);
}

.size-lg {
  padding: 0.85rem 1.6rem;
  font-size: var(--font-size-lg);
}

.block {
  width: 100%;
}

.variant-primary {
  background: var(--color-primary);
  color: #fff;
}

.variant-primary:hover:not(:disabled) {
  background: var(--color-primary-hover);
}

.variant-secondary {
  background: var(--color-surface);
  color: var(--color-text);
  border-color: var(--color-border-strong);
}

.variant-secondary:hover:not(:disabled) {
  background: var(--color-surface-muted);
}

.variant-ghost {
  background: transparent;
  color: var(--color-text);
}

.variant-ghost:hover:not(:disabled) {
  background: var(--color-surface-muted);
}

.variant-danger {
  background: var(--color-danger);
  color: #fff;
}

.variant-dark {
  background: var(--color-forest-900);
  color: #fff;
}

.variant-dark:hover:not(:disabled) {
  background: var(--color-forest-800);
}

.variant-lime {
  background: var(--color-lime-400);
  color: var(--color-forest-900);
}

.variant-lime:hover:not(:disabled) {
  background: var(--color-lime-500);
}

.spinner {
  width: 16px;
  height: 16px;
  border: 2px solid currentColor;
  border-right-color: transparent;
  border-radius: 50%;
  animation: spin 0.7s linear infinite;
}

@keyframes spin {
  to {
    transform: rotate(360deg);
  }
}
</style>
