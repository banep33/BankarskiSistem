<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue'
import AppCard from '@/components/ui/AppCard.vue'
import AppButton from '@/components/ui/AppButton.vue'
import AppField from '@/components/ui/AppField.vue'
import AppIcon from '@/components/ui/AppIcon.vue'
import AppModal from '@/components/ui/AppModal.vue'
import MoneyAmount from '@/components/ui/MoneyAmount.vue'
import StatusViews from '@/components/ui/StatusViews.vue'
import { budgetsApi } from '@/api/bank'
import { useUiStore } from '@/stores/ui'
import { errorMessage } from '@/composables/useAsync'
import { formatDateTime, toIsoDate } from '@/utils/format'
import type { CategorizedTransaction, SpendingCategory, SpendingSummary } from '@/types/api'

const ui = useUiStore()
const currentMonth = toIsoDate(new Date()).slice(0, 7)
const month = ref(currentMonth)
const summary = ref<SpendingSummary | null>(null)
const categories = ref<SpendingCategory[]>([])
const isLoading = ref(true)
const error = ref<string | null>(null)
const budgetForm = reactive({ categoryId: null as number | null, limit: '' as string | number, saving: false })
const editing = reactive<Record<number, string | number>>({})
const pending = reactive({ item: null as CategorizedTransaction | null, categoryId: 0, saving: false })

const maxAmount = computed(() => Math.max(1, ...(summary.value?.categories ?? []).map((c) => Math.max(c.amount, c.monthlyLimit ?? 0))))
const categoryOptions = computed(() => categories.value.map((c) => ({ value: c.id, label: c.name })))
const budgeted = computed(() => (summary.value?.categories ?? []).filter((c) => c.budgetId !== null))
const isCurrentMonth = computed(() => month.value === currentMonth)
const categoryById = computed(() => new Map(categories.value.map((c) => [c.id, c])))

function percent(amount: number, limit: number | null): number {
  return limit ? Math.round((amount / limit) * 100) : 0
}

async function load(): Promise<void> {
  isLoading.value = true
  error.value = null
  try {
    if (!categories.value.length) categories.value = await budgetsApi.categories()
    summary.value = await budgetsApi.spending(month.value)
    for (const item of summary.value.categories) if (item.monthlyLimit !== null) editing[item.categoryId] = item.monthlyLimit
    budgetForm.categoryId ??= categories.value[0]?.id ?? null
  } catch (caught) {
    error.value = errorMessage(caught)
  } finally {
    isLoading.value = false
  }
}

async function saveBudget(categoryId: number, value: string | number): Promise<void> {
  const limit = Number(String(value).replace(',', '.'))
  if (!(limit > 0)) {
    ui.notify('Mesečni iznos mora biti veći od nule.', 'danger')
    return
  }
  budgetForm.saving = true
  try {
    await budgetsApi.set(categoryId, limit)
    ui.notify('Budžet je sačuvan.')
    budgetForm.limit = ''
    await load()
  } catch (caught) {
    ui.notify(errorMessage(caught), 'danger')
  } finally {
    budgetForm.saving = false
  }
}

async function removeBudget(budgetId: number): Promise<void> {
  try {
    await budgetsApi.remove(budgetId)
    ui.notify('Budžet je uklonjen.', 'info')
    await load()
  } catch (caught) {
    ui.notify(errorMessage(caught), 'danger')
  }
}

function changeCategory(item: CategorizedTransaction, categoryId: number): void {
  if (categoryId === item.categoryId) return
  if (item.counterpartyAccountNumber) {
    Object.assign(pending, { item, categoryId })
    return
  }
  assign(item, categoryId, false)
}

async function assign(item: CategorizedTransaction, categoryId: number, remember: boolean): Promise<void> {
  pending.saving = true
  try {
    await budgetsApi.assign(item.transactionId, categoryId, remember)
    ui.notify(remember ? 'Kategorija je promenjena i zapamćena za ovog primaoca.' : 'Kategorija je promenjena.')
    pending.item = null
    await load()
  } catch (caught) {
    ui.notify(errorMessage(caught), 'danger')
  } finally {
    pending.saving = false
  }
}

watch(month, load)
onMounted(load)
</script>

