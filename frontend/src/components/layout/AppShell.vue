<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { RouterLink, RouterView, useRoute, useRouter } from 'vue-router'
import AppIcon from '@/components/ui/AppIcon.vue'
import InactivityDialog from '@/components/security/InactivityDialog.vue'
import ConsentGate from '@/components/security/ConsentGate.vue'
import { mobileNavigationFor, navigationFor } from './navigation'
import { useAuthStore } from '@/stores/auth'
import { useUiStore } from '@/stores/ui'
import { useInactivityLogout } from '@/composables/useInactivityLogout'
import { notificationsApi, privacyApi } from '@/api/bank'
import { greetingForNow, initials } from '@/utils/format'
import { roleLabels } from '@/utils/labels'

const auth = useAuthStore()
const ui = useUiStore()
const route = useRoute()
const router = useRouter()

const sections = computed(() => navigationFor(auth.role))
const mobileItems = computed(() => mobileNavigationFor(auth.role))
const unreadCount = ref(0)
const menuOpen = ref(false)
let pollTimer: number | undefined

async function refreshUnread(): Promise<void> {
  try {
    unreadCount.value = (await notificationsApi.unreadCount()).count
  } catch {
    unreadCount.value = 0
  }
}

async function logout(reason?: string): Promise<void> {
  await auth.logout()
  await router.push({ path: '/prijava', query: reason ? { razlog: reason } : undefined })
}

const { isWarningVisible, secondsLeft, stayLoggedIn } = useInactivityLogout(() => logout('neaktivnost'))

function isActive(path: string): boolean {
  if (path === auth.homeRoute || path === '/placanja') return route.path === path
  return route.path === path || route.path.startsWith(`${path}/`)
}

function cycleTheme(): void {
  ui.theme = ui.theme === 'dark' ? 'light' : 'dark'
}

watch(
  () => route.fullPath,
  () => {
    ui.sidebarOpen = false
    menuOpen.value = false
    refreshUnread()
  },
)

async function applyPrivacyPreferences(): Promise<void> {
  if (!auth.isCustomer) return
  try {
    if ((await privacyApi.preferences()).hideBalancesByDefault) ui.hideBalances = true
  } catch {
    return
  }
}

onMounted(() => {
  refreshUnread()
  applyPrivacyPreferences()
  pollTimer = window.setInterval(refreshUnread, 60_000)
})

onBeforeUnmount(() => window.clearInterval(pollTimer))
</script>

