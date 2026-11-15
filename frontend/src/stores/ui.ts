import { defineStore } from 'pinia'
import { ref, watch } from 'vue'

export type ToastTone = 'success' | 'danger' | 'info'

export interface Toast {
  id: number
  tone: ToastTone
  message: string
}

type Theme = 'system' | 'light' | 'dark'

function readPreference<T extends string>(key: string, fallback: T): T {
  try {
    return (localStorage.getItem(key) as T | null) ?? fallback
  } catch {
    return fallback
  }
}

function writePreference(key: string, value: string): void {
  try {
    localStorage.setItem(key, value)
  } catch {
    return
  }
}

export const useUiStore = defineStore('ui', () => {
  const toasts = ref<Toast[]>([])
  const hideBalances = ref(readPreference<string>('bs.hideBalances', 'false') === 'true')
  const theme = ref<Theme>(readPreference<Theme>('bs.theme', 'system'))
  const sidebarOpen = ref(false)
  let nextId = 1

  function notify(message: string, tone: ToastTone = 'success'): void {
    const id = nextId++
    toasts.value.push({ id, tone, message })
    setTimeout(() => dismiss(id), tone === 'danger' ? 7000 : 4500)
  }

  function dismiss(id: number): void {
    toasts.value = toasts.value.filter((toast) => toast.id !== id)
  }

  function toggleBalances(): void {
    hideBalances.value = !hideBalances.value
  }

  function applyTheme(): void {
    const root = document.documentElement
    if (theme.value === 'system') root.removeAttribute('data-theme')
    else root.setAttribute('data-theme', theme.value)
  }

  watch(hideBalances, (value) => writePreference('bs.hideBalances', String(value)))
  watch(
    theme,
    (value) => {
      writePreference('bs.theme', value)
      applyTheme()
    },
    { immediate: true },
  )

  return { toasts, hideBalances, theme, sidebarOpen, notify, dismiss, toggleBalances }
})
