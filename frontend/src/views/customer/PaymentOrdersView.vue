<script setup lang="ts">
import { onMounted, ref, watch } from 'vue'
import { RouterLink } from 'vue-router'
import AppCard from '@/components/ui/AppCard.vue'
import AppBadge from '@/components/ui/AppBadge.vue'
import AppButton from '@/components/ui/AppButton.vue'
import AppModal from '@/components/ui/AppModal.vue'
import AppPagination from '@/components/ui/AppPagination.vue'
import MoneyAmount from '@/components/ui/MoneyAmount.vue'
import StatusViews from '@/components/ui/StatusViews.vue'
import { paymentsApi } from '@/api/bank'
import { useUiStore } from '@/stores/ui'
import { errorMessage } from '@/composables/useAsync'
import { paymentStatusLabels } from '@/utils/labels'
import { formatAccountNumber, formatDate, formatDateTime } from '@/utils/format'
import type { PagedResponse, PaymentOrder, PaymentOrderStatus } from '@/types/api'

const ui = useUiStore()
const status = ref<PaymentOrderStatus | ''>('')
const page = ref(1)
const result = ref<PagedResponse<PaymentOrder> | null>(null)
const isLoading = ref(true)
const error = ref<string | null>(null)
const toCancel = ref<PaymentOrder | null>(null)
const isCancelling = ref(false)

const filters: { value: PaymentOrderStatus | ''; label: string }[] = [
  { value: '', label: 'Svi' },
  { value: 'Zakazan', label: 'Zakazani' },
  { value: 'UObradi', label: 'U kliringu' },
  { value: 'Izvrsen', label: 'Izvršeni' },
  { value: 'Odbijen', label: 'Odbijeni' },
  { value: 'Otkazan', label: 'Otkazani' },
]

async function load(): Promise<void> {
  isLoading.value = true
  error.value = null
  try {
    result.value = await paymentsApi.list(status.value || undefined, page.value)
  } catch (caught) {
    error.value = errorMessage(caught)
  } finally {
    isLoading.value = false
  }
}

async function cancel(): Promise<void> {
  if (!toCancel.value) return
  isCancelling.value = true
  try {
    await paymentsApi.cancel(toCancel.value.id)
    ui.notify('Zakazani nalog je otkazan. Novac nije skinut sa računa.')
    toCancel.value = null
    await load()
  } catch (caught) {
    ui.notify(errorMessage(caught), 'danger')
  } finally {
    isCancelling.value = false
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
        <h1 class="page-title">Nalozi za plaćanje</h1>
        <p class="page-subtitle">Zakazana, izvršena i odbijena plaćanja. Zakazani nalog možete otkazati do dana izvršenja.</p>
      </div>
      <RouterLink to="/placanja/novo" class="new-link">Novo plaćanje</RouterLink>
    </header>

    <div class="filter-chips" role="group" aria-label="Filter po statusu">
      <button v-for="filter in filters" :key="filter.value" type="button" :aria-pressed="status === filter.value" :class="{ active: status === filter.value }" @click="status = filter.value">
        {{ filter.label }}
      </button>
    </div>

    <AppCard :padded="false">
      <div class="card-body">
        <StatusViews v-if="isLoading" state="loading" :rows="5" />
        <StatusViews v-else-if="error" state="error" :message="error" @retry="load" />
        <StatusViews v-else-if="!result?.items.length" state="empty" icon="file" title="Nema naloga" message="Kada pošaljete ili zakažete plaćanje, pojaviće se ovde." />
        <div v-else class="table-scroll">
          <table class="data-table">
            <thead>
              <tr>
                <th scope="col">Primalac</th>
                <th scope="col">Svrha</th>
                <th scope="col">Datum</th>
                <th scope="col">Status</th>
                <th scope="col" class="numeric">Ukupno</th>
                <th scope="col"><span class="visually-hidden">Radnje</span></th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="order in result.items" :key="order.id">
                <td>
                  <strong>{{ order.recipientName }}</strong>
                  <div class="subtle mono">{{ formatAccountNumber(order.recipientAccountNumber) }}</div>
                </td>
                <td>{{ order.purpose }}</td>
                <td>
                  <template v-if="order.status === 'Zakazan' && order.scheduledFor">Zakazano za {{ formatDate(order.scheduledFor) }}</template>
                  <template v-else>{{ formatDateTime(order.executedAt ?? order.createdAt) }}</template>
                </td>
                <td>
                  <AppBadge :tone="paymentStatusLabels[order.status].tone">{{ paymentStatusLabels[order.status].label }}</AppBadge>
                  <div v-if="order.rejectionReason" class="reason">{{ order.rejectionReason }}</div>
                </td>
                <td class="numeric">
                  <MoneyAmount :amount="order.totalDebit" :currency="order.currency" />
                  <div v-if="order.feeAmount > 0" class="subtle">
                    naknada <MoneyAmount :amount="order.feeAmount" :currency="order.currency" />
                  </div>
                </td>
                <td>
                  <AppButton v-if="order.status === 'Zakazan'" variant="ghost" size="sm" @click="toCancel = order">Otkaži</AppButton>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
        <AppPagination v-if="result" :page="result.page" :total-pages="result.totalPages" :total-count="result.totalCount" @change="page = $event" />
      </div>
    </AppCard>

    <AppModal :open="toCancel !== null" title="Otkazati zakazani nalog?" width="440px" @close="toCancel = null">
      <p v-if="toCancel" class="muted">
        Nalog primaocu <strong>{{ toCancel.recipientName }}</strong> na iznos
        <MoneyAmount :amount="toCancel.amount" :currency="toCancel.currency" :sensitive="false" /> neće biti izvršen.
      </p>
      <template #footer>
        <AppButton variant="ghost" @click="toCancel = null">Ne</AppButton>
        <AppButton variant="danger" :loading="isCancelling" @click="cancel">Da, otkaži</AppButton>
      </template>
    </AppModal>
  </div>
</template>

<style scoped>
.card-body {
  padding: var(--space-5) var(--space-6);
}

.new-link {
  padding: 0.65rem 1.2rem;
  border-radius: var(--radius-pill);
  background: var(--color-primary);
  color: #fff;
  font-weight: 600;
  text-decoration: none;
}

.filter-chips {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-2);
}

.filter-chips button {
  padding: 0.45rem 1rem;
  border: 1px solid var(--color-border-strong);
  border-radius: var(--radius-pill);
  background: var(--color-surface);
  font-weight: 500;
  font-size: var(--font-size-sm);
  cursor: pointer;
}

.filter-chips button.active {
  background: var(--color-forest-900);
  border-color: var(--color-forest-900);
  color: #fff;
}

.numeric {
  text-align: right;
}

.reason {
  margin-top: 4px;
  font-size: var(--font-size-xs);
  color: var(--color-danger);
}
</style>
