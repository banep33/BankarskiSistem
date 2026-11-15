<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { RouterLink } from 'vue-router'
import AppCard from '@/components/ui/AppCard.vue'
import AppButton from '@/components/ui/AppButton.vue'
import AppIcon from '@/components/ui/AppIcon.vue'
import StatusViews from '@/components/ui/StatusViews.vue'
import SystemOverviewPanel from '@/components/banking/SystemOverviewPanel.vue'
import { adminApi } from '@/api/bank'
import { useUiStore } from '@/stores/ui'
import { errorMessage } from '@/composables/useAsync'
import type { SystemOverview, TrialBalanceLine } from '@/types/api'

const ui = useUiStore()
const overview = ref<SystemOverview | null>(null)
const trialBalance = ref<TrialBalanceLine[]>([])
const openAlerts = ref(0)
const openPrivacyRequests = ref(0)
const reconciliationIssues = ref(0)
const isLoading = ref(true)
const isRunningRetention = ref(false)
const error = ref<string | null>(null)

async function load(): Promise<void> {
  isLoading.value = true
  error.value = null
  try {
    const [loadedOverview, balance, alerts, requests, differences] = await Promise.all([
      adminApi.overview(),
      adminApi.trialBalance(),
      adminApi.alerts('Otvoren'),
      adminApi.privacyRequests('Podnet'),
      adminApi.reconciliation(true),
    ])
    overview.value = loadedOverview
    trialBalance.value = balance
    openAlerts.value = alerts.totalCount
    openPrivacyRequests.value = requests.totalCount
    reconciliationIssues.value = differences.length
  } catch (caught) {
    error.value = errorMessage(caught)
  } finally {
    isLoading.value = false
  }
}

async function runRetention(): Promise<void> {
  isRunningRetention.value = true
  try {
    const report = await adminApi.runRetention()
    ui.notify(`Obrisano obaveštenja: ${report.readNotificationsDeleted}, isteklih sesija: ${report.expiredSessionsDeleted}.`)
  } catch (caught) {
    ui.notify(errorMessage(caught), 'danger')
  } finally {
    isRunningRetention.value = false
  }
}

const isBalanced = () => trialBalance.value.every((line) => line.total === 0)

onMounted(load)
</script>

<template>
  <div class="page">
    <header class="page-header">
      <div>
        <h1 class="page-title">Pregled sistema</h1>
        <p class="page-subtitle">Integritet knjige, usklađenost i aktivnost na jednom mestu.</p>
      </div>
      <AppButton variant="secondary" icon="refresh" @click="load">Osveži</AppButton>
    </header>

    <StatusViews v-if="isLoading" state="loading" :rows="6" />
    <StatusViews v-else-if="error" state="error" :message="error" @retry="load" />

    <template v-else-if="overview">
      <div class="grid-4">
        <RouterLink to="/admin/knjiga" class="health" :class="isBalanced() && reconciliationIssues === 0 ? 'ok' : 'bad'">
          <AppIcon :name="isBalanced() && reconciliationIssues === 0 ? 'check' : 'alert'" />
          <span>
            <strong>Glavna knjiga</strong>
            <small>{{ isBalanced() ? 'Bruto bilans je uravnotežen' : 'Bruto bilans NIJE uravnotežen' }}, razlika kod {{ reconciliationIssues }} računa</small>
          </span>
        </RouterLink>
        <RouterLink to="/admin/aml" class="health" :class="openAlerts ? 'warn' : 'ok'">
          <AppIcon name="alert" />
          <span><strong>{{ openAlerts }} AML upozorenja</strong><small>otvoreno, čeka pregled</small></span>
        </RouterLink>
        <RouterLink to="/admin/zahtevi-privatnost" class="health" :class="openPrivacyRequests ? 'warn' : 'ok'">
          <AppIcon name="lock" />
          <span><strong>{{ openPrivacyRequests }} zahteva po ZZPL</strong><small>rok za odgovor 30 dana</small></span>
        </RouterLink>
        <RouterLink to="/admin/audit" class="health ok">
          <AppIcon name="activity" />
          <span><strong>Audit log</strong><small>sve osetljive radnje su evidentirane</small></span>
        </RouterLink>
      </div>
      <SystemOverviewPanel :overview="overview" />
      <AppCard tone="cream" title="Simulacija i čuvanje podataka">
        <p class="muted">AML pravila i kliring su simulacija u edukativne svrhe. U produkciji se upozorenja prosleđuju Upravi za sprečavanje pranja novca kroz propisani kanal.</p>
        <p class="muted retention-copy">Pročitana obaveštenja i istekle sesije brišu se automatski po rokovima čuvanja. Čišćenje možete pokrenuti i ručno.</p>
        <AppButton class="retention" variant="dark" size="sm" icon="trash" :loading="isRunningRetention" @click="runRetention">Pokreni čišćenje sada</AppButton>
      </AppCard>
    </template>
  </div>
</template>

<style scoped>
.health {
  display: flex;
  gap: var(--space-3);
  padding: var(--space-5);
  border-radius: var(--radius-lg);
  text-decoration: none;
  border: 1px solid transparent;
}

.health small {
  display: block;
  font-size: var(--font-size-xs);
  opacity: 0.8;
}

.health.ok {
  background: var(--color-success-soft);
  color: var(--color-success);
}

.health.warn {
  background: var(--color-warning-soft);
  color: var(--color-warning);
}

.health.bad {
  background: var(--color-danger-soft);
  color: var(--color-danger);
}

.health strong {
  color: var(--color-text);
}

.retention-copy {
  margin-top: var(--space-3);
}

.retention {
  margin-top: var(--space-4);
}
</style>
