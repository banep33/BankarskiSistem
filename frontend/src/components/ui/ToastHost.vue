<script setup lang="ts">
import { useUiStore } from '@/stores/ui'
import AppIcon from './AppIcon.vue'

const ui = useUiStore()
const icons = { success: 'check', danger: 'alert', info: 'info' } as const
</script>

<template>
  <div class="toast-host" aria-live="polite" aria-atomic="false">
    <TransitionGroup name="toast">
      <div v-for="toast in ui.toasts" :key="toast.id" class="toast" :class="`tone-${toast.tone}`" role="status">
        <AppIcon :name="icons[toast.tone]" :size="18" />
        <p>{{ toast.message }}</p>
        <button type="button" class="dismiss" aria-label="Zatvori obaveštenje" @click="ui.dismiss(toast.id)">
          <AppIcon name="x" :size="16" />
        </button>
      </div>
    </TransitionGroup>
  </div>
</template>

<style scoped>
.toast-host {
  position: fixed;
  right: var(--space-6);
  bottom: var(--space-6);
  z-index: 700;
  display: flex;
  flex-direction: column;
  gap: var(--space-2);
  width: min(380px, calc(100vw - 2rem));
}

.toast {
  display: flex;
  align-items: flex-start;
  gap: var(--space-3);
  padding: var(--space-4);
  border-radius: var(--radius-md);
  background: var(--color-contrast-bg);
  color: var(--color-contrast-text);
  box-shadow: var(--shadow-lg);
  font-size: var(--font-size-sm);
}

.toast p {
  flex: 1;
}

.tone-success {
  border-left: 4px solid var(--color-lime-400);
}

.tone-danger {
  border-left: 4px solid var(--color-coral-500);
}

.tone-info {
  border-left: 4px solid #7fb3d5;
}

.dismiss {
  border: none;
  background: transparent;
  color: inherit;
  opacity: 0.7;
  cursor: pointer;
}

.toast-enter-active,
.toast-leave-active {
  transition: all var(--transition-base);
}

.toast-enter-from,
.toast-leave-to {
  opacity: 0;
  transform: translateY(8px);
}

@media (max-width: 720px) {
  .toast-host {
    right: var(--space-4);
    bottom: calc(var(--space-4) + 64px);
  }
}
</style>
