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
import { alertRuleLabels, alertStatusLabels, riskLabels } from '@/utils/labels'
import { formatDateTime } from '@/utils/format'
import type { ComplianceAlert, ComplianceAlertStatus, PagedResponse } from '@/types/api'

const ui = useUiStore()
const status = ref<ComplianceAlertStatus | ''>('Otvoren')
const page = ref(1)
const result = ref<PagedResponse<ComplianceAlert> | null>(null)
const isLoading = ref(true)
const error = ref<string | null>(null)
const review = reactive({ alert: null as ComplianceAlert | null, status: 'ZatvorenBezNalaza' as ComplianceAlertStatus, note: '', saving: false })

const statusOptions = [{ value: '', label: 'Svi statusi' }, ...Object.entries(alertStatusLabels).map(([value, item]) => ({ value, label: item.label }))]
const reviewOptions = (['UProveri', 'ZatvorenBezNalaza', 'EskaliranSimulacija'] as ComplianceAlertStatus[]).map((value) => ({ value, label: alertStatusLabels[value].label }))

async function load(): Promise<void> {
  isLoading.value = true
  error.value = null
  try {
    result.value = await adminApi.alerts(status.value || undefined, page.value)
  } catch (caught) {
    error.value = errorMessage(caught)
  } finally {
    isLoading.value = false
  }
}

function openReview(alert: ComplianceAlert): void {
  Object.assign(review, { alert, status: 'ZatvorenBezNalaza', note: '' })
}

async function submitReview(): Promise<void> {
  if (!review.alert || !review.note.trim()) {
    ui.notify('Obrazloženje je obavezno.', 'danger')
    return
  }
  review.saving = true
  try {
    await adminApi.reviewAlert(review.alert.id, review.status, review.note.trim())
    review.alert = null
    ui.notify('Upozorenje je obrađeno.')
    await load()
  } catch (caught) {
    ui.notify(errorMessage(caught), 'danger')
  } finally {
    review.saving = false
  }
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
        <h1 class="page-title">AML upozorenja</h1>
        <p class="page-subtitle">Automatska pravila za sprečavanje pranja novca. Upozorenje nije optužba, već signal za proveru.</p>
      </div>
      <AppField v-model="status" class="status-filter" label="Status" :options="statusOptions" />
    </header>

    <AppCard :padded="false">
      <div class="card-body">
        <StatusViews v-if="isLoading" state="loading" :rows="6" />
        <StatusViews v-else-if="error" state="error" :message="error" @retry="load" />
        <StatusViews v-else-if="!result?.items.length" state="empty" icon="check" title="Nema upozorenja" message="Za izabrani status nema stavki." />
        <div v-else class="table-scroll">
          <table class="data-table">
            <thead>
              <tr>
                <th scope="col">Pravilo</th>
                <th scope="col">Klijent</th>
                <th scope="col">Detalji</th>
                <th scope="col">Ozbiljnost</th>
                <th scope="col">Status</th>
                <th scope="col">Vreme</th>
                <th scope="col"><span class="visually-hidden">Radnje</span></th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="alert in result.items" :key="alert.id">
                <td>
                  <strong>{{ alertRuleLabels[alert.ruleCode] ?? alert.ruleCode }}</strong>
                  <div v-if="alert.isSimulation" class="subtle">simulacija</div>
                </td>
                <td>
                  <RouterLink :to="`/bankar/klijenti/${alert.userId}`">{{ alert.customerEmail ?? `#${alert.userId}` }}</RouterLink>
                </td>
                <td class="details">{{ alert.details }}</td>
                <td><AppBadge :tone="riskLabels[alert.severity].tone">{{ alert.severity }}</AppBadge></td>
                <td>
                  <AppBadge :tone="alertStatusLabels[alert.status].tone">{{ alertStatusLabels[alert.status].label }}</AppBadge>
                  <div v-if="alert.reviewNote" class="subtle note">{{ alert.reviewNote }}</div>
                </td>
                <td class="subtle">{{ formatDateTime(alert.createdAt) }}</td>
                <td>
                  <AppButton v-if="alert.status === 'Otvoren' || alert.status === 'UProveri'" size="sm" variant="secondary" @click="openReview(alert)">Obradi</AppButton>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
        <AppPagination v-if="result" :page="result.page" :total-pages="result.totalPages" :total-count="result.totalCount" @change="page = $event" />
      </div>
    </AppCard>

    <AppModal :open="review.alert !== null" title="Obrada upozorenja" width="520px" @close="review.alert = null">
      <form id="review-form" class="stack" @submit.prevent="submitReview">
        <p v-if="review.alert" class="muted">{{ alertRuleLabels[review.alert.ruleCode] ?? review.alert.ruleCode }}: {{ review.alert.details }}</p>
        <AppField v-model="review.status" label="Odluka" :options="reviewOptions" />
        <AppField v-model="review.note" label="Obrazloženje" multiline :rows="4" :maxlength="1000" required />
      </form>
      <template #footer>
        <AppButton variant="ghost" @click="review.alert = null">Odustani</AppButton>
        <AppButton type="submit" form="review-form" :loading="review.saving">Sačuvaj odluku</AppButton>
      </template>
    </AppModal>
  </div>
</template>

<style scoped>
.status-filter {
  min-width: 220px;
}

.details {
  max-width: 360px;
  font-size: var(--font-size-sm);
}

.note {
  margin-top: 4px;
  max-width: 220px;
}
</style>
