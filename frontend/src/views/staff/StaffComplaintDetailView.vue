<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useRoute } from 'vue-router'
import AppCard from '@/components/ui/AppCard.vue'
import AppBadge from '@/components/ui/AppBadge.vue'
import AppButton from '@/components/ui/AppButton.vue'
import AppField from '@/components/ui/AppField.vue'
import StatusViews from '@/components/ui/StatusViews.vue'
import ComplaintThread from '@/components/banking/ComplaintThread.vue'
import { complaintsApi } from '@/api/bank'
import { useAuthStore } from '@/stores/auth'
import { useUiStore } from '@/stores/ui'
import { errorMessage } from '@/composables/useAsync'
import { complaintCategoryLabels, complaintStatusLabels } from '@/utils/labels'
import { formatDate, formatDateTime } from '@/utils/format'
import type { ComplaintDetail, ComplaintStatus } from '@/types/api'

const route = useRoute()
const auth = useAuthStore()
const ui = useUiStore()
const complaint = ref<ComplaintDetail | null>(null)
const isLoading = ref(true)
const error = ref<string | null>(null)
const message = reactive({ text: '', internal: false, sending: false })
const status = reactive({ value: 'UObradi' as ComplaintStatus, note: '', saving: false })
const extension = reactive({ reason: '', saving: false })

const isClosed = computed(() => ['Resen', 'Odbijen', 'Zatvoren'].includes(complaint.value?.summary.status ?? ''))
const statusOptions = Object.entries(complaintStatusLabels)
  .filter(([value]) => value !== 'Podnet')
  .map(([value, item]) => ({ value, label: item.label }))

async function run(action: () => Promise<ComplaintDetail>, success: string): Promise<boolean> {
  try {
    complaint.value = await action()
    ui.notify(success)
    return true
  } catch (caught) {
    ui.notify(errorMessage(caught), 'danger')
    return false
  }
}

async function load(): Promise<void> {
  isLoading.value = true
  error.value = null
  try {
    complaint.value = await complaintsApi.staffDetail(Number(route.params.id))
    status.value = complaint.value.summary.status === 'Podnet' ? 'UObradi' : complaint.value.summary.status
  } catch (caught) {
    error.value = errorMessage(caught)
  } finally {
    isLoading.value = false
  }
}

async function assignToMe(): Promise<void> {
  if (!complaint.value || !auth.session) return
  await run(() => complaintsApi.assign(complaint.value!.summary.id, auth.session!.userId), 'Prigovor je dodeljen vama.')
}

async function sendMessage(): Promise<void> {
  if (!complaint.value || message.text.trim().length < 2) return
  message.sending = true
  const ok = await run(
    () => complaintsApi.staffMessage(complaint.value!.summary.id, message.text.trim(), message.internal),
    message.internal ? 'Interna beleška je sačuvana.' : 'Odgovor je poslat klijentu.',
  )
  if (ok) message.text = ''
  message.sending = false
}

async function changeStatus(): Promise<void> {
  if (!complaint.value) return
  if (['Resen', 'Odbijen'].includes(status.value) && status.note.trim().length < 10) {
    ui.notify('Za rešenje ili odbijanje unesite obrazloženje koje će klijent videti.', 'danger')
    return
  }
  status.saving = true
  const ok = await run(() => complaintsApi.changeStatus(complaint.value!.summary.id, status.value, status.note.trim() || null), 'Status je promenjen.')
  if (ok) status.note = ''
  status.saving = false
}

async function extend(): Promise<void> {
  if (!complaint.value || extension.reason.trim().length < 10) {
    ui.notify('Unesite obrazloženje produženja roka (najmanje 10 karaktera).', 'danger')
    return
  }
  extension.saving = true
  const ok = await run(() => complaintsApi.extendDeadline(complaint.value!.summary.id, extension.reason.trim()), 'Rok je produžen i klijent je obavešten.')
  if (ok) extension.reason = ''
  extension.saving = false
}

onMounted(load)
</script>

