<script setup lang="ts">
import { nextTick, onBeforeUnmount, ref, useId, watch } from 'vue'
import AppIcon from './AppIcon.vue'
import { lockScroll, unlockScroll } from '@/composables/useScrollLock'

const props = withDefaults(defineProps<{ open: boolean; title: string; width?: string; dismissible?: boolean }>(), {
  width: '520px',
  dismissible: true,
})
const emit = defineEmits<{ close: [] }>()

const dialog = ref<HTMLElement | null>(null)
const titleId = useId()
let previousFocus: HTMLElement | null = null
let isLocked = false

function setLocked(locked: boolean): void {
  if (locked === isLocked) return
  isLocked = locked
  if (locked) lockScroll()
  else unlockScroll()
}

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
      setLocked(true)
      await nextTick()
      const firstInput = dialog.value?.querySelector<HTMLElement>('input, select, textarea')
      ;(firstInput ?? focusableElements()[0])?.focus()
    } else {
      setLocked(false)
      previousFocus?.focus()
    }
  },
  { immediate: true },
)

onBeforeUnmount(() => setLocked(false))
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
  overflow-y: auto;
  overscroll-behavior: contain;
  background: rgb(6 21 15 / 0.55);
  backdrop-filter: blur(3px);
}

.dialog {
  width: 100%;
  min-width: 0;
  max-height: calc(100dvh - 2rem);
  overflow-x: hidden;
  overflow-y: auto;
  color: var(--color-text);
  border: 1px solid var(--color-border);
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
  min-width: 0;
  font-size: var(--font-size-xl);
  overflow-wrap: anywhere;
}

.close {
  display: grid;
  place-items: center;
  width: 36px;
  height: 36px;
  border: none;
  flex-shrink: 0;
  border-radius: 50%;
  background: var(--color-surface-muted);
  color: var(--color-text);
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

@media (max-width: 520px) {
  .backdrop {
    padding: var(--space-3);
  }

  .dialog-header {
    padding: var(--space-5) var(--space-5) 0;
  }

  .dialog-body {
    padding: var(--space-4) var(--space-5);
  }

  .dialog-footer {
    padding: 0 var(--space-5) var(--space-5);
  }

  .dialog-footer > :deep(*) {
    flex: 1 1 auto;
  }
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
