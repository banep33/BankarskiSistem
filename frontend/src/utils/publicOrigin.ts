const STORAGE_KEY = 'bs.publicOrigin'
const PUBLIC_PREFIX = '/javno'

let memoryOrigin: string | null = null

export function isPublicPath(path: string): boolean {
  return path === PUBLIC_PREFIX || path.startsWith(`${PUBLIC_PREFIX}/`)
}

export function rememberPublicOrigin(fromPath: string, toPath: string): void {
  if (!isPublicPath(toPath) || isPublicPath(fromPath) || !fromPath.startsWith('/')) return
  memoryOrigin = fromPath
  try {
    sessionStorage.setItem(STORAGE_KEY, fromPath)
  } catch {
    return
  }
}

export function publicOrigin(fallback: string): string {
  if (memoryOrigin) return memoryOrigin
  try {
    const stored = sessionStorage.getItem(STORAGE_KEY)
    if (stored && stored.startsWith('/') && !isPublicPath(stored)) return stored
  } catch {
    return fallback
  }
  return fallback
}
