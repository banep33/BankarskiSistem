<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useRoute } from 'vue-router'
import AppCard from '@/components/ui/AppCard.vue'
import AppButton from '@/components/ui/AppButton.vue'
import AppField from '@/components/ui/AppField.vue'
import AppBadge from '@/components/ui/AppBadge.vue'
import AppModal from '@/components/ui/AppModal.vue'
import MoneyAmount from '@/components/ui/MoneyAmount.vue'
import StatusViews from '@/components/ui/StatusViews.vue'
import TransactionList from '@/components/banking/TransactionList.vue'
import TransactionDetailModal from '@/components/banking/TransactionDetailModal.vue'
import { accountsApi, helpApi, usersApi } from '@/api/bank'
import { newIdempotencyKey } from '@/api/http'
import { useAuthStore } from '@/stores/auth'
import { useUiStore } from '@/stores/ui'
import { errorMessage } from '@/composables/useAsync'
import { accountStatusLabels, kycLabels, riskLabels } from '@/utils/labels'
import { formatAccountNumber, formatDate, formatDateTime, initials } from '@/utils/format'
import type { Account, AccountStatus, KycStatus, RiskLevel, Transaction, User } from '@/types/api'

const route = useRoute()
const auth = useAuthStore()
const ui = useUiStore()
const userId = computed(() => Number(route.params.id))

const user = ref<User | null>(null)
const accounts = ref<Account[]>([])
const currencies = ref<string[]>(['RSD', 'EUR', 'USD'])
const isLoading = ref(true)
const error = ref<string | null>(null)
const kyc = reactive({ status: 'NijeVerifikovan' as KycStatus, risk: 'Nizak' as RiskLevel })
const isSavingKyc = ref(false)
const newCurrency = ref('RSD')
const isOpening = ref(false)
const selectedAccount = ref<Account | null>(null)
const accountTransactions = ref<Transaction[]>([])
const selectedTransaction = ref<Transaction | null>(null)
const deposit = reactive({ open: false, amount: '' as string | number, description: '', key: '', error: null as string | null, saving: false })

const kycOptions = (['NijeVerifikovan', 'Verifikovan', 'Odbijen'] as KycStatus[]).map((value) => ({ value, label: kycLabels[value].label }))
const riskOptions = (['Nizak', 'Srednji', 'Visok'] as RiskLevel[]).map((value) => ({ value, label: riskLabels[value].label }))
const currencyOptions = computed(() => currencies.value.map((value) => ({ value, label: value })))
const canOpenAccount = computed(() => user.value?.kycStatus === 'Verifikovan' && user.value.isActive)

async function load(): Promise<void> {
  isLoading.value = true
  error.value = null
  try {
    const [loadedUser, loadedAccounts, reference] = await Promise.all([usersApi.get(userId.value), usersApi.accounts(userId.value), helpApi.referenceData()])
    user.value = loadedUser
    accounts.value = loadedAccounts
    currencies.value = reference.currencies
    kyc.status = loadedUser.kycStatus === 'NijePrimenljivo' ? 'NijeVerifikovan' : loadedUser.kycStatus
    kyc.risk = loadedUser.riskRating
  } catch (caught) {
    error.value = errorMessage(caught)
  } finally {
    isLoading.value = false
  }
}

async function saveKyc(): Promise<void> {
  isSavingKyc.value = true
  try {
    user.value = await usersApi.updateKyc(userId.value, kyc.status, kyc.risk)
    ui.notify('KYC status je sačuvan.')
  } catch (caught) {
    ui.notify(errorMessage(caught), 'danger')
  } finally {
    isSavingKyc.value = false
  }
}

async function lifecycle(action: 'activate' | 'deactivate' | 'unlock'): Promise<void> {
  try {
    user.value = await usersApi[action](userId.value)
    ui.notify(action === 'unlock' ? 'Nalog je otključan.' : action === 'activate' ? 'Nalog je aktiviran.' : 'Nalog je deaktiviran i sve sesije su prekinute.')
  } catch (caught) {
    ui.notify(errorMessage(caught), 'danger')
  }
}

async function openAccount(): Promise<void> {
  isOpening.value = true
  try {
    const account = await accountsApi.open(userId.value, newCurrency.value)
    accounts.value.push(account)
    ui.notify(`Otvoren račun ${formatAccountNumber(account.accountNumber)}.`)
  } catch (caught) {
    ui.notify(errorMessage(caught), 'danger')
  } finally {
    isOpening.value = false
  }
}

async function showAccount(account: Account): Promise<void> {
  selectedAccount.value = account
  try {
    accountTransactions.value = (await accountsApi.transactions(account.id, {}, null, 15)).items
  } catch (caught) {
    ui.notify(errorMessage(caught), 'danger')
  }
}

