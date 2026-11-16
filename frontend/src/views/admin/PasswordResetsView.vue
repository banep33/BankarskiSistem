<script setup lang="ts">
import { onMounted, reactive, ref, watch } from 'vue'
import AppCard from '@/components/ui/AppCard.vue'
import AppBadge from '@/components/ui/AppBadge.vue'
import AppButton from '@/components/ui/AppButton.vue'
import AppField from '@/components/ui/AppField.vue'
import AppIcon from '@/components/ui/AppIcon.vue'
import AppModal from '@/components/ui/AppModal.vue'
import AppPagination from '@/components/ui/AppPagination.vue'
import StatusViews from '@/components/ui/StatusViews.vue'
import { adminApi } from '@/api/bank'
import { useAuthStore } from '@/stores/auth'
import { useUiStore } from '@/stores/ui'
import { errorMessage } from '@/composables/useAsync'
import { passwordResetStatusLabels, roleLabels } from '@/utils/labels'
import { formatDateTime, formatRelative } from '@/utils/format'
import type { PagedResponse, PasswordResetRequest, PasswordResetStatus } from '@/types/api'

const auth = useAuthStore()
const ui = useUiStore()
const status = ref<PasswordResetStatus | ''>('Podnet')
const page = ref(1)
const result = ref<PagedResponse<PasswordResetRequest> | null>(null)
const isLoading = ref(true)
const error = ref<string | null>(null)
const decision = reactive({
  request: null as PasswordResetRequest | null,
  mode: 'approve' as 'approve' | 'reject',
  note: '',
  identityConfirmed: false,
  saving: false,
})

const statusOptions = [{ value: '', label: 'Svi statusi' }, ...Object.entries(passwordResetStatusLabels).map(([value, item]) => ({ value, label: item.label }))]

async function load(): Promise<void> {
  isLoading.value = true
  error.value = null
  try {
    result.value = await adminApi.passwordResets(status.value || undefined, page.value)
  } catch (caught) {
    error.value = errorMessage(caught)
  } finally {
    isLoading.value = false
  }
}

function open(request: PasswordResetRequest, mode: 'approve' | 'reject'): void {
  Object.assign(decision, { request, mode, note: '', identityConfirmed: false })
}

async function submit(): Promise<void> {
  if (!decision.request) return
  if (decision.mode === 'approve' && !decision.identityConfirmed) {
    ui.notify('Potvrdite da ste proverili identitet podnosioca.', 'danger')
    return
  }
  if (decision.mode === 'reject' && decision.note.trim().length < 5) {
    ui.notify('Unesite razlog odbijanja.', 'danger')
    return
  }
  decision.saving = true
  try {
    if (decision.mode === 'approve') {
      await adminApi.approvePasswordReset(decision.request.id, decision.note.trim() || null)
      ui.notify('Nova lozinka je aktivirana, a sve sesije korisnika su odjavljene.')
    } else {
      await adminApi.rejectPasswordReset(decision.request.id, decision.note.trim())
      ui.notify('Zahtev je odbijen. Lozinka korisnika nije promenjena.')
    }
    decision.request = null
    await load()
  } catch (caught) {
    ui.notify(errorMessage(caught), 'danger')
  } finally {
    decision.saving = false
  }
}

function isOwn(request: PasswordResetRequest): boolean {
  return request.userId === auth.session?.userId
}

watch(status, () => {
  page.value = 1
  load()
})
watch(page, load)
onMounted(load)
</script>

