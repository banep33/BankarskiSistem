<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import AppCard from '@/components/ui/AppCard.vue'
import AppBadge from '@/components/ui/AppBadge.vue'
import AppIcon from '@/components/ui/AppIcon.vue'
import MoneyAmount from '@/components/ui/MoneyAmount.vue'
import StatusViews from '@/components/ui/StatusViews.vue'
import { adminApi } from '@/api/bank'
import { errorMessage } from '@/composables/useAsync'
import { formatAccountNumber, formatNumber } from '@/utils/format'
import type { LedgerAccountBalance, ReconciliationLine, TrialBalanceLine } from '@/types/api'

const trialBalance = ref<TrialBalanceLine[]>([])
const ledgerAccounts = ref<LedgerAccountBalance[]>([])
const differences = ref<ReconciliationLine[]>([])
const showCustomer = ref(false)
const isLoading = ref(true)
const error = ref<string | null>(null)

const visibleAccounts = computed(() => (showCustomer.value ? ledgerAccounts.value : ledgerAccounts.value.filter((account) => account.type !== 'Klijent')))
const typeLabels: Record<string, string> = { Klijent: 'Klijentsko', Blagajna: 'Blagajna', Uskladjivanje: 'Usklađivanje', Menjacnica: 'Menjačnica', Provizije: 'Provizije', Kliring: 'Kliring' }

async function load(): Promise<void> {
  isLoading.value = true
  error.value = null
  try {
    ;[trialBalance.value, ledgerAccounts.value, differences.value] = await Promise.all([adminApi.trialBalance(), adminApi.ledgerAccounts(), adminApi.reconciliation(true)])
  } catch (caught) {
    error.value = errorMessage(caught)
  } finally {
    isLoading.value = false
  }
}

onMounted(load)
</script>

<template>
  <div class="page">
    <header class="page-header">
      <div>
        <h1 class="page-title">Glavna knjiga</h1>
        <p class="page-subtitle">Dvojno knjigovodstvo: svaki nalog za knjiženje ima jednak zbir duguje i potražuje, a knjiženja se ne mogu menjati.</p>
      </div>
    </header>

    <StatusViews v-if="isLoading" state="loading" :rows="6" />
    <StatusViews v-else-if="error" state="error" :message="error" @retry="load" />

    <template v-else>
      <div class="grid-3">
        <AppCard v-for="line in trialBalance" :key="line.currency" :tone="line.total === 0 ? 'dark' : 'cream'">
          <p class="label">Bruto bilans {{ line.currency }}</p>
          <p class="value">
            <AppIcon :name="line.total === 0 ? 'check' : 'alert'" />
            {{ line.total === 0 ? 'Uravnotežen' : 'Neuravnotežen' }}
          </p>
          <p class="sub">{{ formatNumber(line.postingCount) }} stavki knjiženja, zbir <MoneyAmount :amount="line.total" :currency="line.currency" :sensitive="false" /></p>
        </AppCard>
      </div>

      <AppCard title="Usklađenost stanja računa sa knjigom" :subtitle="differences.length ? 'Pronađene su razlike koje treba istražiti.' : 'Stanje svakog računa jednako je zbiru njegovih knjiženja.'">
        <StatusViews v-if="!differences.length" state="empty" icon="check" title="Nema razlika" message="Svi klijentski računi su usklađeni sa glavnom knjigom." />
        <div v-else class="table-scroll">
          <table class="data-table">
            <thead>
              <tr>
                <th scope="col">Račun</th>
                <th scope="col" class="numeric">Stanje računa</th>
                <th scope="col" class="numeric">Stanje u knjizi</th>
                <th scope="col" class="numeric">Razlika</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="line in differences" :key="line.accountId">
                <td class="mono">{{ formatAccountNumber(line.accountNumber) }}</td>
                <td class="numeric"><MoneyAmount :amount="line.storedBalance" :currency="line.currency" :sensitive="false" /></td>
                <td class="numeric"><MoneyAmount :amount="line.ledgerBalance" :currency="line.currency" :sensitive="false" /></td>
                <td class="numeric danger"><MoneyAmount :amount="line.difference" :currency="line.currency" :sensitive="false" /></td>
              </tr>
            </tbody>
          </table>
        </div>
      </AppCard>

      <AppCard title="Konta">
        <template #actions>
          <label class="check"><input v-model="showCustomer" type="checkbox" /> Prikaži i klijentska konta</label>
        </template>
        <div class="table-scroll">
          <table class="data-table">
            <thead>
              <tr>
                <th scope="col">Šifra</th>
                <th scope="col">Naziv</th>
                <th scope="col">Vrsta</th>
                <th scope="col" class="numeric">Saldo</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="account in visibleAccounts" :key="account.ledgerAccountId">
                <td class="mono">{{ account.code }}</td>
                <td>{{ account.name }}</td>
                <td><AppBadge :tone="account.type === 'Klijent' ? 'neutral' : 'info'">{{ typeLabels[account.type] ?? account.type }}</AppBadge></td>
                <td class="numeric"><MoneyAmount :amount="account.balance" :currency="account.currency" :sensitive="false" /></td>
              </tr>
            </tbody>
          </table>
        </div>
      </AppCard>
    </template>
  </div>
</template>

<style scoped>
.label {
  font-size: var(--font-size-sm);
  opacity: 0.75;
}

.value {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  margin: var(--space-2) 0;
  font-size: var(--font-size-2xl);
  font-weight: 600;
}

.sub {
  font-size: var(--font-size-sm);
  opacity: 0.8;
}

.numeric {
  text-align: right;
}

.danger {
  color: var(--color-danger);
  font-weight: 600;
}

.check {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  font-size: var(--font-size-sm);
  cursor: pointer;
}
</style>