<template>
  <div class="page">
    <header class="page-header">
      <div>
        <h1 class="page-title">Potrošnja i budžeti</h1>
        <p class="page-subtitle">Gde odlazi novac sa vaših dinarskih računa, po kategorijama, i mesečni budžeti sa upozorenjima.</p>
      </div>
      <div class="month">
        <label for="month-picker" class="subtle">Mesec</label>
        <input id="month-picker" v-model="month" type="month" :max="currentMonth" class="month-input" />
      </div>
    </header>

    <StatusViews v-if="isLoading" state="loading" :rows="5" />
    <StatusViews v-else-if="error" state="error" :message="error" @retry="load" />

    <template v-else-if="summary">
      <div class="grid-3">
        <AppCard>
          <p class="label">Prihodi</p>
          <MoneyAmount class="value income" :amount="summary.totalIncome" currency="RSD" />
        </AppCard>
        <AppCard>
          <p class="label">Rashodi</p>
          <MoneyAmount class="value" :amount="summary.totalExpense" currency="RSD" />
        </AppCard>
        <AppCard tone="dark">
          <p class="label">Razlika</p>
          <MoneyAmount class="value" :amount="summary.totalIncome - summary.totalExpense" currency="RSD" />
        </AppCard>
      </div>

      <div class="layout">
        <AppCard title="Potrošnja po kategorijama">
          <StatusViews v-if="!summary.categories.length" state="empty" icon="chart" title="Nema rashoda u ovom mesecu" />
          <ul v-else class="bars">
            <li v-for="item in summary.categories" :key="item.categoryId">
              <div class="row-between">
                <span class="name"><i class="dot" :style="{ background: item.color }" /> {{ item.name }}</span>
                <span>
                  <MoneyAmount :amount="item.amount" currency="RSD" />
                  <span v-if="item.monthlyLimit" class="subtle"> od <MoneyAmount :amount="item.monthlyLimit" currency="RSD" /></span>
                </span>
              </div>
              <div class="track" :aria-label="`${item.name}: ${percent(item.amount, item.monthlyLimit)}% budžeta`">
                <span class="fill" :style="{ width: `${(item.amount / maxAmount) * 100}%`, background: item.color }" />
                <span v-if="item.monthlyLimit" class="limit" :style="{ left: `${(item.monthlyLimit / maxAmount) * 100}%` }" />
              </div>
              <p v-if="item.monthlyLimit" class="budget-state" :class="{ over: item.amount > item.monthlyLimit, near: item.amount <= item.monthlyLimit && percent(item.amount, item.monthlyLimit) >= 80 }">
                {{ percent(item.amount, item.monthlyLimit) }}% budžeta
                <template v-if="item.amount > item.monthlyLimit">, prekoračeno</template>
              </p>
            </li>
          </ul>
        </AppCard>

        <AppCard title="Budžeti" subtitle="Mesečni limit po kategoriji. Upozorenje stiže na 80% i kada se limit pređe.">
          <ul v-if="budgeted.length" class="budgets">
            <li v-for="item in budgeted" :key="item.categoryId">
              <span class="name"><i class="dot" :style="{ background: item.color }" /> {{ item.name }}</span>
              <input v-model="editing[item.categoryId]" class="limit-input" inputmode="decimal" :aria-label="`Mesečni limit za ${item.name}`" />
              <AppButton size="sm" variant="secondary" @click="saveBudget(item.categoryId, editing[item.categoryId] ?? '')">Sačuvaj</AppButton>
              <button type="button" class="remove" :aria-label="`Ukloni budžet ${item.name}`" @click="removeBudget(item.budgetId!)"><AppIcon name="trash" :size="16" /></button>
            </li>
          </ul>
          <p v-else class="muted">Još nemate budžete.</p>
          <form class="new-budget" @submit.prevent="budgetForm.categoryId && saveBudget(budgetForm.categoryId, budgetForm.limit)">
            <AppField v-model="budgetForm.categoryId" label="Kategorija" :options="categoryOptions" />
            <AppField v-model="budgetForm.limit" label="Mesečno (RSD)" inputmode="decimal" />
            <AppButton type="submit" icon="plus" :loading="budgetForm.saving">Dodaj</AppButton>
          </form>
        </AppCard>
      </div>

      <AppCard title="Rashodi u mesecu" :subtitle="isCurrentMonth ? 'Kategoriju možete promeniti; banka predlaže kategoriju po vrsti plaćanja i šifri.' : undefined" :padded="false">
        <div class="card-body">
          <StatusViews v-if="!summary.transactions.length" state="empty" icon="list" title="Nema rashoda" />
          <div v-else class="table-scroll">
            <table class="data-table">
              <thead>
                <tr>
                  <th scope="col">Datum</th>
                  <th scope="col">Primalac i svrha</th>
                  <th scope="col">Kategorija</th>
                  <th scope="col" class="numeric">Iznos</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="item in summary.transactions" :key="item.transactionId">
                  <td class="subtle nowrap">{{ formatDateTime(item.date) }}</td>
                  <td>
                    <strong>{{ item.counterparty }}</strong>
                    <div class="subtle">{{ item.description }}</div>
                  </td>
                  <td>
                    <select class="category-select" :value="item.categoryId" :aria-label="`Kategorija za ${item.counterparty}`" @change="changeCategory(item, Number(($event.target as HTMLSelectElement).value))">
                      <option v-for="category in categories" :key="category.id" :value="category.id">{{ category.name }}</option>
                    </select>
                    <span v-if="!item.isExplicit" class="subtle hint">predlog</span>
                  </td>
                  <td class="numeric"><MoneyAmount :amount="item.amount" currency="RSD" /></td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </AppCard>
    </template>

    <AppModal :open="pending.item !== null" title="Promena kategorije" width="460px" @close="pending.item = null">
      <p v-if="pending.item" class="muted">
        Nova kategorija za plaćanje primaocu <strong>{{ pending.item.counterparty }}</strong> je
        <strong>{{ categoryById.get(pending.categoryId)?.name }}</strong>. Da li da je primenimo i na buduća plaćanja ovom primaocu?
      </p>
      <template #footer>
        <AppButton variant="ghost" :disabled="pending.saving" @click="pending.item && assign(pending.item, pending.categoryId, false)">Samo ovo plaćanje</AppButton>
        <AppButton :loading="pending.saving" @click="pending.item && assign(pending.item, pending.categoryId, true)">Zapamti za primaoca</AppButton>
      </template>
    </AppModal>
  </div>
