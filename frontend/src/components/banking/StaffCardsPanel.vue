<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue'
import AppCard from '@/components/ui/AppCard.vue'
import AppBadge from '@/components/ui/AppBadge.vue'
import AppButton from '@/components/ui/AppButton.vue'
import AppField from '@/components/ui/AppField.vue'
import AppModal from '@/components/ui/AppModal.vue'
import MoneyAmount from '@/components/ui/MoneyAmount.vue'
import StatusViews from '@/components/ui/StatusViews.vue'
import { cardsApi } from '@/api/bank'
import { useUiStore } from '@/stores/ui'
import { errorMessage } from '@/composables/useAsync'
import { cardStatusLabels } from '@/utils/labels'
import { formatAccountNumber, formatDate } from '@/utils/format'
import type { Account, CardBrand, PaymentCard } from '@/types/api'

const props = defineProps<{ userId: number; accounts: Account[]; canIssue: boolean }>()

const ui = useUiStore()
const cards = ref<PaymentCard[]>([])
const isLoading = ref(true)
const error = ref<string | null>(null)
const issue = reactive({ open: false, accountId: null as number | null, brand: 'Dina' as CardBrand, error: null as string | null, saving: false })
const block = reactive({ card: null as PaymentCard | null, reason: '', error: null as string | null, saving: false })

const eligibleAccounts = computed(() => props.accounts.filter((account) => account.kind === 'Tekuci' && account.status === 'Aktivan'))
const accountOptions = computed(() => eligibleAccounts.value.map((account) => ({ value: account.id, label: `${formatAccountNumber(account.accountNumber)} (${account.currency})` })))
const brandOptions = (['Dina', 'Visa', 'Mastercard'] as CardBrand[]).map((value) => ({ value, label: value }))

async function load(): Promise<void> {
  isLoading.value = true
  error.value = null
  try {
    cards.value = await cardsApi.forUser(props.userId)
  } catch (caught) {
    error.value = errorMessage(caught)
  } finally {
    isLoading.value = false
  }
}

function openIssue(): void {
  Object.assign(issue, { open: true, accountId: (accountOptions.value[0]?.value as number | undefined) ?? null, brand: 'Dina', error: null })
}

async function submitIssue(): Promise<void> {
  if (!issue.accountId) {
    issue.error = 'Izaberite račun.'
    return
  }
  issue.saving = true
  issue.error = null
  try {
    const card = await cardsApi.issue(issue.accountId, issue.brand)
    issue.open = false
    ui.notify(`Izdata je ${card.brand} kartica ...${card.last4}.`)
    await load()
  } catch (caught) {
    issue.error = errorMessage(caught)
  } finally {
    issue.saving = false
  }
}

async function submitBlock(): Promise<void> {
  block.saving = true
  block.error = null
  try {
    await cardsApi.block(block.card!.id, block.reason.trim() || null)
    block.card = null
    ui.notify('Kartica je blokirana. Klijent je ne može sam odblokirati.')
    await load()
  } catch (caught) {
    block.error = errorMessage(caught)
  } finally {
    block.saving = false
  }
}

async function unblock(card: PaymentCard): Promise<void> {
  try {
    await cardsApi.unblock(card.id)
    ui.notify('Kartica je odblokirana.')
    await load()
  } catch (caught) {
    ui.notify(errorMessage(caught), 'danger')
  }
}

watch(() => props.userId, load)
onMounted(load)
</script>