<template>
  <div class="page">
    <StatusViews v-if="isLoading" state="loading" :rows="5" />
    <StatusViews v-else-if="error" state="error" :message="error" @retry="load" />

    <template v-else-if="complaint">
      <header class="page-header">
        <div>
          <RouterLink to="/bankar/prigovori" class="back">← Svi prigovori</RouterLink>
          <h1 class="page-title">{{ complaint.summary.subject }}</h1>
          <p class="page-subtitle">
            <span class="mono">{{ complaint.summary.referenceNumber }}</span> · {{ complaint.summary.customerName }} ·
            {{ complaintCategoryLabels[complaint.summary.category] }}
          </p>
        </div>
        <div class="row">
          <AppBadge v-if="complaint.summary.isOverdue" tone="danger">Rok prekoračen</AppBadge>
          <AppBadge :tone="complaintStatusLabels[complaint.summary.status].tone">{{ complaintStatusLabels[complaint.summary.status].label }}</AppBadge>
        </div>
      </header>

      <div class="layout">
        <AppCard title="Prepiska">
          <ComplaintThread :complaint="complaint" show-internal />
          <form v-if="!isClosed" class="reply" @submit.prevent="sendMessage">
            <AppField v-model="message.text" label="Poruka" multiline :rows="4" :maxlength="5000" />
            <div class="row-between">
              <label class="check"><input v-model="message.internal" type="checkbox" /> Interna beleška (klijent ne vidi)</label>
              <AppButton type="submit" icon="send" :loading="message.sending" :disabled="message.text.trim().length < 2">
                {{ message.internal ? 'Sačuvaj belešku' : 'Pošalji klijentu' }}
              </AppButton>
            </div>
          </form>
        </AppCard>

        <aside class="stack">
          <AppCard title="Predmet">
            <dl class="facts">
              <div><dt>Podnet</dt><dd>{{ formatDateTime(complaint.summary.createdAt) }}</dd></div>
              <div><dt>Rok</dt><dd :class="{ late: complaint.summary.isOverdue }">{{ formatDate(complaint.summary.dueAt) }}</dd></div>
              <div><dt>Zadužen</dt><dd>{{ complaint.summary.assignedTo ?? 'Nije dodeljen' }}</dd></div>
              <div v-if="complaint.relatedTransactionId"><dt>Transakcija</dt><dd>#{{ complaint.relatedTransactionId }}</dd></div>
            </dl>
            <AppButton v-if="!isClosed" class="assign" variant="secondary" size="sm" block @click="assignToMe">Preuzmi predmet</AppButton>
          </AppCard>

          <AppCard v-if="!isClosed" title="Promena statusa">
            <form class="stack" @submit.prevent="changeStatus">
              <AppField v-model="status.value" label="Novi status" :options="statusOptions" />
              <AppField v-model="status.note" label="Obrazloženje" multiline :rows="3" :maxlength="5000" hint="Kod rešenja ili odbijanja ovo je odgovor koji klijent dobija." />
              <AppButton type="submit" :loading="status.saving">Sačuvaj status</AppButton>
            </form>
          </AppCard>

          <AppCard v-if="!isClosed && !complaint.summary.deadlineExtended" title="Produženje roka" subtitle="Moguće jednom, za još 15 dana.">
            <form class="stack" @submit.prevent="extend">
              <AppField v-model="extension.reason" label="Razlog" multiline :rows="2" :maxlength="500" />
              <AppButton type="submit" variant="secondary" :loading="extension.saving">Produži rok</AppButton>
            </form>
          </AppCard>
        </aside>
      </div>
    </template>
  </div>
</template>

<style scoped>
.back {
  font-size: var(--font-size-sm);
  color: var(--color-text-muted);
  text-decoration: none;
}

.layout {
  display: grid;
  grid-template-columns: minmax(0, 1.7fr) minmax(280px, 1fr);
  gap: var(--space-5);
  align-items: start;
}

.reply {
  display: flex;
  flex-direction: column;
  gap: var(--space-3);
  margin-top: var(--space-5);
  padding-top: var(--space-5);
  border-top: 1px solid var(--color-border);
}

.check {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  font-size: var(--font-size-sm);
  cursor: pointer;
}

.check input {
  accent-color: var(--color-primary);
}

.facts {
  margin: 0;
}

.facts div {
  display: flex;
  justify-content: space-between;
  gap: var(--space-3);
  padding: var(--space-2) 0;
  border-bottom: 1px solid var(--color-border);
}

.facts dt {
  color: var(--color-text-muted);
  font-size: var(--font-size-sm);
}

.facts dd {
  margin: 0;
  font-weight: 600;
  text-align: right;
}

.late {
  color: var(--color-danger);
}

.assign {
  margin-top: var(--space-4);
}

@media (max-width: 1000px) {
  .layout {
    grid-template-columns: minmax(0, 1fr);
  }
}
</style>
