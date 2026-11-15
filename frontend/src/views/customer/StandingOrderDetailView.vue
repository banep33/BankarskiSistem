<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useRoute } from 'vue-router'
import AppCard from '@/components/ui/AppCard.vue'
import AppBadge from '@/components/ui/AppBadge.vue'
import AppButton from '@/components/ui/AppButton.vue'
import AppField from '@/components/ui/AppField.vue'
import AppIcon from '@/components/ui/AppIcon.vue'
import AppModal from '@/components/ui/AppModal.vue'
import MoneyAmount from '@/components/ui/MoneyAmount.vue'
import StatusViews from '@/components/ui/StatusViews.vue'
import { helpApi, standingOrdersApi } from '@/api/bank'
import { useReauthStore } from '@/stores/reauth'
import { useUiStore } from '@/stores/ui'
import { errorMessage } from '@/composables/useAsync'
import { executionStatusLabels, frequencyLabels, standingOrderStatusLabels } from '@/utils/labels'
import { formatAccountNumber, formatDate, formatDateTime, toIsoDate } from '@/utils/format'
import type { ReferenceData, StandingOrderDetail } from '@/types/api'

const route = useRoute()
const reauth = useReauthStore()
const ui = useUiStore()
const detail = ref<StandingOrderDetail | null>(null)
const referenceData = ref<ReferenceData | null>(null)
const isLoading = ref(true)
const error = ref<string | null>(null)
const isActing = ref(false)
const confirmCancel = ref(false)
const edit = reactive({
  open: false,
  amount: '' as string | number,
  purpose: '',
  referenceModel: '',
  referenceNumber: '',
  endMode: 'none' as 'none' | 'date' | 'count',
  endDate: '',
  maxExecutions: '' as string | number,
  error: null as string | null,
  saving: false,
})

const order = computed(() => detail.value?.order ?? null)
const isOpen = computed(() => order.value?.status === 'Aktivan' || order.value?.status === 'Pauziran')
const modelOptions = computed(() => [{ value: '', label: 'Bez modela' }, ...(referenceData.value?.referenceModels ?? []).map((model) => ({ value: model, label: `Model ${model}` }))])
const endOptions = [
  { value: 'none', label: 'Bez kraja' },
  { value: 'date', label: 'Do datuma' },
  { value: 'count', label: 'Ukupan broj plaćanja' },
]
const endDescription = computed(() => {
  if (!order.value) return ''
  if (order.value.endDate) return `do ${formatDate(order.value.endDate)}`
  if (order.value.maxExecutions) return `ukupno ${order.value.maxExecutions} plaćanja`
  return 'bez kraja'
})

async function load(): Promise<void> {
  isLoading.value = true
  error.value = null
  try {
    ;[detail.value, referenceData.value] = await Promise.all([standingOrdersApi.get(Number(route.params.id)), referenceData.value ?? helpApi.referenceData()])
  } catch (caught) {
    error.value = errorMessage(caught)
  } finally {
    isLoading.value = false
  }
}

async function act(action: 'pause' | 'resume' | 'cancel'): Promise<void> {
  if (!order.value) return
  isActing.value = true
  try {
    await standingOrdersApi[action](order.value.id)
    ui.notify(action === 'pause' ? 'Trajni nalog je pauziran.' : action === 'resume' ? 'Trajni nalog je ponovo aktivan.' : 'Trajni nalog je otkazan.')
    confirmCancel.value = false
    await load()
  } catch (caught) {
    ui.notify(errorMessage(caught), 'danger')
  } finally {
    isActing.value = false
  }
}

function openEdit(): void {
  if (!order.value) return
  Object.assign(edit, {
    open: true,
    amount: order.value.amount,
    purpose: order.value.purpose,
    referenceModel: order.value.referenceModel ?? '',
    referenceNumber: order.value.paymentReferenceNumber ?? '',
    endMode: order.value.endDate ? 'date' : order.value.maxExecutions ? 'count' : 'none',
    endDate: order.value.endDate ?? '',
    maxExecutions: order.value.maxExecutions ?? '',
    error: null,
  })
}

async function saveEdit(): Promise<void> {
  if (!order.value) return
  const amount = Number(String(edit.amount).replace(',', '.'))
  if (!Number.isFinite(amount) || amount <= 0) {
    edit.error = 'Unesite iznos veći od nule.'
    return
  }
  edit.saving = true
  edit.error = null
  try {
    const id = order.value.id
    const saved = await reauth.runWithReauth(
      () =>
        standingOrdersApi.update(id, {
          amount,
          purpose: edit.purpose.trim(),
          referenceModel: edit.referenceModel || null,
          referenceNumber: edit.referenceNumber.trim() || null,
          endDate: edit.endMode === 'date' ? edit.endDate || null : null,
          maxExecutions: edit.endMode === 'count' ? Number(edit.maxExecutions) || null : null,
        }),
      'Izmena trajnog naloga zahteva potvrdu lozinkom.',
    )
    if (!saved) return
    edit.open = false
    ui.notify('Trajni nalog je izmenjen.')
    await load()
  } catch (caught) {
    edit.error = errorMessage(caught)
  } finally {
    edit.saving = false
  }
}

