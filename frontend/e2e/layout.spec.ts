import { expect, test } from '@playwright/test'
import { collectConsoleErrors, inspectLayout, openRoute, signIn } from './support/app'
import { loadSeed, type SeedAccount } from './support/seed'

const seed = loadSeed()

const roles: { name: string; account: () => SeedAccount; routes: string[] }[] = [
  {
    name: 'klijent sa tri racuna',
    account: () => seed.customers.three,
    routes: ['/pocetna', '/racuni', '/placanja/novo', '/placanja', '/menjacnica', '/trajni-nalozi', '/trajni-nalozi/novi', '/transakcije', '/kartice', '/stednja', '/potrosnja', '/primaoci', '/prigovori', '/privatnost', '/pomoc', '/obavestenja', '/postavke', '/bezbednost', '/javno/dokumenti/privatnost'],
  },
  { name: 'klijent sa jednim racunom', account: () => seed.customers.one, routes: ['/pocetna', '/racuni'] },
  { name: 'klijent sa dva racuna', account: () => seed.customers.two, routes: ['/pocetna', '/racuni'] },
  {
    name: 'bankar',
    account: () => seed.banker,
    routes: ['/bankar', '/bankar/klijenti', '/bankar/transakcije', '/bankar/prigovori', '/bankar/storniranja', '/bankar/ocene', '/postavke', '/bezbednost'],
  },
  {
    name: 'administrator',
    account: () => seed.admin,
    routes: ['/admin', '/admin/korisnici', '/admin/racuni', '/admin/kursna-lista', '/admin/knjiga', '/admin/aml', '/admin/zahtevi-lozinka', '/admin/zahtevi-privatnost', '/admin/audit', '/admin/faq'],
  },
]

for (const role of roles) {
  test(`raspored bez gresaka: ${role.name}`, async ({ page }) => {
    const errors = collectConsoleErrors(page)
    await signIn(page, role.account())
    for (const route of role.routes) {
      await openRoute(page, route)
      const issues = await inspectLayout(page)
      expect.soft(issues.overflow, `${route}: bocno pomeranje`).toBeNull()
      expect.soft(issues.clippedContent, `${route}: sadrzaj izlazi iz okvira stranice`).toEqual([])
      expect.soft(issues.cardWidths, `${route}: kartice nisu iste sirine kao stranica`).toEqual([])
      expect.soft(issues.tableAlignment, `${route}: sadrzaj kolone nije centriran ispod naslova`).toEqual([])
      expect.soft(issues.wrappedNavItems, `${route}: stavka menija se lomi u dva reda`).toEqual([])
    }
    expect(errors, 'greske u konzoli').toEqual([])
  })
}