<template>
  <a class="skip-link" href="#main-content">Preskoči na sadržaj</a>
  <div class="shell">
    <aside class="sidebar" :class="{ open: ui.sidebarOpen }" aria-label="Glavna navigacija">
      <RouterLink :to="auth.homeRoute" class="brand">
        <span class="brand-mark" aria-hidden="true">
          <svg viewBox="0 0 32 32" width="34" height="34">
            <rect width="32" height="32" rx="9" fill="#12332e" />
            <path d="M9 21.5 14.2 10.5h3.3L12.3 21.5zM15.6 21.5l5.2-11h2.8l-5.2 11z" fill="#F05A3C" />
            <circle cx="23.5" cy="21" r="2" fill="#C8F169" />
          </svg>
        </span>
        <span>
          <strong>Bankarski Sistem</strong>
          <small>{{ auth.role ? roleLabels[auth.role] : '' }}</small>
        </span>
      </RouterLink>

      <nav class="nav">
        <div v-for="(section, index) in sections" :key="index" class="nav-section">
          <p v-if="section.title" class="nav-title">{{ section.title }}</p>
          <RouterLink
            v-for="item in section.items"
            :key="item.to"
            :to="item.to"
            class="nav-link"
            :class="{ active: isActive(item.to) }"
            :aria-current="isActive(item.to) ? 'page' : undefined"
          >
            <AppIcon :name="item.icon" />
            <span>{{ item.label }}</span>
            <span v-if="item.to === '/obavestenja' && unreadCount > 0" class="nav-count">{{ unreadCount }}</span>
          </RouterLink>
        </div>
      </nav>

      <div class="sidebar-footer">
        <RouterLink to="/pomoc" class="help-card">
          <AppIcon name="help" />
          <span>
            <strong>Treba vam pomoć?</strong>
            <small>Česta pitanja, cenovnik i kontakt</small>
          </span>
        </RouterLink>
      </div>
    </aside>
    <div v-if="ui.sidebarOpen" class="scrim" @click="ui.sidebarOpen = false" />

    <div class="main-column">
      <header class="topbar">
        <button type="button" class="icon-button menu-button" aria-label="Otvori meni" @click="ui.sidebarOpen = true">
          <AppIcon name="menu" />
        </button>
        <div class="greeting">
          <p class="greeting-small">{{ greetingForNow() }},</p>
          <p class="greeting-name">{{ auth.session?.firstName }}</p>
        </div>
        <div class="topbar-actions">
          <button
            type="button"
            class="icon-button"
            :aria-pressed="ui.hideBalances"
            :aria-label="ui.hideBalances ? 'Prikaži iznose' : 'Sakrij iznose'"
            :title="ui.hideBalances ? 'Prikaži iznose' : 'Sakrij iznose'"
            @click="ui.toggleBalances()"
          >
            <AppIcon :name="ui.hideBalances ? 'eye-off' : 'eye'" />
          </button>
          <button type="button" class="icon-button" aria-label="Promeni temu" title="Promeni temu" @click="cycleTheme">
            <AppIcon :name="ui.theme === 'dark' ? 'sun' : 'moon'" />
          </button>
          <RouterLink to="/obavestenja" class="icon-button bell" :aria-label="`Obaveštenja, nepročitanih: ${unreadCount}`">
            <AppIcon name="bell" />
            <span v-if="unreadCount > 0" class="bell-dot">{{ unreadCount > 9 ? '9+' : unreadCount }}</span>
          </RouterLink>
          <div class="user-menu">
            <button type="button" class="avatar-button" :aria-expanded="menuOpen" aria-haspopup="menu" @click="menuOpen = !menuOpen">
              <span class="avatar">{{ initials(auth.session?.firstName, auth.session?.lastName) }}</span>
              <span class="user-name">{{ auth.fullName }}</span>
            </button>
            <div v-if="menuOpen" class="dropdown" role="menu">
              <RouterLink to="/profil" role="menuitem">Profil</RouterLink>
              <RouterLink to="/bezbednost" role="menuitem">Bezbednost i uređaji</RouterLink>
              <RouterLink v-if="auth.isCustomer" to="/privatnost" role="menuitem">Privatnost</RouterLink>
              <button type="button" role="menuitem" class="logout" @click="logout()">
                <AppIcon name="logout" :size="18" /> Odjavi se
              </button>
            </div>
          </div>
        </div>
      </header>

      <main id="main-content" class="content" tabindex="-1">
        <RouterView />
      </main>
    </div>

    <nav class="mobile-nav" aria-label="Brza navigacija">
      <RouterLink v-for="item in mobileItems" :key="item.to" :to="item.to" :class="{ active: isActive(item.to) }">
        <AppIcon :name="item.icon" />
        <span>{{ item.label }}</span>
      </RouterLink>
    </nav>
  </div>

  <InactivityDialog :open="isWarningVisible" :seconds-left="secondsLeft" @stay="stayLoggedIn" @logout="logout()" />
  <ConsentGate />
</template>

<style scoped>
.shell {
  display: grid;
  grid-template-columns: var(--sidebar-width) minmax(0, 1fr);
  min-height: 100vh;
}

.sidebar {
  position: sticky;
  top: 0;
  height: 100vh;
  display: flex;
  flex-direction: column;
  gap: var(--space-6);
  padding: var(--space-6) var(--space-4);
  background: var(--color-forest-900);
  color: #dfe9e5;
  overflow-y: auto;
}

.brand {
  display: flex;
  align-items: center;
  gap: var(--space-3);
  padding: 0 var(--space-2);
  text-decoration: none;
  color: #fff;
}

.brand strong {
  display: block;
  font-size: var(--font-size-lg);
  letter-spacing: -0.02em;
}

.brand small {
  color: var(--color-lime-400);
  font-size: var(--font-size-xs);
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.08em;
}

.nav {
  display: flex;
  flex-direction: column;
  gap: var(--space-5);
  flex: 1;
}

