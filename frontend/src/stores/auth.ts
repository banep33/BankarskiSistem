import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { authApi } from '@/api/bank'
import { setAuthToken, TOKEN_STORAGE_KEY } from '@/api/http'
import type { LegalDocumentSummary, LoginResponse, UserRole } from '@/types/api'

interface StoredSession {
  token: string
  expiresAt: string
  userId: number
  email: string
  firstName: string
  lastName: string
  role: UserRole
}

function readStoredSession(): StoredSession | null {
  try {
    const raw = sessionStorage.getItem(TOKEN_STORAGE_KEY)
    if (!raw) return null
    const session = JSON.parse(raw) as StoredSession
    return new Date(session.expiresAt) > new Date() ? session : null
  } catch {
    return null
  }
}

export const useAuthStore = defineStore('auth', () => {
  const session = ref<StoredSession | null>(readStoredSession())
  const pendingDocuments = ref<LegalDocumentSummary[]>([])
  setAuthToken(session.value?.token ?? null)

  const isAuthenticated = computed(() => session.value !== null)
  const role = computed(() => session.value?.role ?? null)
  const isCustomer = computed(() => role.value === 'Korisnik')
  const isBanker = computed(() => role.value === 'Bankar')
  const isAdministrator = computed(() => role.value === 'Administrator')
  const isStaff = computed(() => isBanker.value || isAdministrator.value)
  const fullName = computed(() => (session.value ? `${session.value.firstName} ${session.value.lastName}` : ''))
  const homeRoute = computed(() => (isCustomer.value ? '/pocetna' : isBanker.value ? '/bankar' : '/admin'))

  function applyLogin(response: LoginResponse): void {
    session.value = {
      token: response.token,
      expiresAt: response.expiresAt,
      userId: response.id,
      email: response.email,
      firstName: response.firstName,
      lastName: response.lastName,
      role: response.role,
    }
    pendingDocuments.value = response.pendingDocuments
    persist()
  }

  async function login(email: string, password: string): Promise<void> {
    applyLogin(await authApi.login(email, password))
  }

  function updateName(firstName: string, lastName: string): void {
    if (!session.value) return
    session.value = { ...session.value, firstName, lastName }
    persist()
  }

  async function logout(callServer = true): Promise<void> {
    if (callServer && session.value) {
      try {
        await authApi.logout()
      } catch {
        session.value = null
      }
    }
    clear()
  }

  function clear(): void {
    session.value = null
    pendingDocuments.value = []
    setAuthToken(null)
    try {
      sessionStorage.removeItem(TOKEN_STORAGE_KEY)
    } catch {
      session.value = null
    }
  }

  function persist(): void {
    setAuthToken(session.value?.token ?? null)
    try {
      if (session.value) sessionStorage.setItem(TOKEN_STORAGE_KEY, JSON.stringify(session.value))
    } catch {
      return
    }
  }

  return {
    session,
    pendingDocuments,
    isAuthenticated,
    role,
    isCustomer,
    isBanker,
    isAdministrator,
    isStaff,
    fullName,
    homeRoute,
    login,
    applyLogin,
    updateName,
    logout,
    clear,
  }
})
