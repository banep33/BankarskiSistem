<script setup lang="ts">
import { onMounted, ref, watch } from 'vue'
import AppCard from '@/components/ui/AppCard.vue'
import AppButton from '@/components/ui/AppButton.vue'
import AppBadge from '@/components/ui/AppBadge.vue'
import AppIcon from '@/components/ui/AppIcon.vue'
import AppModal from '@/components/ui/AppModal.vue'
import AppPagination from '@/components/ui/AppPagination.vue'
import StatusViews from '@/components/ui/StatusViews.vue'
import TwoFactorCard from '@/components/security/TwoFactorCard.vue'
import { securityApi } from '@/api/bank'
import { useUiStore } from '@/stores/ui'
import { errorMessage } from '@/composables/useAsync'
import { securityActionLabels } from '@/utils/labels'
import { formatDateTime, formatRelative } from '@/utils/format'
import type { PagedResponse, SecurityEvent, Session } from '@/types/api'

const ui = useUiStore()
const sessions = ref<Session[]>([])
const events = ref<PagedResponse<SecurityEvent> | null>(null)
const eventsPage = ref(1)
const isLoading = ref(true)
const error = ref<string | null>(null)
const confirmOthers = ref(false)
const isRevoking = ref(false)

async function load(): Promise<void> {
  isLoading.value = true
  error.value = null
  try {
    ;[sessions.value, events.value] = await Promise.all([securityApi.sessions(), securityApi.events(eventsPage.value)])
  } catch (caught) {
    error.value = errorMessage(caught)
  } finally {
    isLoading.value = false
  }
}

async function revoke(session: Session): Promise<void> {
  try {
    await securityApi.revokeSession(session.id)
    sessions.value = sessions.value.filter((item) => item.id !== session.id)
    ui.notify('Uređaj je odjavljen.')
  } catch (caught) {
    ui.notify(errorMessage(caught), 'danger')
  }
}

async function revokeOthers(): Promise<void> {
  isRevoking.value = true
  try {
    const { revoked } = await securityApi.revokeOthers()
    confirmOthers.value = false
    ui.notify(revoked ? `Odjavljeno uređaja: ${revoked}` : 'Nema drugih aktivnih sesija.')
    sessions.value = await securityApi.sessions()
  } catch (caught) {
    ui.notify(errorMessage(caught), 'danger')
  } finally {
    isRevoking.value = false
  }
}

function deviceIcon(label: string | null): string {
  return label && /android|iphone|mobile/i.test(label) ? 'device' : 'bank'
}

watch(eventsPage, async () => {
  events.value = await securityApi.events(eventsPage.value)
})
onMounted(load)
</script>

<template>
  <div class="page">
    <header class="page-header">
      <div>
        <h1 class="page-title">Bezbednost</h1>
        <p class="page-subtitle">Aktivni uređaji i istorija prijava. Ako vidite nešto što ne prepoznajete, odjavite uređaj i promenite lozinku.</p>
      </div>
      <AppButton variant="secondary" icon="logout" @click="confirmOthers = true">Odjavi sve druge uređaje</AppButton>
    </header>

    <StatusViews v-if="isLoading" state="loading" :rows="4" />
    <StatusViews v-else-if="error" state="error" :message="error" @retry="load" />

    <template v-else>
      <div class="tips grid-3">
        <AppCard tone="cream">
          <AppIcon name="shield" />
          <h3>Lozinku znate samo vi</h3>
          <p class="muted">Zaposleni banke nikada ne traže lozinku, ni telefonom ni porukom.</p>
        </AppCard>
        <AppCard tone="cream">
          <AppIcon name="lock" />
          <h3>Automatska odjava</h3>
          <p class="muted">Posle nekoliko minuta neaktivnosti odjavljujemo vas radi zaštite.</p>
        </AppCard>
        <AppCard tone="cream">
          <AppIcon name="alert" />
          <h3>Zaključavanje naloga</h3>
          <p class="muted">Posle 5 pogrešnih lozinki nalog se privremeno zaključava.</p>
        </AppCard>
      </div>

      <TwoFactorCard />

      <AppCard title="Aktivne sesije" :subtitle="`Ukupno ${sessions.length}`">
        <ul class="sessions">
          <li v-for="session in sessions" :key="session.id" class="session">
            <span class="device-icon" :class="{ current: session.isCurrent }"><AppIcon :name="deviceIcon(session.deviceLabel)" /></span>
            <div class="session-info">
              <strong>{{ session.deviceLabel ?? 'Nepoznat uređaj' }}</strong>
              <span class="subtle">
                IP {{ session.ipAddress ?? 'nepoznata' }} · prijava {{ formatDateTime(session.createdAt) }} · aktivnost {{ formatRelative(session.lastSeenAt) }}
              </span>
            </div>
            <AppBadge v-if="session.isCurrent" tone="success">Ovaj uređaj</AppBadge>
            <AppButton v-else variant="ghost" size="sm" @click="revoke(session)">Odjavi</AppButton>
          </li>
        </ul>
      </AppCard>

      <AppCard title="Bezbednosni događaji">
        <StatusViews v-if="!events?.items.length" state="empty" icon="activity" title="Nema događaja" />
        <div v-else class="table-scroll">
          <table class="data-table">
            <thead>
              <tr>
                <th scope="col">Vreme</th>
                <th scope="col">Događaj</th>
                <th scope="col">IP adresa</th>
                <th scope="col">Ishod</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="(event, index) in events.items" :key="index">
                <td>{{ formatDateTime(event.occurredAt) }}</td>
                <td>{{ securityActionLabels[event.action] ?? event.action }}</td>
                <td class="mono subtle">{{ event.ipAddress ?? 'nepoznata' }}</td>
                <td><AppBadge :tone="event.isSuccess ? 'success' : 'danger'">{{ event.isSuccess ? 'Uspešno' : 'Neuspešno' }}</AppBadge></td>
              </tr>
            </tbody>
          </table>
        </div>
        <AppPagination v-if="events" :page="events.page" :total-pages="events.totalPages" :total-count="events.totalCount" @change="eventsPage = $event" />
      </AppCard>
    </template>

    <AppModal :open="confirmOthers" title="Odjaviti sve druge uređaje?" width="440px" @close="confirmOthers = false">
      <p class="muted">Ostaćete prijavljeni samo na ovom uređaju. Ovo preporučujemo ako sumnjate da je neko drugi pristupio nalogu.</p>
      <template #footer>
        <AppButton variant="ghost" @click="confirmOthers = false">Odustani</AppButton>
        <AppButton variant="danger" :loading="isRevoking" @click="revokeOthers">Odjavi sve druge</AppButton>
      </template>
    </AppModal>
  </div>
</template>

<style scoped>
.tips h3 {
  margin: var(--space-2) 0 var(--space-1);
  font-size: var(--font-size-md);
}

.tips :deep(svg) {
  color: var(--color-coral-700);
}

.sessions {
  list-style: none;
  margin: 0;
  padding: 0;
}

.session {
  display: flex;
  align-items: center;
  gap: var(--space-3);
  padding: var(--space-3) 0;
}

.session + .session {
  border-top: 1px solid var(--color-border);
}

.device-icon {
  display: grid;
  place-items: center;
  width: 42px;
  height: 42px;
  flex-shrink: 0;
  border-radius: 50%;
  background: var(--color-surface-muted);
}

.device-icon.current {
  background: var(--color-forest-900);
  color: var(--color-lime-400);
}

.session-info {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-width: 0;
}
</style>
