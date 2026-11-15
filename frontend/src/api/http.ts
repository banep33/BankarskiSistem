import axios, { AxiosError, type AxiosRequestConfig } from 'axios'
import type { ApiProblem } from '@/types/api'

export const TOKEN_STORAGE_KEY = 'bs.session'

export class ApiError extends Error {
  readonly status: number
  readonly problemType: string
  readonly errors: string[]
  readonly traceId: string | null

  constructor(status: number, message: string, problemType: string, errors: string[], traceId: string | null) {
    super(message)
    this.status = status
    this.problemType = problemType
    this.errors = errors
    this.traceId = traceId
  }

  is(problem: string): boolean {
    return this.problemType.endsWith(`/${problem}`)
  }
}

type UnauthorizedHandler = () => void

let unauthorizedHandler: UnauthorizedHandler | null = null
let currentToken: string | null = null

export function setAuthToken(token: string | null): void {
  currentToken = token
}

export function onUnauthorized(handler: UnauthorizedHandler): void {
  unauthorizedHandler = handler
}

const client = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL || '/api/v1',
  timeout: 30_000,
  headers: { 'Content-Type': 'application/json' },
})

client.interceptors.request.use((config) => {
  if (currentToken) {
    config.headers.Authorization = `Bearer ${currentToken}`
  }
  return config
})

client.interceptors.response.use(
  (response) => response,
  (error: AxiosError<ApiProblem>) => {
    const status = error.response?.status ?? 0
    const problem = error.response?.data
    const isLogin = error.config?.url?.includes('/auth/login') ?? false

    if (status === 401 && !isLogin && unauthorizedHandler) {
      unauthorizedHandler()
    }

    if (status === 0) {
      return Promise.reject(
        new ApiError(0, 'Server trenutno nije dostupan. Proverite vezu i pokušajte ponovo.', 'network', [], null),
      )
    }

    const message =
      problem?.detail ||
      (status === 403
        ? 'Nemate dozvolu za ovu radnju.'
        : status === 404
          ? 'Traženi podatak nije pronađen.'
          : 'Došlo je do greške. Pokušajte ponovo.')

    return Promise.reject(
      new ApiError(status, message, problem?.type ?? `status-${status}`, problem?.errors ?? [], problem?.traceId ?? null),
    )
  },
)

export function newIdempotencyKey(): string {
  return crypto.randomUUID().replaceAll('-', '')
}

export async function get<T>(url: string, params?: Record<string, unknown>): Promise<T> {
  const response = await client.get<T>(url, { params: cleanParams(params) })
  return response.data
}

export async function post<T>(url: string, body?: unknown, config?: AxiosRequestConfig): Promise<T> {
  const response = await client.post<T>(url, body, config)
  return response.data
}

export async function put<T>(url: string, body?: unknown): Promise<T> {
  const response = await client.put<T>(url, body)
  return response.data
}

export async function del(url: string): Promise<void> {
  await client.delete(url)
}

export function withIdempotency(key: string): AxiosRequestConfig {
  return { headers: { 'Idempotency-Key': key } }
}

function cleanParams(params?: Record<string, unknown>): Record<string, unknown> | undefined {
  if (!params) return undefined
  return Object.fromEntries(Object.entries(params).filter(([, value]) => value !== undefined && value !== null && value !== ''))
}
