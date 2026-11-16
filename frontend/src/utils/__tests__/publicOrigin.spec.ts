import { beforeEach, describe, expect, it, vi } from 'vitest'

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

describe('povratak iz javnog dela', () => {
  beforeEach(() => {
    vi.resetModules()
    vi.stubGlobal('sessionStorage', memoryStorage())
  })

  it('bez zapamcene stranice vraca podrazumevanu', async () => {
    const { publicOrigin } = await import('@/utils/publicOrigin')
    expect(publicOrigin('/prijava')).toBe('/prijava')
  })

  it('pamti stranicu aplikacije sa koje se ulazi u javni deo', async () => {
    const { publicOrigin, rememberPublicOrigin } = await import('@/utils/publicOrigin')
    rememberPublicOrigin('/privatnost', '/javno/dokumenti/privatnost')
    expect(publicOrigin('/pocetna')).toBe('/privatnost')
  })

  it('promena taba unutar javnog dela ne menja polaznu stranicu', async () => {
    const { publicOrigin, rememberPublicOrigin } = await import('@/utils/publicOrigin')
    rememberPublicOrigin('/privatnost', '/javno/dokumenti/privatnost')
    rememberPublicOrigin('/javno/dokumenti/privatnost', '/javno/dokumenti/uslovi')
    rememberPublicOrigin('/javno/dokumenti/uslovi', '/javno/pomoc')
    expect(publicOrigin('/pocetna')).toBe('/privatnost')
  })

  it('prelazak izmedju stranica aplikacije ne pamti nista', async () => {
    const { publicOrigin, rememberPublicOrigin } = await import('@/utils/publicOrigin')
    rememberPublicOrigin('/pocetna', '/racuni')
    expect(publicOrigin('/prijava')).toBe('/prijava')
  })

  it('posle osvezavanja stranice cita polaznu stranicu iz sesije pregledaca', async () => {
    const first = await import('@/utils/publicOrigin')
    first.rememberPublicOrigin('/prijava', '/javno/pomoc')
    vi.resetModules()
    const reloaded = await import('@/utils/publicOrigin')
    expect(reloaded.publicOrigin('/pocetna')).toBe('/prijava')
  })
})
