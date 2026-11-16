<script setup lang="ts">
import { onMounted, ref, watch } from 'vue'
import AppCard from '@/components/ui/AppCard.vue'
import AppField from '@/components/ui/AppField.vue'
import AppButton from '@/components/ui/AppButton.vue'
import AppBadge from '@/components/ui/AppBadge.vue'
import AppModal from '@/components/ui/AppModal.vue'
import AppPagination from '@/components/ui/AppPagination.vue'
import MoneyAmount from '@/components/ui/MoneyAmount.vue'
import StatusViews from '@/components/ui/StatusViews.vue'
import { accountsApi } from '@/api/bank'
import { useUiStore } from '@/stores/ui'
import { errorMessage } from '@/composables/useAsync'
import { accountStatusLabels } from '@/utils/labels'
import { formatAccountNumber, formatDate } from '@/utils/format'
import type { Account, AccountStatus, PagedResponse } from '@/types/api'

const ui = useUiStore()
const search = ref('')
const page = ref(1)
const result = ref<PagedResponse<Account> | null>(null)
const isLoading = ref(true)
const error = ref<string | null>(null)
const pending = ref<{ account: Account; status: AccountStatus } | null>(null)
const isChanging = ref(false)

async function load(): Promise<void> {
  isLoading.value = true
  error.value = null
  try {
    result.value = await accountsApi.list(search.value.trim() || undefined, page.value)
  } catch (caught) {
    error.value = errorMessage(caught)
  } finally {
    isLoading.value = false
  }
}

async function confirmStatusChange(): Promise<void> {
  if (!pending.value || isChanging.value) return
  const { account, status } = pending.value
  isChanging.value = true
  try {
    const updated = await accountsApi.changeStatus(account.id, status)
    if (result.value) result.value.items = result.value.items.map((item) => (item.id === updated.id ? updated : item))
    pending.value = null
    ui.notify(`Račun ${formatAccountNumber(updated.accountNumber)}: ${accountStatusLabels[status].label}.`)
  } catch (caught) {
    ui.notify(errorMessage(caught), 'danger')
  } finally {
    isChanging.value = false
  }
}

function closeConfirmation(): void {
  if (!isChanging.value) pending.value = null
}

function submit(): void {
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
        <h1 class="page-title">Svi računi</h1>
        <p class="page-subtitle">Pregled i upravljanje statusom računa svih klijenata.</p>
      </div>
    </header>

    <AppCard>
      <form class="search" @submit.prevent="submit">
        <AppField v-model="search" label="Pretraga" type="search" placeholder="Broj računa, ime ili email vlasnika" />
        <AppButton type="submit" icon="search">Traži</AppButton>
      </form>
    </AppCard>

    <AppCard :padded="false">
      <div class="card-body">
        <StatusViews v-if="isLoading" state="loading" :rows="8" />
        <StatusViews v-else-if="error" state="error" :message="error" @retry="load" />
        <StatusViews v-else-if="!result?.items.length" state="empty" icon="wallet" title="Nema računa" />
        <div v-else class="table-scroll">
          <table class="data-table">
            <thead>
              <tr>
                <th scope="col">Broj računa</th>
                <th scope="col">Vlasnik</th>
                <th scope="col">Status</th>
                <th scope="col">Otvoren</th>
                <th scope="col" class="numeric">Stanje</th>
                <th scope="col"><span class="visually-hidden">Radnje</span></th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="account in result.items" :key="account.id">
                <td class="mono">{{ formatAccountNumber(account.accountNumber) }}</td>
                <td>
                  <RouterLink :to="`/bankar/klijenti/${account.userId}`">{{ account.ownerFullName ?? 'Nepoznat' }}</RouterLink>
                  <div class="subtle">{{ account.ownerEmail }}</div>
                </td>
                <td><AppBadge :tone="accountStatusLabels[account.status].tone">{{ accountStatusLabels[account.status].label }}</AppBadge></td>
                <td class="subtle">{{ formatDate(account.createdAt) }}</td>
                <td class="numeric"><MoneyAmount :amount="account.balance" :currency="account.currency" :sensitive="false" /></td>
                <td class="actions">
                  <AppButton v-if="account.status === 'Aktivan'" variant="ghost" size="sm" icon="lock" @click="pending = { account, status: 'Blokiran' }">Blokiraj</AppButton>
                  <AppButton v-else variant="ghost" size="sm" @click="pending = { account, status: 'Aktivan' }">Aktiviraj</AppButton>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
        <AppPagination v-if="result" :page="result.page" :total-pages="result.totalPages" :total-count="result.totalCount" @change="page = $event" />
      </div>
    </AppCard>

    <AppModal :open="pending !== null" :title="pending?.status === 'Blokiran' ? 'Blokiranje računa' : 'Aktiviranje računa'" width="480px" @close="closeConfirmation">
      <div v-if="pending" class="stack">
        <p class="muted">
          {{ pending.status === 'Blokiran'
            ? 'Da li zaista želite da blokirate ovaj račun? Dok je račun blokiran, vlasnik ne može da plaća, podiže gotovinu niti prima nove naloge sa njega.'
            : 'Da li zaista želite da ponovo aktivirate ovaj račun? Vlasnik će odmah moći da ga koristi.' }}
        </p>
        <dl class="confirm-details">
          <div><dt>Račun</dt><dd class="mono">{{ formatAccountNumber(pending.account.accountNumber) }}</dd></div>
          <div><dt>Vlasnik</dt><dd>{{ pending.account.ownerFullName ?? 'Nepoznat' }}</dd></div>
          <div><dt>Stanje</dt><dd><MoneyAmount :amount="pending.account.balance" :currency="pending.account.currency" :sensitive="false" /></dd></div>
          <div><dt>Trenutni status</dt><dd>{{ accountStatusLabels[pending.account.status].label }}</dd></div>
        </dl>
      </div>
      <template #footer>
        <AppButton variant="ghost" :disabled="isChanging" @click="closeConfirmation">Odustani</AppButton>
        <AppButton :variant="pending?.status === 'Blokiran' ? 'danger' : 'primary'" :loading="isChanging" @click="confirmStatusChange">
          {{ pending?.status === 'Blokiran' ? 'Blokiraj račun' : 'Aktiviraj račun' }}
        </AppButton>
      </template>
    </AppModal>
  </div>
</template>

<style scoped>
.search {
  display: flex;
  align-items: flex-end;
  gap: var(--space-3);
}

.search > :first-child {
  flex: 1;
}

.actions {
  white-space: nowrap;
}

.confirm-details {
  display: grid;
  gap: var(--space-2);
  margin: 0;
  padding: var(--space-4);
  border-radius: var(--radius-md);
  background: var(--color-surface-muted);
  font-size: var(--font-size-sm);
}

.confirm-details div {
  display: flex;
  justify-content: space-between;
  gap: var(--space-4);
}

.confirm-details dt {
  color: var(--color-text-muted);
}

.confirm-details dd {
  margin: 0;
  font-weight: 600;
  text-align: right;
  overflow-wrap: anywhere;
}

@media (max-width: 720px) {
  .search {
    flex-wrap: wrap;
  }

  .search > :first-child {
    flex-basis: 100%;
  }
}
</style>
