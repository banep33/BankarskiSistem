<script setup lang="ts">
import { nextTick, onBeforeUnmount, ref, useId, watch } from 'vue'
import AppIcon from './AppIcon.vue'

const props = withDefaults(defineProps<{ open: boolean; title: string; width?: string; dismissible?: boolean }>(), {
  width: '520px',
  dismissible: true,
})
const emit = defineEmits<{ close: [] }>()

const dialog = ref<HTMLElement | null>(null)
const titleId = useId()
let previousFocus: HTMLElement | null = null

function close(): void {
  if (props.dismissible) emit('close')
}

function focusableElements(): HTMLElement[] {
  return Array.from(
    dialog.value?.querySelectorAll<HTMLElement>('button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])') ?? [],
  ).filter((element) => !element.hasAttribute('disabled'))
}

function onKeydown(event: KeyboardEvent): void {
  if (event.key === 'Escape') {
    close()
    return
  }
  if (event.key !== 'Tab') return
  const elements = focusableElements()
  const first = elements[0]
  const last = elements[elements.length - 1]
  if (!first || !last) return
  if (event.shiftKey && document.activeElement === first) {
    event.preventDefault()
    last.focus()
  } else if (!event.shiftKey && document.activeElement === last) {
    event.preventDefault()
    first.focus()
  }
}

watch(
  () => props.open,
  async (open) => {
    if (open) {
      previousFocus = document.activeElement as HTMLElement | null
      document.body.style.overflow = 'hidden'
      await nextTick()
      const firstInput = dialog.value?.querySelector<HTMLElement>('input, select, textarea')
      ;(firstInput ?? focusableElements()[0])?.focus()
    } else {
      document.body.style.overflow = ''
      previousFocus?.focus()
    }
  },
  { immediate: true },
)

onBeforeUnmount(() => {
  document.body.style.overflow = ''
})
</script>

<template>
  <Teleport to="body">
    <Transition name="modal">
      <div v-if="open" class="backdrop" @mousedown.self="close">
        <div
          ref="dialog"
          class="dialog"
          role="dialog"
          aria-modal="true"
          :aria-labelledby="titleId"
          :style="{ maxWidth: width }"
          @keydown="onKeydown"
        >
          <header class="dialog-header">
            <h2 :id="titleId" class="dialog-title">{{ title }}</h2>
            <button v-if="dismissible" type="button" class="close" aria-label="Zatvori" @click="close">
              <AppIcon name="x" />
            </button>
          </header>
          <div class="dialog-body"><slot /></div>
          <footer v-if="$slots.footer" class="dialog-footer"><slot name="footer" /></footer>
        </div>
      </div>
    </Transition>
  </Teleport>
</template>

<style scoped>
.backdrop {
  position: fixed;
  inset: 0;
  z-index: 500;
  display: grid;
  place-items: center;
  padding: var(--space-4);
  background: rgb(6 21 15 / 0.55);
  backdrop-filter: blur(3px);
}

.dialog {
  width: 100%;
  max-height: calc(100vh - 2rem);
  overflow-y: auto;
  background: var(--color-surface);
  border-radius: var(--radius-xl);
  box-shadow: var(--shadow-lg);
}

.dialog-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-4);
  padding: var(--space-6) var(--space-6) 0;
}

.dialog-title {
  font-size: var(--font-size-xl);
}

.close {
  display: grid;
  place-items: center;
  width: 36px;
  height: 36px;
  border: none;
  border-radius: 50%;
  background: var(--color-surface-muted);
  cursor: pointer;
}

.dialog-body {
  padding: var(--space-5) var(--space-6);
}

.dialog-footer {
  display: flex;
  justify-content: flex-end;
  gap: var(--space-3);
  padding: 0 var(--space-6) var(--space-6);
  flex-wrap: wrap;
}

.modal-enter-active,
.modal-leave-active {
  transition: opacity var(--transition-base);
}

.modal-enter-from,
.modal-leave-to {
  opacity: 0;
}
</style>
