<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { RouterLink } from 'vue-router'
import AppCard from '@/components/ui/AppCard.vue'
import AppBadge from '@/components/ui/AppBadge.vue'
import AppButton from '@/components/ui/AppButton.vue'
import AppField from '@/components/ui/AppField.vue'
import AppIcon from '@/components/ui/AppIcon.vue'
import AppModal from '@/components/ui/AppModal.vue'
import MoneyAmount from '@/components/ui/MoneyAmount.vue'
import StatusViews from '@/components/ui/StatusViews.vue'
import { meApi, savingsApi } from '@/api/bank'
import { newIdempotencyKey } from '@/api/http'
import { useUiStore } from '@/stores/ui'
import { errorMessage } from '@/composables/useAsync'
import { formatAccountNumber, formatDate, gridColumnsClass, toIsoDate } from '@/utils/format'
import type { Account, SavingsGoal } from '@/types/api'

type Mode = 'create' | 'edit' | 'contribute' | 'withdraw' | 'close'

const ui = useUiStore()
const goals = ref<SavingsGoal[]>([])
const accounts = ref<Account[]>([])
const isLoading = ref(true)
const error = ref<string | null>(null)
const tomorrow = toIsoDate(new Date(Date.now() + 86_400_000))
const dialog = reactive({
  mode: null as Mode | null,
  goal: null as SavingsGoal | null,
  name: '',
  target: '' as string | number,
  date: '',
  currency: 'RSD',
  accountId: null as number | null,
  amount: '' as string | number,
  key: '',
  error: null as string | null,
  saving: false,
})

const open = computed(() => goals.value.filter((goal) => goal.status !== 'Zatvoren'))
const closed = computed(() => goals.value.filter((goal) => goal.status === 'Zatvoren'))
const totalSaved = computed(() => open.value.filter((goal) => goal.currency === 'RSD').reduce((sum, goal) => sum + goal.savedAmount, 0))
const currentAccounts = computed(() => accounts.value.filter((account) => account.kind === 'Tekuci' && account.status === 'Aktivan'))
const currencyOptions = computed(() => [...new Set(currentAccounts.value.map((account) => account.currency))].map((currency) => ({ value: currency, label: currency })))
const accountOptions = computed(() =>
  currentAccounts.value
    .filter((account) => !dialog.goal || account.currency === dialog.goal.currency)
    .map((account) => ({ value: account.id, label: `${formatAccountNumber(account.accountNumber)} (${account.currency})` })),
)
const dialogTitle = computed(() => ({ create: 'Novi cilj štednje', edit: 'Izmena cilja', contribute: 'Uplata na cilj', withdraw: 'Povlačenje sa cilja', close: 'Zatvaranje cilja' })[dialog.mode ?? 'create'])

async function load(): Promise<void> {
  isLoading.value = true
  error.value = null
  try {
    ;[goals.value, accounts.value] = await Promise.all([savingsApi.list(), meApi.accounts()])
  } catch (caught) {
    error.value = errorMessage(caught)
  } finally {
    isLoading.value = false
  }
}

function openDialog(mode: Mode, goal: SavingsGoal | null = null): void {
  Object.assign(dialog, {
    mode,
    goal,
    name: goal?.name ?? '',
    target: goal?.targetAmount ?? '',
    date: goal?.targetDate ?? '',
    currency: currencyOptions.value[0]?.value ?? 'RSD',
    amount: mode === 'withdraw' || mode === 'contribute' ? '' : goal?.savedAmount ?? '',
    key: newIdempotencyKey(),
    error: null,
  })
  dialog.accountId = (accountOptions.value[0]?.value as number | undefined) ?? null
}

function toNumber(value: string | number): number {
  return Number(String(value).replace(',', '.'))
}

