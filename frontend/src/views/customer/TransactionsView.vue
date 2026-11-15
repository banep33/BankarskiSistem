<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import AppCard from '@/components/ui/AppCard.vue'
import AppField from '@/components/ui/AppField.vue'
import AppButton from '@/components/ui/AppButton.vue'
import StatusViews from '@/components/ui/StatusViews.vue'
import TransactionList from '@/components/banking/TransactionList.vue'
import TransactionDetailModal from '@/components/banking/TransactionDetailModal.vue'
import { meApi, type TransactionFilters } from '@/api/bank'
import { useUiStore } from '@/stores/ui'
import { errorMessage } from '@/composables/useAsync'
import { formatAccountNumber } from '@/utils/format'
import type { Account, Transaction, TransactionType } from '@/types/api'

const ui = useUiStore()
const accounts = ref<Account[]>([])
const transactions = ref<Transaction[]>([])
const cursor = ref<string | null>(null)
const hasMore = ref(false)
const isLoading = ref(true)
const isLoadingMore = ref(false)
const error = ref<string | null>(null)
const selected = ref<Transaction | null>(null)

const filters = reactive({
  accountId: '' as number | '',
  type: '' as TransactionType | '',
  from: '',
  to: '',
  search: '',
})

const ownIds = computed(() => accounts.value.map((account) => account.id))
const accountOptions = computed(() => [
  { value: '', label: 'Svi računi' },
  ...accounts.value.map((account) => ({ value: account.id, label: `${formatAccountNumber(account.accountNumber)} (${account.currency})` })),
])
const typeOptions = [
  { value: '', label: 'Sve vrste' },
  { value: 'Uplata', label: 'Uplate' },
  { value: 'Isplata', label: 'Isplate' },
  { value: 'Transfer', label: 'Plaćanja u banci' },
  { value: 'EksterniTransfer', label: 'Plaćanja drugoj banci' },
]

function activeFilters(): TransactionFilters {
  return {
    accountId: filters.accountId || undefined,
    type: filters.type || undefined,
    from: filters.from || undefined,
    to: filters.to || undefined,
    search: filters.search.trim() || undefined,
  }
}

async function load(): Promise<void> {
  isLoading.value = true
  error.value = null
  try {
    if (!accounts.value.length) accounts.value = await meApi.accounts()
    const page = await meApi.transactions(activeFilters())
    transactions.value = page.items
    cursor.value = page.nextCursor
    hasMore.value = page.hasMore
  } catch (caught) {
    error.value = errorMessage(caught)
  } finally {
    isLoading.value = false
  }
}

async function loadMore(): Promise<void> {
  isLoadingMore.value = true
  try {
    const page = await meApi.transactions(activeFilters(), cursor.value)
    transactions.value.push(...page.items)
    cursor.value = page.nextCursor
    hasMore.value = page.hasMore
  } catch (caught) {
    ui.notify(errorMessage(caught), 'danger')
  } finally {
    isLoadingMore.value = false
  }
}

function reset(): void {
  Object.assign(filters, { accountId: '', type: '', from: '', to: '', search: '' })
  load()
}

onMounted(load)
</script>

<template>
  <div class="page">
    <header class="page-header">
      <div>
        <h1 class="page-title">Transakcije</h1>
        <p class="page-subtitle">Kompletna istorija prometa po svim vašim računima.</p>
      </div>
    </header>

    <AppCard>
      <form class="filters" @submit.prevent="load">
        <AppField v-model="filters.search" label="Pretraga" type="search" placeholder="Opis, primalac ili referenca" />
        <AppField v-model="filters.accountId" label="Račun" :options="accountOptions" />
        <AppField v-model="filters.type" label="Vrsta" :options="typeOptions" />
        <AppField v-model="filters.from" label="Od" type="date" />
        <AppField v-model="filters.to" label="Do" type="date" />
        <div class="filter-actions">
          <AppButton type="submit" icon="search">Primeni</AppButton>
          <AppButton variant="ghost" @click="reset">Poništi</AppButton>
        </div>
      </form>
    </AppCard>

    <AppCard>
      <StatusViews v-if="isLoading" state="loading" :rows="6" />
      <StatusViews v-else-if="error" state="error" :message="error" @retry="load" />
      <StatusViews v-else-if="!transactions.length" state="empty" icon="list" title="Nema transakcija" message="Za izabrane filtere nema rezultata." />
      <template v-else>
        <TransactionList :transactions="transactions" :own-account-ids="ownIds" @select="selected = $event" />
        <div v-if="hasMore" class="more">
          <AppButton variant="secondary" :loading="isLoadingMore" @click="loadMore">Učitaj još</AppButton>
        </div>
      </template>
    </AppCard>

    <TransactionDetailModal :transaction="selected" allow-complaint @close="selected = null" />
  </div>
</template>

<style scoped>
.filters {
  display: grid;
  grid-template-columns: 2fr 1.6fr 1.2fr 1fr 1fr auto;
  gap: var(--space-3);
  align-items: end;
}

.filter-actions {
  display: flex;
  gap: var(--space-2);
}

.more {
  display: flex;
  justify-content: center;
  padding-top: var(--space-4);
}

@media (max-width: 1200px) {
  .filters {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}

@media (max-width: 600px) {
  .filters {
    grid-template-columns: minmax(0, 1fr);
  }
}
</style>