async function changeStatus(account: Account, status: AccountStatus): Promise<void> {
  try {
    const updated = await accountsApi.changeStatus(account.id, status)
    accounts.value = accounts.value.map((item) => (item.id === updated.id ? updated : item))
    if (selectedAccount.value?.id === updated.id) selectedAccount.value = updated
    ui.notify(`Status računa je promenjen u: ${accountStatusLabels[status].label}.`)
  } catch (caught) {
    ui.notify(errorMessage(caught), 'danger')
  }
}

function openDeposit(account: Account): void {
  selectedAccount.value = account
  Object.assign(deposit, { open: true, amount: '', description: '', key: newIdempotencyKey(), error: null })
}

async function submitDeposit(): Promise<void> {
  if (!selectedAccount.value) return
  const amount = Number(String(deposit.amount).replace(',', '.'))
  if (!Number.isFinite(amount) || amount <= 0) {
    deposit.error = 'Unesite iznos veći od nule.'
    return
  }
  deposit.saving = true
  deposit.error = null
  try {
    const result = await accountsApi.deposit(selectedAccount.value.id, amount, deposit.description.trim() || null, deposit.key)
    deposit.open = false
    ui.notify(`${result.message} Referenca ${result.transactionReference}.`)
    accounts.value = await usersApi.accounts(userId.value)
    const refreshed = accounts.value.find((item) => item.id === selectedAccount.value?.id)
    if (refreshed) await showAccount(refreshed)
  } catch (caught) {
    deposit.error = errorMessage(caught)
  } finally {
    deposit.saving = false
  }
}

onMounted(load)
</script>

<template>
  <div class="page">
    <StatusViews v-if="isLoading" state="loading" :rows="5" />
    <StatusViews v-else-if="error" state="error" :message="error" @retry="load" />

    <template v-else-if="user">
      <RouterLink :to="auth.isAdministrator ? '/admin/korisnici' : '/bankar/klijenti'" class="back">← Nazad na listu</RouterLink>
      <AppCard tone="dark">
        <div class="identity">
          <span class="avatar">{{ initials(user.firstName, user.lastName) }}</span>
          <div>
            <h1 class="name">{{ user.firstName }} {{ user.lastName }}</h1>
            <p class="email">{{ user.email }} · klijent od {{ formatDate(user.createdAt) }}</p>
            <div class="row badges">
              <AppBadge :tone="kycLabels[user.kycStatus].tone">KYC: {{ kycLabels[user.kycStatus].label }}</AppBadge>
              <AppBadge :tone="riskLabels[user.riskRating].tone">{{ riskLabels[user.riskRating].label }}</AppBadge>
              <AppBadge v-if="user.isLocked" tone="danger">Zaključan do {{ formatDateTime(user.lockedUntil) }}</AppBadge>
              <AppBadge v-else :tone="user.isActive ? 'success' : 'neutral'">{{ user.isActive ? 'Aktivan nalog' : 'Neaktivan nalog' }}</AppBadge>
            </div>
          </div>
          <div class="identity-actions">
            <AppButton v-if="user.isLocked" variant="lime" size="sm" @click="lifecycle('unlock')">Otključaj</AppButton>
            <AppButton v-if="user.isActive" variant="secondary" size="sm" @click="lifecycle('deactivate')">Deaktiviraj</AppButton>
            <AppButton v-else variant="lime" size="sm" @click="lifecycle('activate')">Aktiviraj</AppButton>
          </div>
        </div>
      </AppCard>

      <div class="grid-2 align-start">
        <AppCard title="Provera identiteta (KYC)" subtitle="Račun se može otvoriti tek kada je identitet verifikovan.">
          <form class="stack" @submit.prevent="saveKyc">
            <div class="grid-2">
              <AppField v-model="kyc.status" label="Status" :options="kycOptions" />
              <AppField v-model="kyc.risk" label="Procena rizika" :options="riskOptions" />
            </div>
            <p v-if="user.kycVerifiedAt" class="subtle">Verifikovan {{ formatDateTime(user.kycVerifiedAt) }}</p>
            <AppButton type="submit" :loading="isSavingKyc">Sačuvaj KYC</AppButton>
          </form>
        </AppCard>

        <AppCard title="Otvaranje računa">
          <div v-if="canOpenAccount" class="open-account">
            <AppField v-model="newCurrency" label="Valuta" :options="currencyOptions" />
            <AppButton icon="plus" :loading="isOpening" @click="openAccount">Otvori račun</AppButton>
          </div>
          <p v-else class="muted">
            {{ user.isActive ? 'Pre otvaranja računa potrebno je verifikovati identitet klijenta.' : 'Nalog klijenta nije aktivan.' }}
          </p>
        </AppCard>
      </div>

      <AppCard title="Računi klijenta" :padded="false">
        <div class="card-body">
          <StatusViews v-if="!accounts.length" state="empty" icon="wallet" title="Klijent nema račune" />
          <div v-else class="table-scroll">
            <table class="data-table">
              <thead>
                <tr>
                  <th scope="col">Broj računa</th>
                  <th scope="col">Valuta</th>
                  <th scope="col">Status</th>
                  <th scope="col" class="numeric">Stanje</th>
                  <th scope="col"><span class="visually-hidden">Radnje</span></th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="account in accounts" :key="account.id" :class="{ selected: selectedAccount?.id === account.id }">
                  <td><button type="button" class="link-button mono" @click="showAccount(account)">{{ formatAccountNumber(account.accountNumber) }}</button></td>
                  <td>{{ account.currency }}</td>
                  <td><AppBadge :tone="accountStatusLabels[account.status].tone">{{ accountStatusLabels[account.status].label }}</AppBadge></td>
                  <td class="numeric"><MoneyAmount :amount="account.balance" :currency="account.currency" :sensitive="false" /></td>
                  <td class="actions">
                    <AppButton v-if="account.status === 'Aktivan'" size="sm" variant="secondary" icon="arrow-down" @click="openDeposit(account)">Uplata</AppButton>
                    <AppButton v-if="account.status === 'Aktivan'" size="sm" variant="ghost" @click="changeStatus(account, 'Blokiran')">Blokiraj</AppButton>
                    <AppButton v-else size="sm" variant="ghost" @click="changeStatus(account, 'Aktivan')">Aktiviraj</AppButton>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </AppCard>

      <AppCard v-if="selectedAccount" :title="`Poslednje transakcije, ${formatAccountNumber(selectedAccount.accountNumber)}`">
        <TransactionList v-if="accountTransactions.length" :transactions="accountTransactions" :own-account-ids="[selectedAccount.id]" @select="selectedTransaction = $event" />
        <StatusViews v-else state="empty" icon="list" title="Nema transakcija" />
      </AppCard>
    </template>

    <AppModal :open="deposit.open" title="Uplata na račun" width="460px" @close="deposit.open = false">
      <form id="deposit-form" class="stack" @submit.prevent="submitDeposit">
        <p v-if="selectedAccount" class="muted">
          Račun <span class="mono">{{ formatAccountNumber(selectedAccount.accountNumber) }}</span> ({{ selectedAccount.currency }})
        </p>
        <AppField v-model="deposit.amount" label="Iznos" inputmode="decimal" required />
        <AppField v-model="deposit.description" label="Opis (nije obavezno)" :maxlength="200" placeholder="npr. Uplata gotovine na šalteru" />
        <p class="subtle">Gotovinske uplate iznad zakonskog praga automatski se prijavljuju AML proveri.</p>
        <p v-if="deposit.error" class="form-error" role="alert">{{ deposit.error }}</p>
      </form>
      <template #footer>
        <AppButton variant="ghost" @click="deposit.open = false">Odustani</AppButton>
        <AppButton type="submit" form="deposit-form" :loading="deposit.saving">Proknjiži uplatu</AppButton>
      </template>
    </AppModal>

    <TransactionDetailModal :transaction="selectedTransaction" @close="selectedTransaction = null" />
  </div>
