import type { UserRole } from '@/types/api'

export interface NavItem {
  to: string
  label: string
  icon: string
}

export interface NavSection {
  title?: string
  items: NavItem[]
}

const customerNavigation: NavSection[] = [
  {
    items: [
      { to: '/pocetna', label: 'Početna', icon: 'home' },
      { to: '/racuni', label: 'Računi', icon: 'wallet' },
      { to: '/placanja/novo', label: 'Novo plaćanje', icon: 'send' },
      { to: '/placanja', label: 'Nalozi', icon: 'file' },
      { to: '/trajni-nalozi', label: 'Trajni nalozi', icon: 'repeat' },
      { to: '/menjacnica', label: 'Menjačnica', icon: 'exchange' },
      { to: '/transakcije', label: 'Transakcije', icon: 'list' },
      { to: '/primaoci', label: 'Primaoci', icon: 'users' },
    ],
  },
  {
    title: 'Podrška',
    items: [
      { to: '/obavestenja', label: 'Obaveštenja', icon: 'bell' },
      { to: '/prigovori', label: 'Prigovori', icon: 'chat' },
      { to: '/pomoc', label: 'Pomoć i cenovnik', icon: 'help' },
    ],
  },
  {
    title: 'Nalog',
    items: [
      { to: '/profil', label: 'Profil', icon: 'settings' },
      { to: '/bezbednost', label: 'Bezbednost', icon: 'shield' },
      { to: '/privatnost', label: 'Privatnost', icon: 'lock' },
    ],
  },
]

const bankerNavigation: NavSection[] = [
  {
    items: [
      { to: '/bankar', label: 'Pregled', icon: 'home' },
      { to: '/bankar/klijenti', label: 'Klijenti', icon: 'users' },
      { to: '/bankar/transakcije', label: 'Transakcije', icon: 'list' },
      { to: '/bankar/prigovori', label: 'Prigovori', icon: 'chat' },
      { to: '/bankar/ocene', label: 'Zadovoljstvo', icon: 'star' },
    ],
  },
  {
    title: 'Nalog',
    items: [
      { to: '/profil', label: 'Profil', icon: 'settings' },
      { to: '/bezbednost', label: 'Bezbednost', icon: 'shield' },
      { to: '/obavestenja', label: 'Obaveštenja', icon: 'bell' },
    ],
  },
]

const administratorNavigation: NavSection[] = [
  {
    items: [
      { to: '/admin', label: 'Pregled sistema', icon: 'home' },
      { to: '/admin/korisnici', label: 'Korisnici', icon: 'users' },
      { to: '/admin/racuni', label: 'Računi', icon: 'wallet' },
      { to: '/bankar/transakcije', label: 'Transakcije', icon: 'list' },
      { to: '/admin/knjiga', label: 'Glavna knjiga', icon: 'book' },
      { to: '/admin/kursna-lista', label: 'Kursna lista', icon: 'exchange' },
    ],
  },
  {
    title: 'Usklađenost',
    items: [
      { to: '/admin/aml', label: 'AML upozorenja', icon: 'alert' },
      { to: '/admin/zahtevi-lozinka', label: 'Zahtevi za lozinku', icon: 'key' },
      { to: '/admin/zahtevi-privatnost', label: 'Zahtevi po ZZPL', icon: 'lock' },
      { to: '/admin/audit', label: 'Audit log', icon: 'activity' },
      { to: '/bankar/prigovori', label: 'Prigovori', icon: 'chat' },
      { to: '/bankar/ocene', label: 'Zadovoljstvo', icon: 'star' },
      { to: '/admin/faq', label: 'Centar za pomoć', icon: 'help' },
    ],
  },
  {
    title: 'Nalog',
    items: [
      { to: '/profil', label: 'Profil', icon: 'settings' },
      { to: '/bezbednost', label: 'Bezbednost', icon: 'shield' },
    ],
  },
]

export function navigationFor(role: UserRole | null): NavSection[] {
  if (role === 'Korisnik') return customerNavigation
  if (role === 'Bankar') return bankerNavigation
  if (role === 'Administrator') return administratorNavigation
  return []
}

export function mobileNavigationFor(role: UserRole | null): NavItem[] {
  if (role === 'Korisnik') {
    return [
      { to: '/pocetna', label: 'Početna', icon: 'home' },
      { to: '/racuni', label: 'Računi', icon: 'wallet' },
      { to: '/placanja/novo', label: 'Plati', icon: 'send' },
      { to: '/transakcije', label: 'Istorija', icon: 'list' },
      { to: '/profil', label: 'Profil', icon: 'settings' },
    ]
  }
  return navigationFor(role)[0]?.items.slice(0, 5) ?? []
}
