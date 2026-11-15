import { beforeEach, describe, expect, it, vi } from 'vitest'

function installDom(viewportWidth: number, clientWidth: number) {
  const body = { style: { overflow: '', paddingRight: '' } }
  vi.stubGlobal('window', { innerWidth: viewportWidth })
  vi.stubGlobal('document', { body, documentElement: { clientWidth } })
  return body
}

describe('zaključavanje skrolovanja za modale', () => {
  beforeEach(() => {
    vi.resetModules()
    vi.unstubAllGlobals()
  })

  it('nadoknađuje širinu trake za skrolovanje da se sadržaj ne pomeri', async () => {
    const body = installDom(1440, 1425)
    const { lockScroll, unlockScroll } = await import('@/composables/useScrollLock')

    lockScroll()
    expect(body.style.overflow).toBe('hidden')
    expect(body.style.paddingRight).toBe('15px')

    unlockScroll()
    expect(body.style.overflow).toBe('')
    expect(body.style.paddingRight).toBe('')
  })

  it('modal preko modala ne otključava stranicu prerano', async () => {
    const body = installDom(1440, 1425)
    const { lockScroll, unlockScroll } = await import('@/composables/useScrollLock')

    lockScroll()
    lockScroll()
    unlockScroll()
    expect(body.style.overflow).toBe('hidden')
    unlockScroll()
    expect(body.style.overflow).toBe('')
  })

  it('višak otključavanja ne kvari stanje', async () => {
    const body = installDom(375, 375)
    const { lockScroll, unlockScroll } = await import('@/composables/useScrollLock')

    unlockScroll()
    lockScroll()
    expect(body.style.paddingRight).toBe('')
    expect(body.style.overflow).toBe('hidden')
  })
})
