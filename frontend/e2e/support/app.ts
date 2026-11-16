import { expect, type Page } from '@playwright/test'
import type { SeedAccount } from './seed'

export function collectConsoleErrors(page: Page): string[] {
  const errors: string[] = []
  page.on('console', (message) => {
    if (message.type() === 'error') errors.push(message.text())
  })
  page.on('pageerror', (error) => errors.push(error.message))
  return errors
}

export async function signIn(page: Page, account: SeedAccount): Promise<void> {
  await page.goto('/prijava')
  await page.locator('input[type=email]').fill(account.email)
  await page.locator('input[type=password]').fill(account.password)
  await page.locator('form button[type=submit]').click()
  await expect(page).not.toHaveURL(/\/prijava/)
}

export async function openRoute(page: Page, route: string): Promise<void> {
  await page.goto(route)
  await page.waitForLoadState('networkidle')
  await expect(page.locator('main, .public-content').first()).toBeVisible()
}

export interface LayoutIssues {
  overflow: string | null
  clippedContent: string[]
  cardWidths: string[]
  tableAlignment: string[]
  wrappedNavItems: string[]
}

export async function inspectLayout(page: Page): Promise<LayoutIssues> {
  return page.evaluate(() => {
    const vw = document.documentElement.clientWidth
    const overflow = document.documentElement.scrollWidth > vw ? `stranica je siroka ${document.documentElement.scrollWidth}px na ekranu od ${vw}px` : null
    const main = document.querySelector('main') ?? document.body
    const container = main.querySelector('.page') ?? main
    const cr = container.getBoundingClientRect()
    const clippedContent: string[] = []
    for (const el of container.querySelectorAll<HTMLElement>('*')) {
      if (el.closest('.table-scroll, .card-strip, .scroll, .tabs, [role=dialog]')) continue
      const r = el.getBoundingClientRect()
      if (r.width > 0 && r.height > 0 && (r.right > cr.right + 1 || r.left < cr.left - 1))
        clippedContent.push(`${el.tagName.toLowerCase()}.${String(el.className).split(' ')[0]} izlazi do ${Math.round(r.right)}px, a stranica je do ${Math.round(cr.right)}px`)
    }
    const cardWidths: string[] = []
    if (vw <= 720) {
      for (const el of container.querySelectorAll<HTMLElement>('.card, section.hero, .account-card')) {
        if (el.parentElement?.closest('.card, .hero, .account-card, .card-strip, [role=dialog]')) continue
        const r = el.getBoundingClientRect()
        if (r.width > 0 && (Math.abs(r.left - cr.left) > 1.5 || Math.abs(r.right - cr.right) > 1.5))
          cardWidths.push(`${el.className.split(' ')[0]} ${Math.round(r.width)}px umesto ${Math.round(cr.width)}px`)
      }
    }
    const tableAlignment: string[] = []
    for (const table of document.querySelectorAll('table.data-table')) {
      const heads = [...table.querySelectorAll('thead th')]
      for (const row of [...table.querySelectorAll('tbody tr')].slice(0, 5)) {
        ;[...row.children].forEach((cell, index) => {
          const head = heads[index]
          if (!head) return
          const rects: DOMRect[] = []
          for (const el of cell.querySelectorAll<HTMLElement>('.app-button, .badge, img, input, select'))
            if (!el.parentElement?.closest('.app-button, .badge')) rects.push(el.getBoundingClientRect())
          const walker = document.createTreeWalker(cell, NodeFilter.SHOW_TEXT)
          for (let node = walker.nextNode(); node; node = walker.nextNode()) {
            if (!node.textContent?.trim() || node.parentElement?.closest('.app-button, .badge')) continue
            const range = document.createRange()
            range.selectNodeContents(node)
            rects.push(...[...range.getClientRects()].filter((r) => r.width > 0))
          }
          if (!rects.length) return
          const center = (Math.min(...rects.map((r) => r.left)) + Math.max(...rects.map((r) => r.right))) / 2
          const cellRect = cell.getBoundingClientRect()
          if (Math.abs(center - (cellRect.left + cellRect.right) / 2) > 3) tableAlignment.push(`kolona "${head.textContent?.trim() || 'radnje'}"`)
        })
      }
    }
    const wrappedNavItems: string[] = []
    for (const nav of document.querySelectorAll('nav, [role=tablist], .segmented, .filter-chips')) {
      if (nav.closest('.sidebar, .mobile-nav')) continue
      for (const item of nav.querySelectorAll(':scope > a, :scope > button')) {
        const walker = document.createTreeWalker(item, NodeFilter.SHOW_TEXT)
        const tops = new Set<number>()
        for (let node = walker.nextNode(); node; node = walker.nextNode()) {
          if (!node.textContent?.trim()) continue
          const range = document.createRange()
          range.selectNodeContents(node)
          for (const r of range.getClientRects()) if (r.width > 0) tops.add(Math.round(r.top))
        }
        if (tops.size > 1) wrappedNavItems.push(item.textContent?.trim() ?? '')
      }
    }
    return { overflow, clippedContent: [...new Set(clippedContent)].slice(0, 5), cardWidths: [...new Set(cardWidths)], tableAlignment: [...new Set(tableAlignment)], wrappedNavItems }
  })
}

export async function expectDialogCentered(page: Page): Promise<void> {
  const dialog = page.getByRole('dialog')
  await expect(dialog).toBeVisible()
  const box = await dialog.boundingBox()
  const width = await page.evaluate(() => window.innerWidth)
  expect(box).not.toBeNull()
  expect(Math.abs(box!.x - (width - box!.x - box!.width))).toBeLessThanOrEqual(2)
}
