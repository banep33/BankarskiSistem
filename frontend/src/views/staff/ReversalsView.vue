<script setup lang="ts">
import { onMounted, reactive, ref, watch } from 'vue'
import AppCard from '@/components/ui/AppCard.vue'
import AppBadge from '@/components/ui/AppBadge.vue'
import AppButton from '@/components/ui/AppButton.vue'
import AppField from '@/components/ui/AppField.vue'
import AppModal from '@/components/ui/AppModal.vue'
import AppPagination from '@/components/ui/AppPagination.vue'
import MoneyAmount from '@/components/ui/MoneyAmount.vue'
import StatusViews from '@/components/ui/StatusViews.vue'
import { reversalsApi } from '@/api/bank'
import { useAuthStore } from '@/stores/auth'
import { useUiStore } from '@/stores/ui'
import { errorMessage } from '@/composables/useAsync'
import { reversalStatusLabels, transactionTypeLabels } from '@/utils/labels'
import { formatAccountNumber, formatDateTime } from '@/utils/format'
import type { PagedResponse, ReversalStatus, TransactionReversal } from '@/types/api'

const auth = useAuthStore()
const ui = useUiStore()
const status = ref<ReversalStatus | ''>('Podnet')
const page = ref(1)
const result = ref<PagedResponse<TransactionReversal> | null>(null)
const isLoading = ref(true)
const error = ref<string | null>(null)
const decision = reactive({ item: null as TransactionReversal | null, mode: 'approve' as 'approve' | 'reject', note: '', saving: false })

const statusOptions = [{ value: '', label: 'Svi statusi' }, ...Object.entries(reversalStatusLabels).map(([value, item]) => ({ value, label: item.label }))]

async function load(): Promise<void> {
  isLoading.value = true
  error.value = null
  try {
    result.value = await reversalsApi.list(status.value || undefined, page.value)
  } catch (caught) {
    error.value = errorMessage(caught)
  } finally {
    isLoading.value = false
  }
}

function open(item: TransactionReversal, mode: 'approve' | 'reject'): void {
  Object.assign(decision, { item, mode, note: '' })
}

async function submit(): Promise<void> {
  if (!decision.item) return
  if (decision.mode === 'reject' && decision.note.trim().length < 5) {
    ui.notify('Unesite razlog odbijanja.', 'danger')
    return
  }
  decision.saving = true
  try {
    if (decision.mode === 'approve') {
      await reversalsApi.approve(decision.item.id, decision.note.trim() || null)
      ui.notify('Transakcija je stornirana, a stanja računa su vraćena.')
    } else {
      await reversalsApi.reject(decision.item.id, decision.note.trim())
      ui.notify('Zahtev je odbijen.')
    }
    decision.item = null
    await load()
  } catch (caught) {
    ui.notify(errorMessage(caught), 'danger')
  } finally {
    decision.saving = false
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
        <h1 class="page-title">Storniranja</h1>
        <p class="page-subtitle">
          Ispravka pogrešnih transakcija po principu četiri oka: bankar podnosi zahtev iz detalja transakcije, a administrator koji ga nije podneo odobrava.
        </p>
      </div>
      <AppField v-model="status" class="status-filter" label="Status" :options="statusOptions" />
    </header>

    <AppCard :padded="false">
      <div class="card-body">
        <StatusViews v-if="isLoading" state="loading" :rows="5" />
        <StatusViews v-else-if="error" state="error" :message="error" @retry="load" />
        <StatusViews v-else-if="!result?.items.length" state="empty" icon="refresh" title="Nema zahteva" message="Zahtev se podnosi iz detalja transakcije u odeljku Transakcije." />
        <div v-else class="table-scroll">
          <table class="data-table">
            <thead>
              <tr>
                <th scope="col">Transakcija</th>
                <th scope="col">Računi</th>
                <th scope="col">Razlog</th>
                <th scope="col">Podneo</th>
                <th scope="col">Status</th>
                <th scope="col" class="numeric">Iznos</th>
                <th scope="col"><span class="visually-hidden">Radnje</span></th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="item in result.items" :key="item.id">
                <td>
                  <span class="mono">{{ item.originalReference }}</span>
                  <div class="subtle">{{ item.originalType ? transactionTypeLabels[item.originalType] : '' }}, {{ formatDateTime(item.originalCreatedAt) }}</div>
                </td>
                <td class="mono small">
                  <div v-if="item.fromAccountNumber">sa {{ formatAccountNumber(item.fromAccountNumber) }}</div>
                  <div v-if="item.toAccountNumber">na {{ formatAccountNumber(item.toAccountNumber) }}</div>
                </td>
                <td class="reason">{{ item.reason }}</td>
                <td>
                  {{ item.requestedBy }}
                  <div class="subtle">{{ formatDateTime(item.createdAt) }}</div>
                </td>
                <td>
                  <AppBadge :tone="reversalStatusLabels[item.status].tone">{{ reversalStatusLabels[item.status].label }}</AppBadge>
                  <div v-if="item.reviewedBy" class="subtle">{{ item.reviewedBy }}</div>
                  <div v-if="item.reviewNote" class="subtle small">{{ item.reviewNote }}</div>
                </td>
                <td class="numeric">
                  <MoneyAmount v-if="item.amount !== null" :amount="item.amount" :currency="item.currency ?? 'RSD'" :sensitive="false" />
                </td>
                <td class="actions">
                  <template v-if="item.status === 'Podnet' && auth.isAdministrator">
                    <span v-if="item.requestedBy === auth.fullName" class="subtle">Odlučuje drugi administrator</span>
                    <template v-else>
                      <AppButton size="sm" icon="check" @click="open(item, 'approve')">Odobri</AppButton>
                      <AppButton size="sm" variant="ghost" @click="open(item, 'reject')">Odbij</AppButton>
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

    <AppModal :open="decision.item !== null" :title="decision.mode === 'approve' ? 'Odobravanje storniranja' : 'Odbijanje zahteva'" width="500px" @close="decision.item = null">
      <form id="reversal-decision" class="stack" @submit.prevent="submit">
        <p v-if="decision.item" class="muted">
          {{ decision.item.originalReference }}: {{ decision.item.reason }}
        </p>
        <p v-if="decision.mode === 'approve'" class="muted">
          Pravi se protivstavka koja poništava sva knjiženja originalne transakcije. Ako na računu primaoca više nema dovoljno sredstava, storniranje neće biti moguće.
        </p>
        <AppField v-model="decision.note" :label="decision.mode === 'approve' ? 'Napomena (nije obavezno)' : 'Razlog odbijanja'" multiline :rows="3" :maxlength="500" />
      </form>
      <template #footer>
        <AppButton variant="ghost" @click="decision.item = null">Odustani</AppButton>
        <AppButton type="submit" form="reversal-decision" :variant="decision.mode === 'approve' ? 'danger' : 'secondary'" :loading="decision.saving">
          {{ decision.mode === 'approve' ? 'Storniraj' : 'Odbij zahtev' }}
        </AppButton>
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

.reason {
  max-width: 280px;
  font-size: var(--font-size-sm);
}

.small {
  font-size: var(--font-size-xs);
}

.numeric {
  text-align: right;
}

.actions {
  display: flex;
  gap: var(--space-1);
  justify-content: flex-end;
}
</style>
