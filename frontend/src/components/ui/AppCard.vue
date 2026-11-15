<script setup lang="ts">
withDefaults(defineProps<{ title?: string; subtitle?: string; padded?: boolean; tone?: 'default' | 'dark' | 'cream' }>(), {
  padded: true,
  tone: 'default',
})
</script>

<template>
  <section class="card" :class="[`tone-${tone}`, { padded }]">
    <header v-if="title || $slots.actions" class="card-header">
      <div>
        <h2 v-if="title" class="card-title">{{ title }}</h2>
        <p v-if="subtitle" class="card-subtitle">{{ subtitle }}</p>
      </div>
      <div v-if="$slots.actions" class="card-actions"><slot name="actions" /></div>
    </header>
    <slot />
  </section>
</template>

<style scoped>
.card {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-lg);
  box-shadow: var(--shadow-sm);
  min-width: 0;
}

.padded {
  padding: var(--space-6);
}

.card-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: var(--space-4);
  margin-bottom: var(--space-5);
}

.card:not(.padded) .card-header {
  margin-bottom: var(--space-1);
  padding: var(--space-5) var(--space-6) 0;
}

@media (max-width: 720px) {
  .padded {
    padding: var(--space-5);
  }

  .card:not(.padded) .card-header {
    padding: var(--space-4) var(--space-4) 0;
  }

  .card-header {
    flex-wrap: wrap;
  }
}

.card-title {
  font-size: var(--font-size-lg);
}

.card-subtitle {
  margin-top: 2px;
  font-size: var(--font-size-sm);
  color: var(--color-text-muted);
}

.card-actions {
  display: flex;
  gap: var(--space-2);
  flex-wrap: wrap;
}

.tone-dark {
  background: var(--color-emphasis-bg);
  border-color: var(--color-emphasis-border);
  color: var(--color-emphasis-text);
}

.tone-dark .card-subtitle {
  color: var(--color-emphasis-text-muted);
}

.tone-cream {
  background: var(--color-cream);
  border-color: transparent;
}
</style>
