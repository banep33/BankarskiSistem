import { defineStore } from 'pinia'
import { ref } from 'vue'
import { securityApi } from '@/api/bank'
import { ApiError } from '@/api/http'

export const useReauthStore = defineStore('reauth', () => {
  const isOpen = ref(false)
  const reason = ref('')
  let resolver: ((confirmed: boolean) => void) | null = null

  function request(message: string): Promise<boolean> {
    reason.value = message
    isOpen.value = true
    return new Promise((resolve) => {
      resolver = resolve
    })
  }

  async function confirm(password: string): Promise<void> {
    await securityApi.reauthenticate(password)
    close(true)
  }

  function close(confirmed: boolean): void {
    isOpen.value = false
    resolver?.(confirmed)
    resolver = null
  }

  async function runWithReauth<T>(action: () => Promise<T>, message: string): Promise<T | null> {
    try {
      return await action()
    } catch (error) {
      if (error instanceof ApiError && error.is('reauthentication-required')) {
        const confirmed = await request(error.message || message)
        return confirmed ? await action() : null
      }
      throw error
    }
  }

  return { isOpen, reason, request, confirm, close, runWithReauth }
})