</template>

<style scoped>
.month {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.month-input {
  padding: 0.55rem 0.8rem;
  border: 1px solid var(--color-border-strong);
  border-radius: var(--radius-md);
  background: var(--color-surface);
}

.label {
  font-size: var(--font-size-sm);
  opacity: 0.8;
}

.value {
  display: block;
  margin-top: var(--space-2);
  font-size: var(--font-size-2xl);
  font-weight: 600;
}

.value.income {
  color: var(--color-success);
}

.layout {
  display: grid;
  grid-template-columns: minmax(0, 1.4fr) minmax(0, 1fr);
  gap: var(--space-5);
  align-items: start;
}

.bars,
.budgets {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
}

.name {
  display: inline-flex;
  align-items: center;
  gap: var(--space-2);
  font-weight: 600;
}

.dot {
  display: inline-block;
  width: 12px;
  height: 12px;
  border-radius: 4px;
}

.track {
  position: relative;
  height: 12px;
  margin-top: var(--space-2);
  border-radius: var(--radius-pill);
  background: var(--color-surface-muted);
}

.fill {
  position: absolute;
  inset: 0 auto 0 0;
  border-radius: var(--radius-pill);
}

.limit {
  position: absolute;
  top: -3px;
  bottom: -3px;
  width: 2px;
  background: var(--color-text);
}

.budget-state {
  margin-top: 4px;
  font-size: var(--font-size-xs);
  color: var(--color-success);
}

.budget-state.near {
  color: var(--color-warning);
}

.budget-state.over {
  color: var(--color-danger);
  font-weight: 600;
}

.budgets li {
  display: grid;
  grid-template-columns: 1fr 110px auto auto;
  gap: var(--space-2);
  align-items: center;
}

.limit-input {
  padding: 0.4rem 0.6rem;
  border: 1px solid var(--color-border-strong);
  border-radius: var(--radius-sm);
  background: var(--color-surface);
  text-align: right;
}

.remove {
  border: none;
  background: none;
  color: var(--color-text-muted);
  cursor: pointer;
}

.new-budget {
  display: grid;
  grid-template-columns: 1fr 120px auto;
  gap: var(--space-2);
  align-items: end;
  margin-top: var(--space-5);
  padding-top: var(--space-4);
  border-top: 1px solid var(--color-border);
}

.card-body {
  padding: var(--space-5) var(--space-6);
}

.category-select {
  padding: 0.35rem 0.5rem;
  border: 1px solid var(--color-border-strong);
  border-radius: var(--radius-sm);
  background: var(--color-surface);
}

.hint {
  margin-left: var(--space-2);
  font-size: var(--font-size-xs);
}

.numeric {
  text-align: right;
}

.nowrap {
  white-space: nowrap;
}

@media (max-width: 1000px) {
  .layout {
    grid-template-columns: minmax(0, 1fr);
  }
}
</style>