.nav-section {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.nav-title {
  padding: 0 var(--space-3) var(--space-2);
  font-size: var(--font-size-xs);
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.08em;
  color: rgb(223 233 229 / 0.45);
}

.nav-link {
  display: flex;
  align-items: center;
  gap: var(--space-3);
  padding: 0.65rem var(--space-3);
  border-radius: var(--radius-md);
  color: rgb(223 233 229 / 0.82);
  text-decoration: none;
  font-weight: 500;
  transition:
    background var(--transition-fast),
    color var(--transition-fast);
}

.nav-link:hover {
  background: var(--color-forest-800);
  color: #fff;
}

.nav-link.active {
  background: var(--color-forest-700);
  color: #fff;
  box-shadow: inset 3px 0 0 var(--color-coral-500);
}

.nav-count {
  margin-left: auto;
  min-width: 22px;
  padding: 1px 7px;
  border-radius: var(--radius-pill);
  background: var(--color-coral-500);
  color: #fff;
  font-size: var(--font-size-xs);
  text-align: center;
}

.help-card {
  display: flex;
  gap: var(--space-3);
  padding: var(--space-4);
  border-radius: var(--radius-md);
  background: var(--color-forest-800);
  color: #fff;
  text-decoration: none;
}

.help-card :deep(svg) {
  color: var(--color-lime-400);
}

.help-card small {
  display: block;
  color: rgb(223 233 229 / 0.6);
  font-size: var(--font-size-xs);
}

.main-column {
  display: flex;
  flex-direction: column;
  min-width: 0;
}

.topbar {
  position: sticky;
  top: 0;
  z-index: 50;
  display: flex;
  align-items: center;
  gap: var(--space-4);
  height: var(--topbar-height);
  padding: 0 var(--space-8);
  background: color-mix(in srgb, var(--color-bg) 88%, transparent);
  backdrop-filter: blur(10px);
  border-bottom: 1px solid var(--color-border);
}

.greeting-small {
  font-size: var(--font-size-sm);
  color: var(--color-text-muted);
}

.greeting-name {
  font-size: var(--font-size-lg);
  font-weight: 600;
}

.topbar-actions {
  margin-left: auto;
  display: flex;
  align-items: center;
  gap: var(--space-2);
}

.icon-button {
  position: relative;
  display: grid;
  place-items: center;
  width: 42px;
  height: 42px;
  border: 1px solid var(--color-border);
  border-radius: 50%;
  background: var(--color-surface);
  color: var(--color-text);
  cursor: pointer;
  text-decoration: none;
}

.icon-button:hover {
  border-color: var(--color-border-strong);
}

.bell-dot {
  position: absolute;
  top: -3px;
  right: -3px;
  min-width: 19px;
  height: 19px;
  padding: 0 5px;
  border-radius: var(--radius-pill);
  background: var(--color-coral-500);
  color: #fff;
  font-size: 11px;
  font-weight: 700;
  display: grid;
  place-items: center;
}

.menu-button {
  display: none;
}

.user-menu {
  position: relative;
}

.avatar-button {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  padding: 4px 14px 4px 4px;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-pill);
  background: var(--color-surface);
  cursor: pointer;
}

.avatar {
  display: grid;
  place-items: center;
  width: 34px;
  height: 34px;
  border-radius: 50%;
  background: var(--color-lime-400);
  color: var(--color-forest-900);
  font-weight: 700;
  font-size: var(--font-size-sm);
}

.user-name {
  font-weight: 600;
  font-size: var(--font-size-sm);
}

.dropdown {
  position: absolute;
  right: 0;
  top: calc(100% + 8px);
  min-width: 220px;
  padding: var(--space-2);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  background: var(--color-surface);
  box-shadow: var(--shadow-md);
  display: flex;
  flex-direction: column;
}

.dropdown a,
.dropdown button {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  padding: var(--space-2) var(--space-3);
  border: none;
  border-radius: var(--radius-sm);
  background: transparent;
  text-decoration: none;
  text-align: left;
  font-size: var(--font-size-sm);
  cursor: pointer;
}

.dropdown a:hover,
.dropdown button:hover {
  background: var(--color-surface-muted);
}

.logout {
  color: var(--color-danger);
  border-top: 1px solid var(--color-border) !important;
  margin-top: var(--space-1);
}

.content {
  width: 100%;
  max-width: var(--content-max-width);
  margin: 0 auto;
  padding: var(--space-8);
  outline: none;
}

.mobile-nav,
.scrim {
  display: none;
}

@media (max-width: 960px) {
  .shell {
    grid-template-columns: minmax(0, 1fr);
  }

  .sidebar {
    position: fixed;
    inset: 0 auto 0 0;
    z-index: 300;
    width: min(300px, 85vw);
    transform: translateX(-100%);
    transition: transform var(--transition-base);
  }

  .sidebar.open {
    transform: translateX(0);
  }

  .scrim {
    display: block;
    position: fixed;
    inset: 0;
    z-index: 250;
    background: rgb(6 21 15 / 0.5);
  }

  .menu-button {
    display: grid;
  }

  .topbar {
    padding: 0 var(--space-4);
  }

  .content {
    padding: var(--space-5) var(--space-4) calc(var(--space-8) + 64px);
  }

  .user-name {
    display: none;
  }

  .avatar-button {
    padding: 4px;
  }

  .mobile-nav {
    position: fixed;
    inset: auto 0 0 0;
    z-index: 200;
    display: flex;
    justify-content: space-around;
    padding: var(--space-2) var(--space-2) calc(var(--space-2) + env(safe-area-inset-bottom));
    background: var(--color-surface);
    border-top: 1px solid var(--color-border);
  }

  .mobile-nav a {
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 2px;
    min-width: 56px;
    padding: var(--space-1);
    color: var(--color-text-muted);
    text-decoration: none;
    font-size: 11px;
    font-weight: 600;
  }

  .mobile-nav a.active {
    color: var(--color-primary);
  }
}

@media (max-width: 520px) {
  .greeting-small {
    display: none;
  }
}
</style>