</template>

<style scoped>
.back {
  font-size: var(--font-size-sm);
  color: var(--color-text-muted);
  text-decoration: none;
}

.identity {
  display: flex;
  align-items: center;
  gap: var(--space-5);
  flex-wrap: wrap;
}

.avatar {
  display: grid;
  place-items: center;
  width: 64px;
  height: 64px;
  border-radius: 50%;
  background: var(--color-lime-400);
  color: var(--color-forest-900);
  font-size: var(--font-size-xl);
  font-weight: 700;
}

.name {
  font-size: var(--font-size-2xl);
}

.email {
  color: rgb(255 255 255 / 0.7);
  font-size: var(--font-size-sm);
}

.badges {
  margin-top: var(--space-2);
  flex-wrap: wrap;
}

.identity-actions {
  margin-left: auto;
  display: flex;
  gap: var(--space-2);
}

.align-start {
  align-items: start;
}

.open-account {
  display: flex;
  align-items: flex-end;
  gap: var(--space-3);
}

.open-account > :first-child {
  flex: 1;
}

.card-body {
  padding: var(--space-5) var(--space-6);
}

.numeric {
  text-align: right;
}

.actions {
  display: flex;
  gap: var(--space-1);
  justify-content: flex-end;
}

.selected {
  background: var(--color-primary-soft);
}

.link-button {
  border: none;
  background: none;
  padding: 0;
  color: var(--color-primary-hover);
  font-weight: 600;
  cursor: pointer;
  text-decoration: underline;
}

.form-error {
  color: var(--color-danger);
  font-size: var(--font-size-sm);
}
</style>