async function submit(): Promise<void> {
  dialog.error = null
  dialog.saving = true
  try {
    if (dialog.mode === 'create' || dialog.mode === 'edit') {
      const target = toNumber(dialog.target)
      if (!dialog.name.trim() || !(target > 0)) throw new Error('Unesite naziv i ciljani iznos veći od nule.')
      if (dialog.mode === 'create') await savingsApi.create(dialog.name.trim(), target, dialog.date || null, dialog.currency)
      else await savingsApi.update(dialog.goal!.id, dialog.name.trim(), target, dialog.date || null)
      ui.notify(dialog.mode === 'create' ? 'Cilj je napravljen i otvoren je štedni račun.' : 'Cilj je izmenjen.')
    } else if (dialog.mode === 'contribute' || dialog.mode === 'withdraw') {
      const amount = toNumber(dialog.amount)
      if (!(amount > 0) || !dialog.accountId) throw new Error('Izaberite račun i unesite iznos veći od nule.')
      const result =
        dialog.mode === 'contribute'
          ? await savingsApi.contribute(dialog.goal!.id, dialog.accountId, amount, dialog.key)
          : await savingsApi.withdraw(dialog.goal!.id, dialog.accountId, amount, dialog.key)
      ui.notify(result.status === 'Ostvaren' && dialog.mode === 'contribute' ? `Čestitamo, cilj „${result.name}“ je ostvaren!` : 'Prenos je izvršen.')
    } else if (dialog.mode === 'close') {
      if (!dialog.accountId) throw new Error('Izaberite račun na koji se vraća ušteđeni novac.')
      await savingsApi.close(dialog.goal!.id, dialog.accountId)
      ui.notify('Cilj je zatvoren, a ušteđeni novac je vraćen na tekući račun.')
    }
    dialog.mode = null
    await load()
  } catch (caught) {
    dialog.error = caught instanceof Error && !('status' in caught) ? caught.message : errorMessage(caught)
  } finally {
    dialog.saving = false
  }
}

onMounted(load)
</script>

<template>
  <div class="page">
    <header class="page-header">
      <div>
        <h1 class="page-title">Štednja</h1>
        <p class="page-subtitle">Ciljevi štednje sa posebnim štednim računom za svaki cilj. Novac je odvojen od tekućeg računa i ne može se slučajno potrošiti.</p>
      </div>
      <AppButton icon="plus" :disabled="!currencyOptions.length" @click="openDialog('create')">Novi cilj</AppButton>
    </header>

    <StatusViews v-if="isLoading" state="loading" :rows="4" />
    <StatusViews v-else-if="error" state="error" :message="error" @retry="load" />
    <StatusViews v-else-if="!goals.length" state="empty" icon="star" title="Još nemate ciljeve štednje" message="Napravite cilj, na primer letovanje ili novi telefon, i pratite koliko vam je ostalo." />

    <template v-else>
      <AppCard tone="dark">
        <div class="summary">
          <div>
            <p class="label">Ukupno ušteđeno (RSD ciljevi)</p>
            <p class="big amount-fit"><MoneyAmount :amount="totalSaved" currency="RSD" /></p>
          </div>
          <p class="tip"><AppIcon name="repeat" :size="16" /> Za automatsku štednju napravite trajni nalog ka štednom računu cilja, dugmetom „Automatska štednja“.</p>
        </div>
      </AppCard>

      <div v-if="open.length" :class="gridColumnsClass(open.length, 2)">
        <AppCard v-for="goal in open" :key="goal.id">
          <div class="row-between">
            <h2 class="goal-name">{{ goal.name }}</h2>
            <AppBadge :tone="goal.status === 'Ostvaren' ? 'success' : 'info'">{{ goal.status === 'Ostvaren' ? 'Ostvaren' : 'U toku' }}</AppBadge>
          </div>
          <p class="amounts">
            <MoneyAmount :amount="goal.savedAmount" :currency="goal.currency" />
            <span class="subtle"> od <MoneyAmount :amount="goal.targetAmount" :currency="goal.currency" :sensitive="false" /></span>
          </p>
          <div class="progress" role="progressbar" :aria-valuenow="goal.progress" aria-valuemin="0" aria-valuemax="100" :aria-label="`${goal.name}: ${goal.progress}%`">
            <span :style="{ width: `${goal.progress}%` }" :class="{ done: goal.status === 'Ostvaren' }" />
          </div>
          <div class="row-between meta">
            <span>{{ goal.progress }}%</span>
            <span v-if="goal.targetDate">do {{ formatDate(goal.targetDate) }}</span>
          </div>
          <p v-if="goal.monthlyNeeded" class="needed">Da biste stigli do cilja, mesečno je potrebno <MoneyAmount :amount="goal.monthlyNeeded" :currency="goal.currency" :sensitive="false" />.</p>
          <p class="subtle mono small">Štedni račun {{ goal.accountNumber ? formatAccountNumber(goal.accountNumber) : '' }}</p>
          <div class="actions">
            <AppButton size="sm" icon="arrow-down" @click="openDialog('contribute', goal)">Uplati</AppButton>
            <AppButton size="sm" variant="secondary" icon="arrow-up" :disabled="goal.savedAmount <= 0" @click="openDialog('withdraw', goal)">Povuci</AppButton>
            <RouterLink v-if="goal.accountNumber" :to="{ path: '/trajni-nalozi/novi', query: { naRacun: goal.accountNumber, naziv: goal.name } }" class="auto-link">
              Automatska štednja
            </RouterLink>
            <AppButton size="sm" variant="ghost" icon="edit" @click="openDialog('edit', goal)">Izmeni</AppButton>
            <AppButton size="sm" variant="ghost" @click="openDialog('close', goal)">Zatvori</AppButton>
          </div>
        </AppCard>
      </div>

      <AppCard v-if="closed.length" title="Zatvoreni ciljevi">
        <ul class="closed">
          <li v-for="goal in closed" :key="goal.id" class="row-between">
            <span>{{ goal.name }}</span>
            <span class="subtle">zatvoren {{ formatDate(goal.closedAt) }}</span>
          </li>
        </ul>
      </AppCard>
    </template>

    <AppModal :open="dialog.mode !== null" :title="dialogTitle" width="480px" @close="dialog.mode = null">
      <form id="savings-form" class="stack" @submit.prevent="submit">
        <template v-if="dialog.mode === 'create' || dialog.mode === 'edit'">
          <AppField v-model="dialog.name" label="Naziv cilja" :maxlength="60" placeholder="npr. Letovanje" required />
          <AppField v-model="dialog.target" label="Ciljani iznos" inputmode="decimal" required />
          <AppField v-model="dialog.date" label="Do kada (nije obavezno)" type="date" :min="tomorrow" />
          <AppField v-if="dialog.mode === 'create'" v-model="dialog.currency" label="Valuta" :options="currencyOptions" />
          <p v-if="dialog.mode === 'create'" class="subtle">Za cilj se automatski otvara štedni račun. Sa njega se novac može prebaciti samo na sopstveni tekući račun.</p>
        </template>
        <template v-else-if="dialog.mode === 'contribute' || dialog.mode === 'withdraw'">
          <AppField v-model="dialog.accountId" :label="dialog.mode === 'contribute' ? 'Sa tekućeg računa' : 'Na tekući račun'" :options="accountOptions" />
          <AppField v-model="dialog.amount" :label="`Iznos (${dialog.goal?.currency})`" inputmode="decimal" required />
          <p class="subtle">Prenos između sopstvenih računa je besplatan i ne troši dnevni limit.</p>
        </template>
        <template v-else-if="dialog.mode === 'close'">
          <p class="muted">Ušteđenih <MoneyAmount :amount="dialog.goal?.savedAmount ?? 0" :currency="dialog.goal?.currency ?? 'RSD'" :sensitive="false" /> vraća se na izabrani tekući račun, a štedni račun se zatvara.</p>
          <AppField v-model="dialog.accountId" label="Vrati na račun" :options="accountOptions" />
        </template>
        <p v-if="dialog.error" class="form-error" role="alert">{{ dialog.error }}</p>
      </form>
      <template #footer>
        <AppButton variant="ghost" @click="dialog.mode = null">Odustani</AppButton>
        <AppButton type="submit" form="savings-form" :variant="dialog.mode === 'close' ? 'danger' : 'primary'" :loading="dialog.saving">
          {{ dialog.mode === 'close' ? 'Zatvori cilj' : 'Potvrdi' }}
        </AppButton>
      </template>
    </AppModal>
  </div>
