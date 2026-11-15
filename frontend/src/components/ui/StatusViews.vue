<script setup lang="ts">
import AppIcon from './AppIcon.vue'
import AppButton from './AppButton.vue'

withDefaults(
  defineProps<{
    state: 'loading' | 'error' | 'empty'
    title?: string
    message?: string | null
    icon?: string
    rows?: number
  }>(),
  { rows: 3 },
)
defineEmits<{ retry: [] }>()
</script>

<template>
  <div v-if="state === 'loading'" class="skeleton-list" aria-busy="true" aria-label="Učitavanje">
    <div v-for="row in rows" :key="row" class="skeleton" />
  </div>
  <div v-else class="status" :class="state" :role="state === 'error' ? 'alert' : undefined">
    <div class="status-icon">
      <AppIcon :name="icon ?? (state === 'error' ? 'alert' : 'info')" :size="24" />
    </div>
    <h3 class="status-title">{{ title ?? (state === 'error' ? 'Podaci nisu učitani' : 'Nema podataka') }}</h3>
    <p v-if="message" class="status-message">{{ message }}</p>
    <AppButton v-if="state === 'error'" variant="secondary" size="sm" icon="refresh" @click="$emit('retry')">Pokušaj ponovo</AppButton>
    <slot />
  </div>
</template>

<style scoped>
.skeleton-list {
  display: flex;
  flex-direction: column;
  gap: var(--space-3);
}

.skeleton {
  height: 52px;
  border-radius: var(--radius-md);
  background: linear-gradient(90deg, var(--color-surface-muted) 25%, var(--color-border) 37%, var(--color-surface-muted) 63%);
  background-size: 400% 100%;
  animation: shimmer 1.4s ease infinite;
}

@keyframes shimmer {
  0% {
    background-position: 100% 50%;
  }
  100% {
    background-position: 0 50%;
  }
}

.status {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: var(--space-2);
  padding: var(--space-8) var(--space-4);
  text-align: center;
}

.status-icon {
  display: grid;
  place-items: center;
  width: 52px;
  height: 52px;
  border-radius: 50%;
  background: var(--color-surface-muted);
  color: var(--color-text-muted);
  margin-bottom: var(--space-2);
}

.error .status-icon {
  background: var(--color-danger-soft);
  color: var(--color-danger);
}

.status-title {
  font-size: var(--font-size-lg);
}

.status-message {
  max-width: 420px;
  color: var(--color-text-muted);
  font-size: var(--font-size-sm);
  margin-bottom: var(--space-2);
}
</style>
