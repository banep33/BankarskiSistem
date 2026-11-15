<script setup lang="ts">
import type { Tone } from '@/utils/labels'

withDefaults(defineProps<{ tone?: Tone }>(), { tone: 'neutral' })
</script>

<template>
  <span class="badge" :class="`tone-${tone}`"><slot /></span>
</template>

<style scoped>
.badge {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  padding: 0.2rem 0.6rem;
  border-radius: var(--radius-pill);
  font-size: var(--font-size-xs);
  font-weight: 600;
  white-space: nowrap;
}

.tone-neutral {
  background: var(--color-surface-muted);
  color: var(--color-text-muted);
  border: 1px solid var(--color-border);
}

.tone-success {
  background: var(--color-success-soft);
  color: var(--color-success);
}

.tone-danger {
  background: var(--color-danger-soft);
  color: var(--color-danger);
}

.tone-warning {
  background: var(--color-warning-soft);
  color: var(--color-warning);
}

.tone-info {
  background: var(--color-info-soft);
  color: var(--color-info);
}

.tone-brand {
  background: var(--color-primary-soft);
  color: var(--color-link);
}
</style>
