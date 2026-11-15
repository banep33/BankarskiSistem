<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { RouterLink } from 'vue-router'
import AppCard from '@/components/ui/AppCard.vue'
import AppIcon from '@/components/ui/AppIcon.vue'
import StatusViews from '@/components/ui/StatusViews.vue'
import SystemOverviewPanel from '@/components/banking/SystemOverviewPanel.vue'
import { adminApi, complaintsApi, usersApi } from '@/api/bank'
import { useAuthStore } from '@/stores/auth'
import { errorMessage } from '@/composables/useAsync'
import type { SystemOverview } from '@/types/api'

const auth = useAuthStore()
const overview = ref<SystemOverview | null>(null)
const overdueComplaints = ref(0)
const openComplaints = ref(0)
const pendingKyc = ref(0)
const isLoading = ref(true)
const error = ref<string | null>(null)

async function load(): Promise<void> {
  isLoading.value = true
  error.value = null
  try {
    const [loadedOverview, overdue, open, customers] = await Promise.all([
      adminApi.overview(),
      complaintsApi.queue({ onlyOverdue: true }),
      complaintsApi.queue({ status: 'Podnet' }),
      usersApi.list({ role: 'Korisnik', pageSize: 100 }),
    ])
    overview.value = loadedOverview
    overdueComplaints.value = overdue.totalCount
    openComplaints.value = open.totalCount
    pendingKyc.value = customers.items.filter((user) => user.kycStatus === 'NijeVerifikovan').length
  } catch (caught) {
    error.value = errorMessage(caught)
  } finally {
    isLoading.value = false
  }
}

onMounted(load)
</script>

<template>
  <div class="page">
    <header class="page-header">
      <div>
        <h1 class="page-title">Dobro došli, {{ auth.session?.firstName }}</h1>
        <p class="page-subtitle">Zadaci koji čekaju i stanje poslovanja za danas.</p>
      </div>
    </header>

    <StatusViews v-if="isLoading" state="loading" :rows="5" />
    <StatusViews v-else-if="error" state="error" :message="error" @retry="load" />

    <template v-else-if="overview">
      <div class="grid-3">
        <RouterLink to="/bankar/prigovori" class="task" :class="{ urgent: overdueComplaints > 0 }">
          <AppIcon name="alert" />
          <span><strong>{{ overdueComplaints }}</strong> prigovora sa prekoračenim rokom</span>
        </RouterLink>
        <RouterLink to="/bankar/prigovori" class="task">
          <AppIcon name="chat" />
          <span><strong>{{ openComplaints }}</strong> novih prigovora čeka obradu</span>
        </RouterLink>
        <RouterLink to="/bankar/klijenti" class="task">
          <AppIcon name="users" />
          <span><strong>{{ pendingKyc }}</strong> klijenata čeka proveru identiteta</span>
        </RouterLink>
      </div>
      <SystemOverviewPanel :overview="overview" />
      <AppCard tone="cream">
        <p class="reminder">
          <AppIcon name="shield" :size="18" /> Podatke klijenata otvarajte samo kada postoji poslovni razlog. Svaki pristup se evidentira i klijent ga vidi u
          svom odeljku Privatnost.
        </p>
      </AppCard>
    </template>
  </div>
</template>

<style scoped>
.task {
  display: flex;
  align-items: center;
  gap: var(--space-3);
  padding: var(--space-5);
  border-radius: var(--radius-lg);
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  text-decoration: none;
  box-shadow: var(--shadow-sm);
}

.task :deep(svg) {
  color: var(--color-forest-700);
}

.task strong {
  font-size: var(--font-size-xl);
  margin-right: 4px;
}

.task.urgent {
  background: var(--color-danger-soft);
  border-color: transparent;
}

.task.urgent :deep(svg) {
  color: var(--color-danger);
}

.reminder {
  display: flex;
  gap: var(--space-2);
  align-items: flex-start;
  font-size: var(--font-size-sm);
}
</style>
