import { createRouter, createWebHistory, type RouteRecordRaw } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { rememberPublicOrigin } from '@/utils/publicOrigin'
import type { UserRole } from '@/types/api'

declare module 'vue-router' {
  interface RouteMeta {
    title?: string
    public?: boolean
    roles?: UserRole[]
  }
}

const customer: UserRole[] = ['Korisnik']
const staff: UserRole[] = ['Bankar', 'Administrator']
const administrator: UserRole[] = ['Administrator']

const routes: RouteRecordRaw[] = [
  {
    path: '/prijava',
    component: () => import('@/views/auth/LoginView.vue'),
    meta: { public: true, title: 'Prijava' },
  },
  {
    path: '/javno',
    component: () => import('@/components/layout/PublicLayout.vue'),
    meta: { public: true },
    children: [
      { path: 'pomoc', component: () => import('@/views/shared/HelpView.vue'), meta: { public: true, title: 'Pomoć i cenovnik' } },
      { path: 'zaboravljena-lozinka', component: () => import('@/views/auth/ForgotPasswordView.vue'), meta: { public: true, title: 'Zaboravljena lozinka' } },
      { path: 'dokumenti/:type', component: () => import('@/views/shared/LegalDocumentView.vue'), meta: { public: true, title: 'Pravni dokumenti' } },
    ],
  },
  {
    path: '/',
    component: () => import('@/components/layout/AppShell.vue'),
    children: [
      { path: '', redirect: () => useAuthStore().homeRoute },
      { path: 'pocetna', component: () => import('@/views/customer/DashboardView.vue'), meta: { roles: customer, title: 'Početna' } },
      { path: 'racuni', component: () => import('@/views/customer/AccountsView.vue'), meta: { roles: customer, title: 'Računi' } },
      { path: 'racuni/:id/izvod', component: () => import('@/views/shared/StatementView.vue'), meta: { roles: customer, title: 'Izvod' } },
      { path: 'racuni/:id', component: () => import('@/views/customer/AccountDetailView.vue'), meta: { roles: customer, title: 'Račun' } },
      { path: 'placanja/novo', component: () => import('@/views/customer/NewPaymentView.vue'), meta: { roles: customer, title: 'Novo plaćanje' } },
      { path: 'placanja', component: () => import('@/views/customer/PaymentOrdersView.vue'), meta: { roles: customer, title: 'Nalozi' } },
      { path: 'menjacnica', component: () => import('@/views/customer/ExchangeView.vue'), meta: { roles: customer, title: 'Menjačnica' } },
      { path: 'trajni-nalozi', component: () => import('@/views/customer/StandingOrdersView.vue'), meta: { roles: customer, title: 'Trajni nalozi' } },
      { path: 'trajni-nalozi/novi', component: () => import('@/views/customer/StandingOrderFormView.vue'), meta: { roles: customer, title: 'Novi trajni nalog' } },
      { path: 'trajni-nalozi/:id', component: () => import('@/views/customer/StandingOrderDetailView.vue'), meta: { roles: customer, title: 'Trajni nalog' } },
      { path: 'transakcije', component: () => import('@/views/customer/TransactionsView.vue'), meta: { roles: customer, title: 'Transakcije' } },
      { path: 'kartice', component: () => import('@/views/customer/CardsView.vue'), meta: { roles: customer, title: 'Kartice' } },
      { path: 'stednja', component: () => import('@/views/customer/SavingsView.vue'), meta: { roles: customer, title: 'Štednja' } },
      { path: 'potrosnja', component: () => import('@/views/customer/SpendingView.vue'), meta: { roles: customer, title: 'Potrošnja i budžeti' } },
      { path: 'primaoci', component: () => import('@/views/customer/BeneficiariesView.vue'), meta: { roles: customer, title: 'Primaoci' } },
      { path: 'prigovori', component: () => import('@/views/customer/ComplaintsView.vue'), meta: { roles: customer, title: 'Prigovori' } },
      { path: 'prigovori/:id', component: () => import('@/views/customer/ComplaintDetailView.vue'), meta: { roles: customer, title: 'Prigovor' } },
      { path: 'privatnost', component: () => import('@/views/customer/PrivacyView.vue'), meta: { roles: customer, title: 'Privatnost' } },
      { path: 'pomoc', component: () => import('@/views/shared/HelpView.vue'), meta: { title: 'Pomoć i cenovnik' } },
      { path: 'obavestenja', component: () => import('@/views/shared/NotificationsView.vue'), meta: { title: 'Obaveštenja' } },
      { path: 'postavke', component: () => import('@/views/shared/SettingsView.vue'), meta: { title: 'Postavke' } },
      { path: 'profil', redirect: '/postavke' },
      { path: 'bezbednost', component: () => import('@/views/shared/SecurityView.vue'), meta: { title: 'Bezbednost' } },
      { path: 'bankar', component: () => import('@/views/staff/StaffDashboardView.vue'), meta: { roles: ['Bankar'], title: 'Pregled' } },
      { path: 'bankar/klijenti', component: () => import('@/views/staff/ClientsView.vue'), meta: { roles: staff, title: 'Klijenti' } },
      { path: 'bankar/racuni/:id/izvod', component: () => import('@/views/shared/StatementView.vue'), meta: { roles: staff, title: 'Izvod' } },
      { path: 'bankar/klijenti/:id', component: () => import('@/views/staff/ClientDetailView.vue'), meta: { roles: staff, title: 'Klijent' } },
      { path: 'bankar/transakcije', component: () => import('@/views/staff/StaffTransactionsView.vue'), meta: { roles: staff, title: 'Transakcije' } },
      { path: 'bankar/prigovori', component: () => import('@/views/staff/ComplaintsQueueView.vue'), meta: { roles: staff, title: 'Prigovori' } },
      { path: 'bankar/prigovori/:id', component: () => import('@/views/staff/StaffComplaintDetailView.vue'), meta: { roles: staff, title: 'Prigovor' } },
      { path: 'bankar/storniranja', component: () => import('@/views/staff/ReversalsView.vue'), meta: { roles: staff, title: 'Storniranja' } },
      { path: 'bankar/ocene', component: () => import('@/views/staff/FeedbackView.vue'), meta: { roles: staff, title: 'Zadovoljstvo korisnika' } },
      { path: 'admin', component: () => import('@/views/admin/AdminDashboardView.vue'), meta: { roles: administrator, title: 'Pregled sistema' } },
      { path: 'admin/korisnici', component: () => import('@/views/admin/UsersView.vue'), meta: { roles: administrator, title: 'Korisnici' } },
      { path: 'admin/racuni', component: () => import('@/views/admin/AllAccountsView.vue'), meta: { roles: administrator, title: 'Računi' } },
      { path: 'admin/kursna-lista', component: () => import('@/views/admin/ExchangeRatesAdminView.vue'), meta: { roles: administrator, title: 'Kursna lista' } },
      { path: 'admin/knjiga', component: () => import('@/views/admin/LedgerView.vue'), meta: { roles: administrator, title: 'Glavna knjiga' } },
      { path: 'admin/aml', component: () => import('@/views/admin/ComplianceView.vue'), meta: { roles: administrator, title: 'AML upozorenja' } },
      { path: 'admin/zahtevi-lozinka', component: () => import('@/views/admin/PasswordResetsView.vue'), meta: { roles: administrator, title: 'Zahtevi za lozinku' } },
      { path: 'admin/zahtevi-privatnost', component: () => import('@/views/admin/PrivacyRequestsView.vue'), meta: { roles: administrator, title: 'Zahtevi po ZZPL' } },
      { path: 'admin/audit', component: () => import('@/views/admin/AuditLogView.vue'), meta: { roles: administrator, title: 'Audit log' } },
      { path: 'admin/faq', component: () => import('@/views/admin/FaqAdminView.vue'), meta: { roles: administrator, title: 'Centar za pomoć' } },
    ],
  },
  { path: '/:pathMatch(.*)*', component: () => import('@/views/shared/NotFoundView.vue'), meta: { public: true, title: 'Stranica ne postoji' } },
]

const SETUP_ALLOWED_PATHS = ['/bezbednost', '/postavke', '/obavestenja', '/pomoc']

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes,
  scrollBehavior: () => ({ top: 0 }),
})

router.beforeEach(async (to, from) => {
  const auth = useAuthStore()
  await auth.initialize()
  if (from.matched.length) rememberPublicOrigin(from.fullPath, to.path)

  if (to.path === '/prijava' && auth.isAuthenticated) return auth.homeRoute
  if (to.meta.public) return true
  if (!auth.isAuthenticated) return { path: '/prijava', query: { povratak: to.fullPath } }
  if (auth.twoFactorSetupRequired && !SETUP_ALLOWED_PATHS.includes(to.path)) return { path: '/bezbednost', query: { obavezno: '1' } }
  if (to.meta.roles && auth.role && !to.meta.roles.includes(auth.role)) return auth.homeRoute
  return true
})

router.afterEach((to) => {
  document.title = to.meta.title ? `${to.meta.title} | Bankarski Sistem` : 'Bankarski Sistem'
})

export default router