onMounted(load)
</script>

<template>
  <div class="page">
    <StatusViews v-if="isLoading" state="loading" :rows="5" />
    <StatusViews v-else-if="error" state="error" :message="error" @retry="load" />

    <template v-else-if="order && detail">
      <header class="page-header">
        <div>
          <RouterLink to="/trajni-nalozi" class="back">← Trajni nalozi</RouterLink>
          <h1 class="page-title">{{ order.recipientName }}</h1>
          <p class="page-subtitle"><span class="mono">{{ order.referenceNumber }}</span> · {{ order.purpose }}</p>
        </div>
        <div class="row actions">
          <AppBadge :tone="standingOrderStatusLabels[order.status].tone">{{ standingOrderStatusLabels[order.status].label }}</AppBadge>
          <template v-if="isOpen">
            <AppButton variant="secondary" icon="edit" @click="openEdit">Izmeni</AppButton>
            <AppButton v-if="order.status === 'Aktivan'" variant="secondary" :loading="isActing" @click="act('pause')">Pauziraj</AppButton>
            <AppButton v-else variant="secondary" icon="refresh" :loading="isActing" @click="act('resume')">Nastavi</AppButton>
            <AppButton variant="ghost" @click="confirmCancel = true">Otkaži</AppButton>
          </template>
        </div>
      </header>

      <div v-if="order.consecutiveFailures > 0 || order.status === 'Pauziran'" class="warning" role="status">
        <AppIcon name="alert" />
        <span>
          <template v-if="order.status === 'Pauziran' && order.consecutiveFailures >= 3">
            Nalog je automatski pauziran posle {{ order.consecutiveFailures }} neuspešna pokušaja. Poslednji razlog: {{ order.lastFailureReason }}. Kada obezbedite sredstva, kliknite Nastavi.
          </template>
          <template v-else-if="order.status === 'Pauziran'">Nalog je pauziran i neće se izvršavati dok ga ne nastavite. Propušteni datumi se ne nadoknađuju.</template>
          <template v-else>Poslednje plaćanje nije izvršeno: {{ order.lastFailureReason }}</template>
        </span>
      </div>

      <div class="summary">
        <AppCard tone="dark">
          <p class="label">Iznos</p>
          <p class="big amount-fit"><MoneyAmount :amount="order.amount" :currency="order.currency" /></p>
          <p class="label">{{ frequencyLabels[order.frequency].adverb }}, {{ endDescription }}</p>
        </AppCard>
        <AppCard title="Sledeća plaćanja">
          <ol v-if="order.upcomingDates.length" class="dates">
            <li v-for="date in order.upcomingDates" :key="date" :class="{ first: date === order.nextExecutionDate }">
              {{ formatDate(date) }}<span v-if="date === toIsoDate(new Date())" class="today">danas</span>
            </li>
          </ol>
          <p v-else class="muted">Nema planiranih plaćanja.</p>
        </AppCard>
        <AppCard title="Podaci">
          <dl class="facts">
            <div><dt>Sa računa</dt><dd class="mono">{{ order.fromAccountNumber ? formatAccountNumber(order.fromAccountNumber) : '' }}</dd></div>
            <div><dt>Račun primaoca</dt><dd class="mono">{{ formatAccountNumber(order.recipientAccountNumber) }}</dd></div>
            <div><dt>Šifra plaćanja</dt><dd>{{ order.paymentCode }}</dd></div>
            <div v-if="order.paymentReferenceNumber"><dt>Poziv na broj</dt><dd class="mono">{{ order.referenceModel ? `(${order.referenceModel}) ` : '' }}{{ order.paymentReferenceNumber }}</dd></div>
            <div><dt>Prvo plaćanje</dt><dd>{{ formatDate(order.startDate) }}</dd></div>
            <div><dt>Izvršeno puta</dt><dd>{{ order.executionsCount }}</dd></div>
          </dl>
        </AppCard>
      </div>

      <AppCard title="Istorija izvršenja" :padded="false">
        <div class="card-body">
          <StatusViews v-if="!detail.executions.length" state="empty" icon="calendar" title="Još nema izvršenja" message="Prvo plaćanje se izvršava na dan dospeća." />
          <div v-else class="table-scroll">
            <table class="data-table">
              <thead>
                <tr>
                  <th scope="col">Datum dospeća</th>
                  <th scope="col">Status</th>
                  <th scope="col">Referenca ili razlog</th>
                  <th scope="col">Obrađeno</th>
                  <th scope="col" class="numeric">Naknada</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="execution in detail.executions" :key="execution.id">
                  <td>{{ formatDate(execution.scheduledFor) }}</td>
                  <td><AppBadge :tone="executionStatusLabels[execution.status].tone">{{ executionStatusLabels[execution.status].label }}</AppBadge></td>
                  <td :class="{ reason: execution.failureReason }">
                    <span v-if="execution.transactionReference" class="mono">{{ execution.transactionReference }}</span>
                    <span v-else>{{ execution.failureReason }}</span>
                  </td>
                  <td class="subtle">{{ formatDateTime(execution.completedAt) }}</td>
                  <td class="numeric">
                    <MoneyAmount v-if="execution.feeAmount !== null" :amount="execution.feeAmount" :currency="order.currency" :sensitive="false" />
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </AppCard>
    </template>

    <AppModal :open="edit.open" title="Izmena trajnog naloga" width="560px" @close="edit.open = false">
      <form id="standing-edit" class="stack" @submit.prevent="saveEdit">
        <p class="muted">Primalac, račun i učestalost se ne menjaju. Za njih otkažite nalog i napravite novi.</p>
        <AppField v-model="edit.amount" :label="`Iznos (${order?.currency ?? 'RSD'})`" inputmode="decimal" required />
        <AppField v-model="edit.purpose" label="Svrha" :maxlength="140" required />
        <div class="grid-2">
          <AppField v-model="edit.referenceModel" label="Model" :options="modelOptions" />
          <AppField v-model="edit.referenceNumber" label="Poziv na broj" :maxlength="23" />
        </div>
        <AppField v-model="edit.endMode" label="Kraj" :options="endOptions" />
        <AppField v-if="edit.endMode === 'date'" v-model="edit.endDate" label="Poslednje plaćanje najkasnije" type="date" />
        <AppField v-if="edit.endMode === 'count'" v-model="edit.maxExecutions" label="Ukupan broj plaćanja" type="number" min="1" max="600" />
        <p v-if="edit.error" class="form-error" role="alert">{{ edit.error }}</p>
      </form>
      <template #footer>
        <AppButton variant="ghost" @click="edit.open = false">Odustani</AppButton>
        <AppButton type="submit" form="standing-edit" :loading="edit.saving">Sačuvaj</AppButton>
      </template>
    </AppModal>

    <AppModal :open="confirmCancel" title="Otkazati trajni nalog?" width="440px" @close="confirmCancel = false">
      <p class="muted">Plaćanja primaocu {{ order?.recipientName }} više se neće izvršavati. Već izvršena plaćanja ostaju u istoriji. Otkazivanje se ne može poništiti.</p>
      <template #footer>
        <AppButton variant="ghost" @click="confirmCancel = false">Ne</AppButton>
        <AppButton variant="danger" :loading="isActing" @click="act('cancel')">Da, otkaži</AppButton>
      </template>
    </AppModal>
  </div>