</template>

<style scoped>
.summary {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: var(--space-5);
  flex-wrap: wrap;
}

.label {
  font-size: var(--font-size-sm);
  color: var(--color-emphasis-text-muted);
}

.summary > div {
  flex: 1 1 260px;
  min-width: 0;
}

.big {
  --amount-max: var(--font-size-3xl);
  margin-top: var(--space-2);
  font-weight: 600;
}

.tip {
  display: flex;
  gap: var(--space-2);
  align-items: center;
  max-width: 420px;
  font-size: var(--font-size-sm);
  color: var(--color-emphasis-accent);
}

.goal-name {
  font-size: var(--font-size-lg);
}

.amounts {
  margin: var(--space-3) 0 var(--space-2);
  font-size: var(--font-size-xl);
  font-weight: 600;
}

.progress {
  height: 12px;
  border-radius: var(--radius-pill);
  background: var(--color-surface-muted);
  overflow: hidden;
}

.progress span {
  display: block;
  height: 100%;
  border-radius: var(--radius-pill);
  background: var(--color-primary);
}

.progress span.done {
  background: var(--color-success);
}

.meta {
  margin-top: var(--space-1);
  font-size: var(--font-size-sm);
}

.needed {
  margin-top: var(--space-3);
  font-size: var(--font-size-sm);
}

.small {
  margin-top: var(--space-2);
  font-size: var(--font-size-xs);
}

.actions {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: var(--space-2);
  margin-top: var(--space-4);
  padding-top: var(--space-3);
  border-top: 1px solid var(--color-border);
}

.auto-link {
  font-size: var(--font-size-sm);
  font-weight: 600;
  color: var(--color-link);
}

.closed {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: var(--space-2);
}

.form-error {
  color: var(--color-danger);
  font-size: var(--font-size-sm);
}
</style>
