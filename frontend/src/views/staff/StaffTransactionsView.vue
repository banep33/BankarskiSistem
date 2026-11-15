<script setup lang="ts">
import { onMounted, reactive, ref, watch } from 'vue'
import AppCard from '@/components/ui/AppCard.vue'
import AppField from '@/components/ui/AppField.vue'
import AppButton from '@/components/ui/AppButton.vue'
import AppBadge from '@/components/ui/AppBadge.vue'
import AppPagination from '@/components/ui/AppPagination.vue'
import MoneyAmount from '@/components/ui/MoneyAmount.vue'
import StatusViews from '@/components/ui/StatusViews.vue'
import TransactionDetailModal from '@/components/banking/TransactionDetailModal.vue'
import { transactionsApi } from '@/api/bank'
import { errorMessage } from '@/composables/useAsync'
import { transactionTypeLabels } from '@/utils/labels'
import { formatAccountNumber, formatDateTime } from '@/utils/format'
import type { PagedResponse, Transaction, TransactionStatus, TransactionType } from '@/types/api'

const filters = reactive({ search: '', type: '' as TransactionType | '', status: '' as TransactionStatus | '', from: '', to: '' })
const page = ref(1)
const result = ref<PagedResponse<Transaction> | null>(null)
const isLoading = ref(true)
const error = ref<string | null>(null)
const selected = ref<Transaction | null>(null)

const typeOptions = [{ value: '', label: 'Sve vrste' }, ...Object.entries(transactionTypeLabels).map(([value, label]) => ({ value, label }))]
const statusOptions = [
  { value: '', label: 'Svi statusi' },
  { value: 'Uspesna', label: 'Uspešne' },
  { value: 'NaCekanju', label: 'Na čekanju' },
  { value: 'Neuspesna', label: 'Neuspešne' },
]

async function load(): Promise<void> {
  isLoading.value = true
  error.value = null
  try {
    result.value = await transactionsApi.list({
      search: filters.search.trim() || undefined,
      type: filters.type || undefined,
      status: filters.status || undefined,
      from: filters.from || undefined,
      to: filters.to || undefined,
      page: page.value,
    })
  } catch (caught) {
    error.value = errorMessage(caught)
  } finally {
    isLoading.value = false
  }
}

function search(): void {
  page.value = 1
  load()
}

watch(page, load)
onMounted(load)
</script>

<template>
  <div class="page">
    <header class="page-header">
      <div>
        <h1 class="page-title">Transakcije</h1>
        <p class="page-subtitle">Pregled prometa svih klijenata za potrebe podrške i kontrole.</p>
      </div>
    </header>

    <AppCard>
      <form class="filters" @submit.prevent="search">
        <AppField v-model="filters.search" label="Pretraga" type="search" placeholder="Referenca, račun ili opis" />
        <AppField v-model="filters.type" label="Vrsta" :options="typeOptions" />
        <AppField v-model="filters.status" label="Status" :options="statusOptions" />
        <AppField v-model="filters.from" label="Od" type="date" />
        <AppField v-model="filters.to" label="Do" type="date" />
        <AppButton type="submit" icon="search">Traži</AppButton>
      </form>
    </AppCard>

    <AppCard :padded="false">
      <div class="card-body">
        <StatusViews v-if="isLoading" state="loading" :rows="8" />
        <StatusViews v-else-if="error" state="error" :message="error" @retry="load" />
        <StatusViews v-else-if="!result?.items.length" state="empty" icon="list" title="Nema transakcija" />
        <div v-else class="table-scroll">
          <table class="data-table">
            <thead>
              <tr>
                <th scope="col">Referenca</th>
                <th scope="col">Vreme</th>
                <th scope="col">Vrsta</th>
                <th scope="col">Sa računa</th>
                <th scope="col">Na račun</th>
                <th scope="col">Status</th>
                <th scope="col" class="numeric">Iznos</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="transaction in result.items" :key="transaction.id" class="clickable" @click="selected = transaction">
                <td>
                  <button type="button" class="link-button mono" @click.stop="selected = transaction">{{ transaction.referenceNumber }}</button>
                </td>
                <td class="subtle">{{ formatDateTime(transaction.createdAt) }}</td>
                <td>{{ transactionTypeLabels[transaction.type] }}</td>
                <td class="mono">{{ transaction.fromAccountNumber ? formatAccountNumber(transaction.fromAccountNumber) : 'Šalter' }}</td>
                <td class="mono">
                  {{
                    transaction.toAccountNumber
                      ? formatAccountNumber(transaction.toAccountNumber)
                      : transaction.counterpartyAccountNumber
                        ? formatAccountNumber(transaction.counterpartyAccountNumber)
                        : 'Isplata'
                  }}
                </td>
                <td>
                  <AppBadge v-if="transaction.reversedAt" tone="neutral">Stornirana</AppBadge>
                  <AppBadge v-else :tone="transaction.status === 'Uspesna' ? 'success' : transaction.status === 'NaCekanju' ? 'warning' : 'danger'">
                    {{ transaction.status === 'Uspesna' ? 'Uspešna' : transaction.status === 'NaCekanju' ? 'Na čekanju' : 'Neuspešna' }}
                  </AppBadge>
                </td>
                <td class="numeric"><MoneyAmount :amount="transaction.amount" :currency="transaction.currency" :sensitive="false" /></td>
              </tr>
            </tbody>
          </table>
        </div>
        <AppPagination v-if="result" :page="result.page" :total-pages="result.totalPages" :total-count="result.totalCount" @change="page = $event" />
      </div>
    </AppCard>

    <TransactionDetailModal :transaction="selected" allow-reversal-request @close="selected = null" @changed="load" />
  </div>
</template>

<style scoped>
.filters {
  display: grid;
  grid-template-columns: 2fr 1.2fr 1fr 1fr 1fr auto;
  gap: var(--space-3);
  align-items: end;
}

.numeric {
  text-align: right;
}

.clickable {
  cursor: pointer;
}

.link-button {
  border: none;
  background: none;
  padding: 0;
  color: var(--color-link);
  font-weight: 600;
  cursor: pointer;
}

@media (max-width: 1100px) {
  .filters {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}
</style>
