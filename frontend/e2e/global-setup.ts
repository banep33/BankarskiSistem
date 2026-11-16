import type { FullConfig } from '@playwright/test'
import { saveSeed, type SeedAccount, type SeedData } from './support/seed'

const PASSWORD = 'E2eLozinka123'

interface Session {
  token: string
}

async function api<T>(baseUrl: string, method: string, path: string, body?: unknown, token?: string, headers: Record<string, string> = {}): Promise<T> {
  const response = await fetch(`${baseUrl}/api/v1${path}`, {
    method,
    headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}), ...headers },
    body: body === undefined ? undefined : JSON.stringify(body),
  })
  const text = await response.text()
  if (!response.ok) throw new Error(`${method} ${path} -> ${response.status}: ${text}`)
  return (text ? JSON.parse(text) : undefined) as T
}

async function login(baseUrl: string, account: SeedAccount): Promise<string> {
  const session = await api<Session & { twoFactorSetupRequired?: boolean }>(baseUrl, 'POST', '/auth/login', account)
  const pending = await api<{ id: number }[]>(baseUrl, 'GET', '/me/privacy/pending-documents', undefined, session.token)
  if (pending.length) await api(baseUrl, 'POST', '/me/privacy/consents', { documentIds: pending.map((doc) => doc.id) }, session.token)
  return session.token
}

async function createUser(baseUrl: string, token: string, firstName: string, email: string, role: 'Korisnik' | 'Bankar'): Promise<{ id: number }> {
  return api(baseUrl, 'POST', '/users', { firstName, lastName: 'Test', email, password: PASSWORD, confirmPassword: PASSWORD, role }, token)
}

async function openAccounts(baseUrl: string, banker: string, userId: number, deposits: [string, number][]): Promise<void> {
  await api(baseUrl, 'POST', `/users/${userId}/kyc`, { status: 'Verifikovan', riskRating: 'Nizak' }, banker)
  for (const [currency, amount] of deposits) {
    const account = await api<{ id: number }>(baseUrl, 'POST', '/accounts', { userId, currency }, banker)
    if (amount > 0)
      await api(baseUrl, 'POST', `/accounts/${account.id}/deposits`, { amount, description: 'Uplata za E2E test' }, banker, { 'Idempotency-Key': crypto.randomUUID() })
  }
}

export default async function globalSetup(config: FullConfig): Promise<void> {
  const baseUrl = String(config.projects[0]?.use.baseURL ?? 'http://localhost:5173')
  const suffix = Date.now().toString(36)
  const admin: SeedAccount = {
    email: process.env.E2E_ADMIN_EMAIL ?? 'ui.admin@test.local',
    password: process.env.E2E_ADMIN_PASSWORD ?? 'UiTest123!',
  }
  const adminToken = await login(baseUrl, admin)

  const banker: SeedAccount = { email: `e2e.bankar.${suffix}@test.local`, password: PASSWORD }
  await createUser(baseUrl, adminToken, 'Bankar', banker.email, 'Bankar')
  const bankerToken = await login(baseUrl, banker)

  const customers = {
    one: { email: `e2e.jedan.${suffix}@test.local`, password: PASSWORD },
    two: { email: `e2e.dva.${suffix}@test.local`, password: PASSWORD },
    three: { email: `e2e.tri.${suffix}@test.local`, password: PASSWORD },
  }
  const plans: [SeedAccount, string, [string, number][]][] = [
    [customers.one, 'Jedan', [['RSD', 30_000]]],
    [customers.two, 'Dva', [['RSD', 25_000], ['EUR', 500]]],
    [customers.three, 'Tri', [['RSD', 9_999_999.99], ['EUR', 1_250_000.5], ['USD', 3_400]]],
  ]
  for (const [account, name, deposits] of plans) {
    const user = await createUser(baseUrl, bankerToken, name, account.email, 'Korisnik')
    await openAccounts(baseUrl, bankerToken, user.id, deposits)
    await login(baseUrl, account)
  }

  const data: SeedData = { admin, banker, customers }
  saveSeed(data)
}