<template>
  <div class="page">
    <header class="page-header">
      <div>
        <h1 class="page-title">Zahtevi za lozinku</h1>
        <p class="page-subtitle">Korisnici koji su zaboravili lozinku predlažu novu. Ona važi tek kada je odobrite, posle provere identiteta.</p>
      </div>
      <AppField v-model="status" class="status-filter" label="Status" :options="statusOptions" />
    </header>

    <AppCard tone="cream">
      <div class="rules">
        <AppIcon name="shield" />
        <ul>
          <li>Pre odobrenja proverite identitet kanalom koji ne zavisi od zahteva: povratni poziv na broj iz evidencije ili lični dolazak.</li>
          <li>Obratite pažnju na IP adresu i uređaj. Zahtev sa nepoznatog uređaja posle više neuspešnih prijava može biti pokušaj preuzimanja naloga.</li>
          <li>O zahtevu za sopstvenu lozinku mora da odluči drugi administrator. Zahtev ističe posle 72 sata.</li>
        </ul>
      </div>
    </AppCard>

    <AppCard :padded="false">
      <div class="card-body">
        <StatusViews v-if="isLoading" state="loading" :rows="5" />
        <StatusViews v-else-if="error" state="error" :message="error" @retry="load" />
        <StatusViews v-else-if="!result?.items.length" state="empty" icon="key" title="Nema zahteva" message="Za izabrani status nema zahteva za novu lozinku." />
        <div v-else class="table-scroll">
          <table class="data-table">
            <thead>
              <tr>
                <th scope="col">Korisnik</th>
                <th scope="col">Podnet</th>
                <th scope="col">Uređaj</th>
                <th scope="col">Status</th>
                <th scope="col"><span class="visually-hidden">Radnje</span></th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="request in result.items" :key="request.id">
                <td>
                  <strong>{{ request.fullName }}</strong>
                  <div class="subtle">{{ request.email }}</div>
                  <div class="row tags">
                    <AppBadge v-if="request.role" tone="neutral">{{ roleLabels[request.role] }}</AppBadge>
                    <AppBadge v-if="request.userIsLocked" tone="danger">Nalog zaključan</AppBadge>
                  </div>
                </td>
                <td>
                  {{ formatDateTime(request.createdAt) }}
                  <div class="subtle mono">{{ request.referenceNumber }}</div>
                  <div v-if="request.status === 'Podnet'" class="subtle">ističe {{ formatRelative(request.expiresAt) }}</div>
                </td>
                <td class="device">
                  <span class="mono">{{ request.ipAddress ?? 'nepoznata IP adresa' }}</span>
                  <div class="subtle agent">{{ request.userAgent ?? 'nepoznat uređaj' }}</div>
                </td>
                <td>
                  <AppBadge :tone="passwordResetStatusLabels[request.status].tone">{{ passwordResetStatusLabels[request.status].label }}</AppBadge>
                  <div v-if="request.reviewedBy" class="subtle">{{ request.reviewedBy }}, {{ formatDateTime(request.reviewedAt) }}</div>
                  <div v-if="request.reviewNote" class="subtle note">{{ request.reviewNote }}</div>
                </td>
                <td class="actions">
                  <template v-if="request.status === 'Podnet'">
                    <span v-if="isOwn(request)" class="subtle">Odlučuje drugi administrator</span>
                    <template v-else>
                      <AppButton size="sm" icon="check" @click="open(request, 'approve')">Odobri</AppButton>
                      <AppButton size="sm" variant="ghost" @click="open(request, 'reject')">Odbij</AppButton>
                    </template>
                  </template>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
        <AppPagination v-if="result" :page="result.page" :total-pages="result.totalPages" :total-count="result.totalCount" @change="page = $event" />
      </div>
    </AppCard>

    <AppModal
      :open="decision.request !== null"
      :title="decision.mode === 'approve' ? 'Odobravanje nove lozinke' : 'Odbijanje zahteva'"
      width="520px"
      @close="decision.request = null"
    >
      <form id="reset-decision" class="stack" @submit.prevent="submit">
        <p v-if="decision.request" class="muted">
          {{ decision.request.fullName }} ({{ decision.request.email }}), zahtev <span class="mono">{{ decision.request.referenceNumber }}</span>
        </p>
        <label v-if="decision.mode === 'approve'" class="confirm">
          <input v-model="decision.identityConfirmed" type="checkbox" />
          <span>Proverio/la sam identitet podnosioca nezavisnim kanalom (povratni poziv ili lični dolazak).</span>
        </label>
        <AppField
          v-model="decision.note"
          :label="decision.mode === 'approve' ? 'Napomena (nije obavezno)' : 'Razlog odbijanja'"
          multiline
          :rows="3"
          :maxlength="500"
          :required="decision.mode === 'reject'"
          :hint="decision.mode === 'approve' ? 'Npr. kako je identitet potvrđen. Vidljivo samo zaposlenima.' : 'Korisnik dobija obaveštenje da zahtev nije odobren.'"
        />
      </form>
      <template #footer>
        <AppButton variant="ghost" @click="decision.request = null">Odustani</AppButton>
        <AppButton type="submit" form="reset-decision" :variant="decision.mode === 'approve' ? 'primary' : 'danger'" :loading="decision.saving">
          {{ decision.mode === 'approve' ? 'Odobri i aktiviraj' : 'Odbij zahtev' }}
        </AppButton>
      </template>
    </AppModal>
  </div>
</template>

<style scoped>
.status-filter {
  min-width: 220px;
}

.rules {
  display: flex;
  gap: var(--space-3);
  align-items: flex-start;
}

.rules :deep(svg) {
  color: var(--color-coral-700);
  margin-top: 2px;
}

.rules ul {
  margin: 0;
  padding-left: var(--space-4);
  display: flex;
  flex-direction: column;
  gap: var(--space-1);
  font-size: var(--font-size-sm);
}

.tags {
  margin-top: 4px;
  gap: 4px;
}

.device {
  max-width: 260px;
}

.agent {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.note {
  max-width: 220px;
}

.actions {
  white-space: nowrap;
}

.confirm {
  display: flex;
  gap: var(--space-3);
  align-items: flex-start;
  padding: var(--space-3);
  border-radius: var(--radius-md);
  background: var(--color-warning-soft);
  font-size: var(--font-size-sm);
  cursor: pointer;
}

.confirm input {
  margin-top: 3px;
  width: 18px;
  height: 18px;
  accent-color: var(--color-primary);
}
</style>
