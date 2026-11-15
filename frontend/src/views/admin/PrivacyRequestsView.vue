<script setup lang="ts">
import { onMounted, reactive, ref, watch } from 'vue'
import AppCard from '@/components/ui/AppCard.vue'
import AppBadge from '@/components/ui/AppBadge.vue'
import AppButton from '@/components/ui/AppButton.vue'
import AppField from '@/components/ui/AppField.vue'
import AppModal from '@/components/ui/AppModal.vue'
import AppPagination from '@/components/ui/AppPagination.vue'
import StatusViews from '@/components/ui/StatusViews.vue'
import { adminApi } from '@/api/bank'
import { useUiStore } from '@/stores/ui'
import { errorMessage } from '@/composables/useAsync'
import { privacyRequestStatusLabels, privacyRequestTypeLabels } from '@/utils/labels'
import { formatDate } from '@/utils/format'
import type { PagedResponse, PrivacyRequest, PrivacyRequestStatus } from '@/types/api'

const ui = useUiStore()
const status = ref<PrivacyRequestStatus | ''>('')
const page = ref(1)
const result = ref<PagedResponse<PrivacyRequest> | null>(null)
const isLoading = ref(true)
const error = ref<string | null>(null)
const respond = reactive({ request: null as PrivacyRequest | null, status: 'Usvojen' as PrivacyRequestStatus, response: '', saving: false })

const statusOptions = [{ value: '', label: 'Svi statusi' }, ...Object.entries(privacyRequestStatusLabels).map(([value, item]) => ({ value, label: item.label }))]
const respondOptions = (['UObradi', 'Usvojen', 'DelimicnoUsvojen', 'Odbijen'] as PrivacyRequestStatus[]).map((value) => ({ value, label: privacyRequestStatusLabels[value].label }))

async function load(): Promise<void> {
  isLoading.value = true
  error.value = null
  try {
    result.value = await adminApi.privacyRequests(status.value || undefined, page.value)
  } catch (caught) {
    error.value = errorMessage(caught)
  } finally {
    isLoading.value = false
  }
}

function open(request: PrivacyRequest): void {
  Object.assign(respond, { request, status: 'Usvojen', response: '' })
}

async function submit(): Promise<void> {
  if (!respond.request) return
  if (respond.status !== 'UObradi' && respond.response.trim().length < 10) {
    ui.notify('Unesite odgovor podnosiocu (najmanje 10 karaktera).', 'danger')
    return
  }
  respond.saving = true
  try {
    await adminApi.respondPrivacyRequest(respond.request.id, respond.status, respond.response.trim())
    respond.request = null
    ui.notify('Odgovor je sačuvan i podnosilac je obavešten.')
    await load()
  } catch (caught) {
    ui.notify(errorMessage(caught), 'danger')
  } finally {
    respond.saving = false
  }
}

function isOverdue(request: PrivacyRequest): boolean {
  return ['Podnet', 'UObradi'].includes(request.status) && new Date(request.dueAt) < new Date()
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
        <h1 class="page-title">Zahtevi po ZZPL</h1>
        <p class="page-subtitle">Zahtevi lica na koja se podaci odnose. Rok za odgovor je 30 dana od prijema.</p>
      </div>
      <AppField v-model="status" class="status-filter" label="Status" :options="statusOptions" />
    </header>

    <AppCard :padded="false">
      <div class="card-body">
        <StatusViews v-if="isLoading" state="loading" :rows="5" />
        <StatusViews v-else-if="error" state="error" :message="error" @retry="load" />
        <StatusViews v-else-if="!result?.items.length" state="empty" icon="lock" title="Nema zahteva" />
        <div v-else class="table-scroll">
          <table class="data-table">
            <thead>
              <tr>
                <th scope="col">Zahtev</th>
                <th scope="col">Podnosilac</th>
                <th scope="col">Opis</th>
                <th scope="col">Rok</th>
                <th scope="col">Status</th>
                <th scope="col"><span class="visually-hidden">Radnje</span></th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="request in result.items" :key="request.id">
                <td>
                  <strong>{{ privacyRequestTypeLabels[request.requestType] }}</strong>
                  <div class="subtle mono">{{ request.referenceNumber }}</div>
                </td>
                <td>{{ request.customerEmail }}</td>
                <td class="details">{{ request.details }}</td>
                <td :class="{ late: isOverdue(request) }">{{ formatDate(request.dueAt) }}</td>
                <td><AppBadge :tone="privacyRequestStatusLabels[request.status].tone">{{ privacyRequestStatusLabels[request.status].label }}</AppBadge></td>
                <td>
                  <AppButton v-if="request.status === 'Podnet' || request.status === 'UObradi'" size="sm" variant="secondary" @click="open(request)">Odgovori</AppButton>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
        <AppPagination v-if="result" :page="result.page" :total-pages="result.totalPages" :total-count="result.totalCount" @change="page = $event" />
      </div>
    </AppCard>

    <AppModal :open="respond.request !== null" title="Odgovor na zahtev" width="560px" @close="respond.request = null">
      <form id="privacy-response" class="stack" @submit.prevent="submit">
        <p v-if="respond.request" class="muted">{{ respond.request.details }}</p>
        <AppField v-model="respond.status" label="Odluka" :options="respondOptions" />
        <AppField v-model="respond.response" label="Odgovor podnosiocu" multiline :rows="5" :maxlength="10000" hint="Kod odbijanja navedite pravni osnov, npr. obavezu čuvanja podataka po Zakonu o sprečavanju pranja novca." />
      </form>
      <template #footer>
        <AppButton variant="ghost" @click="respond.request = null">Odustani</AppButton>
        <AppButton type="submit" form="privacy-response" :loading="respond.saving">Sačuvaj</AppButton>
      </template>
    </AppModal>
  </div>
</template>

<style scoped>
.card-body {
  padding: var(--space-5) var(--space-6);
}

.status-filter {
  min-width: 220px;
}

.details {
  max-width: 380px;
  font-size: var(--font-size-sm);
}

.late {
  color: var(--color-danger);
  font-weight: 600;
}
</style>