<template>
  <AppCard title="Kartice klijenta" subtitle="Debitne kartice uz tekuće račune. Puni broj kartice se ne čuva, vide se samo poslednje 4 cifre." :padded="false">
    <template #actions>
      <AppButton size="sm" icon="plus" :disabled="!canIssue || !eligibleAccounts.length" @click="openIssue">Izdaj karticu</AppButton>
    </template>
    <div class="card-body">
      <StatusViews v-if="isLoading" state="loading" :rows="2" />
      <StatusViews v-else-if="error" state="error" :message="error" @retry="load" />
      <StatusViews v-else-if="!cards.length" state="empty" icon="card" title="Klijent nema kartice" :message="canIssue ? 'Kartica se izdaje uz aktivan tekući račun.' : 'Kartica se izdaje tek kada je identitet klijenta verifikovan.'" />
      <div v-else class="table-scroll">
        <table class="data-table">
          <thead>
            <tr>
              <th scope="col">Kartica</th>
              <th scope="col">Račun</th>
              <th scope="col">Važi do</th>
              <th scope="col">Status</th>
              <th scope="col" class="numeric">Dnevni limit</th>
              <th scope="col"><span class="visually-hidden">Radnje</span></th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="card in cards" :key="card.id">
              <td><strong>{{ card.brand }}</strong> <span class="mono">...{{ card.last4 }}</span></td>
              <td class="mono">{{ card.accountNumber ? formatAccountNumber(card.accountNumber) : '' }}</td>
              <td>{{ card.expiry }}</td>
              <td>
                <AppBadge :tone="cardStatusLabels[card.status].tone">{{ cardStatusLabels[card.status].label }}</AppBadge>
                <div v-if="card.statusReason" class="subtle small">{{ card.statusReason }}</div>
                <div v-if="card.cancelledAt" class="subtle small">{{ formatDate(card.cancelledAt) }}</div>
              </td>
              <td class="numeric"><MoneyAmount :amount="card.dailyPurchaseLimit" :currency="card.currency" :sensitive="false" /></td>
              <td class="actions">
                <AppButton v-if="card.status === 'Aktivna'" size="sm" variant="ghost" @click="Object.assign(block, { card, reason: '', error: null })">Blokiraj</AppButton>
                <AppButton v-else-if="card.status === 'Blokirana'" size="sm" variant="secondary" @click="unblock(card)">Odblokiraj</AppButton>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>

    <AppModal :open="issue.open" title="Izdavanje kartice" width="440px" @close="issue.open = false">
      <form id="issue-card-form" class="stack" @submit.prevent="submitIssue">
        <AppField v-model="issue.accountId" label="Tekući račun" :options="accountOptions" />
        <AppField v-model="issue.brand" label="Vrsta kartice" :options="brandOptions" />
        <p class="subtle">Kartica važi 3 godine. Podrazumevani dnevni limiti su 100.000 RSD za plaćanje i 40.000 RSD na bankomatu (za devizne račune 1.000 i 400). Plaćanje u inostranstvu je isključeno dok ga klijent ne uključi.</p>
        <p v-if="issue.error" class="form-error" role="alert">{{ issue.error }}</p>
      </form>
      <template #footer>
        <AppButton variant="ghost" @click="issue.open = false">Odustani</AppButton>
        <AppButton type="submit" form="issue-card-form" :loading="issue.saving">Izdaj</AppButton>
      </template>
    </AppModal>

    <AppModal :open="block.card !== null" title="Blokiranje kartice" width="440px" @close="block.card = null">
      <form id="block-card-form" class="stack" @submit.prevent="submitBlock">
        <p class="muted">Kartica ...{{ block.card?.last4 }} biće blokirana od strane banke. Klijent je neće moći sam da odblokira.</p>
        <AppField v-model="block.reason" label="Razlog" :maxlength="200" placeholder="npr. Sumnja na zloupotrebu" />
        <p v-if="block.error" class="form-error" role="alert">{{ block.error }}</p>
      </form>
      <template #footer>
        <AppButton variant="ghost" @click="block.card = null">Odustani</AppButton>
        <AppButton type="submit" form="block-card-form" variant="danger" :loading="block.saving">Blokiraj</AppButton>
      </template>
    </AppModal>
  </AppCard>
</template>

<style scoped>
.small {
  font-size: var(--font-size-xs);
}

.actions {
  text-align: right;
  white-space: nowrap;
}

.form-error {
  color: var(--color-danger);
  font-size: var(--font-size-sm);
}
</style>
