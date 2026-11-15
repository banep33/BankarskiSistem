<script setup lang="ts">
import { computed, onMounted } from 'vue'
import AppCard from '@/components/ui/AppCard.vue'
import AppBadge from '@/components/ui/AppBadge.vue'
import MoneyAmount from '@/components/ui/MoneyAmount.vue'
import StatusViews from '@/components/ui/StatusViews.vue'
import AccountCard from '@/components/banking/AccountCard.vue'
import { meApi } from '@/api/bank'
import { useAsync } from '@/composables/useAsync'
import { accountStatusLabels } from '@/utils/labels'
import { formatAccountNumber, formatDate, gridColumnsClass } from '@/utils/format'

const { data: accounts, isLoading, error, load } = useAsync(() => meApi.accounts(), [])
const variants = ['forest', 'coral', 'cream'] as const

const totals = computed(() =>
  Object.entries(
    accounts.value.reduce<Record<string, number>>((sum, account) => {
      sum[account.currency] = (sum[account.currency] ?? 0) + account.balance
      return sum
    }, {}),
  ),
)

onMounted(load)
</script>

<template>
  <div class="page">
    <header class="page-header">
      <div>
        <h1 class="page-title">Računi</h1>
        <p class="page-subtitle">Pregled svih vaših tekućih i deviznih računa.</p>
      </div>
      <div class="row">
        <span v-for="[currency, amount] in totals" :key="currency" class="total">
          <span class="subtle">Ukupno {{ currency }}</span>
          <MoneyAmount :amount="amount" :currency="currency" />
        </span>
      </div>
    </header>

    <StatusViews v-if="isLoading" state="loading" :rows="3" />
    <StatusViews v-else-if="error" state="error" :message="error" @retry="load" />
    <StatusViews
      v-else-if="accounts.length === 0"
      state="empty"
      icon="wallet"
      title="Nemate otvoren račun"
      message="Račun otvara bankar nakon provere identiteta (KYC). Kontakt banke je u odeljku Pomoć."
    />

    <template v-else>
      <div :class="gridColumnsClass(accounts.length)">
        <AccountCard v-for="(account, index) in accounts" :key="account.id" :account="account" :variant="variants[index % 3]" />
      </div>

      <AppCard title="Detaljan pregled" :padded="false">
        <div class="table-scroll">
          <table class="data-table">
            <thead>
              <tr>
                <th scope="col">Broj računa</th>
                <th scope="col">Valuta</th>
                <th scope="col">Status</th>
                <th scope="col">Otvoren</th>
                <th scope="col" class="numeric">Stanje</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="account in accounts" :key="account.id">
                <td>
                  <RouterLink :to="`/racuni/${account.id}`" class="mono">{{ formatAccountNumber(account.accountNumber) }}</RouterLink>
                </td>
                <td>{{ account.currency }}</td>
                <td>
                  <AppBadge :tone="accountStatusLabels[account.status].tone">{{ accountStatusLabels[account.status].label }}</AppBadge>
                </td>
                <td class="subtle">{{ formatDate(account.createdAt) }}</td>
                <td class="numeric"><MoneyAmount :amount="account.balance" :currency="account.currency" /></td>
              </tr>
            </tbody>
          </table>
        </div>
      </AppCard>
    </template>
  </div>
</template>

<style scoped>
.total {
  display: flex;
  flex-direction: column;
  padding: var(--space-3) var(--space-4);
  border-radius: var(--radius-md);
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  font-weight: 600;
}

.numeric {
  text-align: right;
}
</style>
