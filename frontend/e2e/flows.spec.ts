import { expect, test } from '@playwright/test'
import { expectDialogCentered, openRoute, signIn } from './support/app'
import { loadSeed } from './support/seed'

const seed = loadSeed()

test('kartice racuna popunjavaju celu sirinu za jedan i dva racuna', async ({ page }) => {
  for (const [account, expected] of [
    [seed.customers.one, 1],
    [seed.customers.two, 2],
  ] as const) {
    await signIn(page, account)
    await openRoute(page, '/pocetna')
    const cards = page.locator('.account-card')
    await expect(cards).toHaveCount(expected)
    const grid = await cards.first().locator('xpath=..').boundingBox()
    const boxes = await cards.evaluateAll((elements) => elements.map((el) => el.getBoundingClientRect().width))
    const gap = expected > 1 ? 16 : 0
    const used = boxes.reduce((sum, width) => sum + width, 0) + gap * (expected - 1)
    const isPhone = (page.viewportSize()?.width ?? 1280) <= 720
    if (!isPhone) expect(Math.abs(used - grid!.width)).toBeLessThanOrEqual(8)
    expect(new Set(boxes.map((width) => Math.round(width))).size).toBe(1)
    await page.locator('.avatar-button').click()
    await page.getByRole('menuitem', { name: /Odjavi se/ }).click()
    await expect(page).toHaveURL(/\/prijava/)
  }
})

test('meni na inicijalima nema Postavke, a odjava postoji samo u njemu', async ({ page }) => {
  await signIn(page, seed.customers.three)
  await page.locator('.avatar-button').click()
  const menu = page.getByRole('menu')
  await expect(menu.getByRole('menuitem', { name: 'Postavke' })).toHaveCount(0)
  await expect(menu.getByRole('menuitem', { name: /Odjavi se/ })).toBeVisible()
  await page.keyboard.press('Escape')
  await expect(menu).toHaveCount(0)
  await openRoute(page, '/postavke')
  await expect(page.getByRole('button', { name: /Odjavi se/ })).toHaveCount(0)
})

test('povratak sa politike privatnosti vraca na stranicu Privatnost', async ({ page }) => {
  await signIn(page, seed.customers.three)
  await openRoute(page, '/privatnost')
  await page.getByRole('link', { name: 'Politika privatnosti' }).click()
  await expect(page).toHaveURL(/\/javno\/dokumenti\/privatnost/)
  await page.getByRole('button', { name: 'Nazad' }).click()
  await expect(page).toHaveURL(/\/privatnost$/)
})

test('obavestenja i privatnost imaju odvojena dugmad za cuvanje', async ({ page }) => {
  await signIn(page, seed.customers.three)
  await openRoute(page, '/privatnost')
  const saveNotifications = page.getByRole('button', { name: 'Sačuvaj obaveštenja' })
  const savePrivacy = page.getByRole('button', { name: 'Sačuvaj podešavanja privatnosti' })
  await expect(saveNotifications).toBeDisabled()
  await expect(savePrivacy).toBeDisabled()
  await page.getByRole('switch').first().click()
  await expect(saveNotifications).toBeEnabled()
  await expect(savePrivacy).toBeDisabled()
  await saveNotifications.click()
  await expect(saveNotifications).toBeDisabled()
})

test('blokiranje racuna trazi potvrdu, odustajanje ne menja status', async ({ page }) => {
  await signIn(page, seed.admin)
  await openRoute(page, '/admin/racuni')
  await page.getByLabel('Pretraga').fill(seed.customers.two.email)
  await page.getByRole('button', { name: 'Traži' }).click()
  const row = page.locator('tbody tr').first()
  await row.getByRole('button', { name: 'Blokiraj' }).click()
  await expectDialogCentered(page)
  await expect(page.getByRole('dialog')).toContainText('Da li zaista želite da blokirate')
  await page.getByRole('dialog').getByRole('button', { name: 'Odustani' }).click()
  await expect(row).toContainText('Aktivan')
  await row.getByRole('button', { name: 'Blokiraj' }).click()
  await page.getByRole('dialog').getByRole('button', { name: 'Blokiraj račun' }).click()
  await expect(row).toContainText('Blokiran')
  await row.getByRole('button', { name: 'Aktiviraj' }).click()
  await page.getByRole('dialog').getByRole('button', { name: 'Aktiviraj račun' }).click()
  await expect(row).toContainText('Aktivan')
})

test('modal za izmenu korisnika je centriran', async ({ page }) => {
  await signIn(page, seed.admin)
  await openRoute(page, '/admin/korisnici')
  await page.locator('tbody tr').first().getByRole('button', { name: 'Izmeni' }).click()
  await expectDialogCentered(page)
})