</template>

<style scoped>
.back {
  font-size: var(--font-size-sm);
  color: var(--color-text-muted);
  text-decoration: none;
}

.actions {
  flex-wrap: wrap;
}

.warning {
  display: flex;
  gap: var(--space-3);
  align-items: flex-start;
  padding: var(--space-4);
  border-radius: var(--radius-md);
  background: var(--color-warning-soft);
  color: var(--color-warning);
  font-size: var(--font-size-sm);
  font-weight: 500;
}

.summary {
  display: grid;
  grid-template-columns: 1fr 1fr 1.3fr;
  gap: var(--space-5);
  align-items: start;
}

.label {
  font-size: var(--font-size-sm);
  color: var(--color-emphasis-text-muted);
}

.big {
  --amount-max: var(--font-size-3xl);
  margin: var(--space-2) 0;
  font-weight: 600;
}

.dates {
  margin: 0;
  padding-left: var(--space-5);
  display: flex;
  flex-direction: column;
  gap: var(--space-2);
}

.dates .first {
  font-weight: 700;
  color: var(--color-link);
}

.today {
  margin-left: var(--space-2);
  padding: 1px 8px;
  border-radius: var(--radius-pill);
  background: var(--color-lime-400);
  color: var(--color-forest-900);
  font-size: var(--font-size-xs);
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
  font-size: var(--font-size-sm);
}

.facts dt {
  color: var(--color-text-muted);
}

.facts dd {
  margin: 0;
  font-weight: 600;
  text-align: right;
}

.reason {
  color: var(--color-danger);
  font-size: var(--font-size-sm);
}

.numeric {
  text-align: right;
}

.form-error {
  color: var(--color-danger);
  font-size: var(--font-size-sm);
}

@media (max-width: 1100px) {
  .summary {
    grid-template-columns: minmax(0, 1fr);
  }
}
</style>
