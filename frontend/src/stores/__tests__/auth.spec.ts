import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import type { LoginResponse } from '@/types/api'

const refresh = vi.fn()

vi.mock('@/api/bank', () => ({
  authApi: { refresh: () => refresh(), login: vi.fn(), completeTwoFactor: vi.fn(), logout: vi.fn() },
}))

function memoryStorage(): Storage {
  const data = new Map<string, string>()
  return {
    get length() {
      return data.size
    },
    clear: () => data.clear(),
    getItem: (key) => data.get(key) ?? null,
    key: (index) => [...data.keys()][index] ?? null,
    removeItem: (key) => void data.delete(key),
    setItem: (key, value) => void data.set(key, value),
  }
}

function loginResponse(overrides: Partial<LoginResponse> = {}): LoginResponse {
  return {
    id: 7,
    token: 'access-token',
    email: 'bankar@test.local',
    firstName: 'Bojan',
    lastName: 'Bankar',
    role: 'Bankar',
    expiresAt: '2026-11-15T12:00:00Z',
    pendingDocuments: [],
    mfaVerified: false,
    twoFactorSetupRequired: true,
    ...overrides,
  }
}

describe('auth store', () => {
  beforeEach(() => {
    vi.stubGlobal('localStorage', memoryStorage())
    vi.stubGlobal('sessionStorage', memoryStorage())
    refresh.mockReset()
    setActivePinia(createPinia())
  })

  it('bez oznake prijave ne poziva obnovu sesije (nema greške 401 u konzoli)', async () => {
    const { useAuthStore } = await import('@/stores/auth')
    await useAuthStore().initialize()
    expect(refresh).not.toHaveBeenCalled()
  })

  it('sa oznakom prijave obnavlja sesiju, a token ne čuva u pregledaču', async () => {
    localStorage.setItem('bs.signedIn', '1')
    refresh.mockResolvedValue(loginResponse({ mfaVerified: true, twoFactorSetupRequired: false }))
    const { useAuthStore } = await import('@/stores/auth')
    const auth = useAuthStore()

    await auth.initialize()

    expect(refresh).toHaveBeenCalledOnce()
    expect(auth.isAuthenticated).toBe(true)
    for (let index = 0; index < localStorage.length; index++) {
      const key = localStorage.key(index)!
      expect(localStorage.getItem(key)).not.toContain('access-token')
    }
  })

  it('neuspešna obnova briše oznaku prijave', async () => {
    localStorage.setItem('bs.signedIn', '1')
    refresh.mockRejectedValue(new Error('401'))
    const { useAuthStore } = await import('@/stores/auth')

    await useAuthStore().initialize()

    expect(localStorage.getItem('bs.signedIn')).toBeNull()
  })

  it('zaposleni bez druge provere vodi se na podešavanje dvostepene prijave', async () => {
    const { useAuthStore } = await import('@/stores/auth')
    const auth = useAuthStore()

    auth.applyLogin(loginResponse())

    expect(auth.twoFactorSetupRequired).toBe(true)
    expect(auth.homeRoute).toBe('/bezbednost')
  })

  it('posle druge provere zaposleni dobija svoju početnu stranicu', async () => {
    const { useAuthStore } = await import('@/stores/auth')
    const auth = useAuthStore()

    auth.applyLogin(loginResponse({ mfaVerified: true, twoFactorSetupRequired: false }))

    expect(auth.homeRoute).toBe('/bankar')
  })

  it('odjava uklanja token i oznaku prijave', async () => {
    const { useAuthStore } = await import('@/stores/auth')
    const auth = useAuthStore()
    auth.applyLogin(loginResponse({ mfaVerified: true, twoFactorSetupRequired: false }))

    await auth.logout(false)

    expect(auth.isAuthenticated).toBe(false)
    expect(localStorage.getItem('bs.signedIn')).toBeNull()
  })
})
