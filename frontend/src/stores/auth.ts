import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { authApi } from '@/api/bank'
import { setAuthToken } from '@/api/http'
import type { LegalDocumentSummary, LoginResponse, TwoFactorChallengeResponse, UserRole } from '@/types/api'

interface ActiveSession {
  token: string
  expiresAt: string
  userId: number
  email: string
  firstName: string
  lastName: string
  role: UserRole
}

export type LoginStep = { kind: 'done' } | { kind: 'two-factor'; challengeToken: string; expiresAt: string }

const LEGACY_STORAGE_KEY = 'bs.session'

function isChallenge(response: LoginResponse | TwoFactorChallengeResponse): response is TwoFactorChallengeResponse {
  return 'requiresTwoFactor' in response && response.requiresTwoFactor === true
}

export const useAuthStore = defineStore('auth', () => {
  const session = ref<ActiveSession | null>(null)
  const pendingDocuments = ref<LegalDocumentSummary[]>([])
  let initialization: Promise<void> | null = null

  try {
    sessionStorage.removeItem(LEGACY_STORAGE_KEY)
  } catch {
    session.value = null
  }

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
    setAuthToken(response.token)
  }

  async function login(email: string, password: string): Promise<LoginStep> {
    const response = await authApi.login(email, password)
    if (isChallenge(response)) return { kind: 'two-factor', challengeToken: response.challengeToken, expiresAt: response.expiresAt }
    applyLogin(response)
    return { kind: 'done' }
  }

  async function completeTwoFactor(challengeToken: string, code: string): Promise<void> {
    applyLogin(await authApi.completeTwoFactor(challengeToken, code))
  }

  async function refresh(): Promise<boolean> {
    try {
      applyLogin(await authApi.refresh())
      return true
    } catch {
      return false
    }
  }

  function initialize(): Promise<void> {
    initialization ??= refresh().then(() => undefined)
    return initialization
  }

  function updateName(firstName: string, lastName: string): void {
    if (session.value) session.value = { ...session.value, firstName, lastName }
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
    completeTwoFactor,
    refresh,
    initialize,
    applyLogin,
    updateName,
    logout,
    clear,
  }
})
