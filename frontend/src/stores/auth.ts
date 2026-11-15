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
  mfaVerified: boolean
  twoFactorSetupRequired: boolean
}

export type LoginStep = { kind: 'done' } | { kind: 'two-factor'; challengeToken: string; expiresAt: string }

const LEGACY_STORAGE_KEY = 'bs.session'
const SESSION_HINT_KEY = 'bs.signedIn'

function setSessionHint(active: boolean): void {
  try {
    if (active) localStorage.setItem(SESSION_HINT_KEY, '1')
    else localStorage.removeItem(SESSION_HINT_KEY)
  } catch {
    return
  }
}

function hasSessionHint(): boolean {
  try {
    return localStorage.getItem(SESSION_HINT_KEY) === '1'
  } catch {
    return true
  }
}

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
  const twoFactorSetupRequired = computed(() => session.value?.twoFactorSetupRequired === true)
  const homeRoute = computed(() => (twoFactorSetupRequired.value ? '/bezbednost' : isCustomer.value ? '/pocetna' : isBanker.value ? '/bankar' : '/admin'))

  function applyLogin(response: LoginResponse): void {
    session.value = {
      token: response.token,
      expiresAt: response.expiresAt,
      userId: response.id,
      email: response.email,
      firstName: response.firstName,
      lastName: response.lastName,
      role: response.role,
      mfaVerified: response.mfaVerified,
      twoFactorSetupRequired: response.twoFactorSetupRequired,
    }
    pendingDocuments.value = response.pendingDocuments
    setAuthToken(response.token)
    setSessionHint(true)
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
      setSessionHint(false)
      return false
    }
  }

  function initialize(): Promise<void> {
    initialization ??= hasSessionHint() ? refresh().then(() => undefined) : Promise.resolve()
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
    setSessionHint(false)
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
    twoFactorSetupRequired,
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
