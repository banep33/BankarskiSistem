<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import AppCard from '@/components/ui/AppCard.vue'
import AppBadge from '@/components/ui/AppBadge.vue'
import AppButton from '@/components/ui/AppButton.vue'
import AppField from '@/components/ui/AppField.vue'
import AppModal from '@/components/ui/AppModal.vue'
import AppPagination from '@/components/ui/AppPagination.vue'
import MoneyAmount from '@/components/ui/MoneyAmount.vue'
import StatusViews from '@/components/ui/StatusViews.vue'
import PaymentCardVisual from '@/components/banking/PaymentCardVisual.vue'
import { budgetsApi, cardsApi } from '@/api/bank'
import { useUiStore } from '@/stores/ui'
import { useReauthStore } from '@/stores/reauth'
import { errorMessage } from '@/composables/useAsync'
import { cardChannelLabels } from '@/utils/labels'
import { formatDateTime, formatMoney } from '@/utils/format'
import type { CardAuthorization, CardChannel, PagedResponse, PaymentCard, SpendingCategory } from '@/types/api'

const ui = useUiStore()
const reauth = useReauthStore()
const cards = ref<PaymentCard[]>([])
const categories = ref<SpendingCategory[]>([])
const selectedId = ref<number | null>(null)
const isLoading = ref(true)
const error = ref<string | null>(null)
const settings = reactive({ onlineEnabled: true, contactlessEnabled: true, abroadEnabled: false, dailyPurchaseLimit: '' as string | number, dailyAtmLimit: '' as string | number, saving: false, error: null as string | null })
const simulation = reactive({
  channel: 'Prodavnica' as CardChannel,
  merchantName: '',
  amount: '' as string | number,
  isAbroad: false,
  contactless: true,
  categoryCode: '',
  saving: false,
  error: null as string | null,
  result: null as CardAuthorization | null,
})
const lost = reactive({ open: false, reissue: true, saving: false, error: null as string | null })
const history = ref<PagedResponse<CardAuthorization> | null>(null)
const historyPage = ref(1)

const selected = computed(() => cards.value.find((card) => card.id === selectedId.value) ?? null)
const usable = computed(() => selected.value !== null && selected.value.status !== 'Ukinuta')
const channelOptions = (Object.keys(cardChannelLabels) as CardChannel[]).map((value) => ({ value, label: cardChannelLabels[value] }))
const categoryOptions = computed(() => [
  { value: '', label: 'Automatski (kupovina)' },
  ...categories.value.filter((category) => !['GOTOVINA', 'STEDNJA', 'KREDITI', 'POREZI'].includes(category.code)).map((category) => ({ value: category.code, label: category.name })),
])

async function load(keepSelection = true): Promise<void> {
  isLoading.value = true
  error.value = null
  try {
    ;[cards.value, categories.value] = await Promise.all([cardsApi.mine(), categories.value.length ? Promise.resolve(categories.value) : budgetsApi.categories()])
    if (!keepSelection || !cards.value.some((card) => card.id === selectedId.value)) selectedId.value = cards.value[0]?.id ?? null
    syncSettings()
    await loadHistory(1)
  } catch (caught) {
    error.value = errorMessage(caught)
  } finally {
    isLoading.value = false
  }
}

function select(card: PaymentCard): void {
  selectedId.value = card.id
  simulation.result = null
  syncSettings()
  void loadHistory(1)
}

function syncSettings(): void {
  const card = selected.value
  if (!card) return
  Object.assign(settings, {
    onlineEnabled: card.onlineEnabled,
    contactlessEnabled: card.contactlessEnabled,
    abroadEnabled: card.abroadEnabled,
    dailyPurchaseLimit: card.dailyPurchaseLimit,
    dailyAtmLimit: card.dailyAtmLimit,
    error: null,
  })
}

async function loadHistory(page: number): Promise<void> {
  if (!selectedId.value) {
    history.value = null
    return
  }
  historyPage.value = page
  history.value = await cardsApi.authorizations(selectedId.value, page, 10)
}

function replaceCard(updated: PaymentCard): void {
  cards.value = cards.value.map((card) => (card.id === updated.id ? updated : card))
}

function toNumber(value: string | number): number {
  return Number(String(value).replace(',', '.'))
}

async function saveSettings(): Promise<void> {
  const card = selected.value!
  const purchase = toNumber(settings.dailyPurchaseLimit)
  const atm = toNumber(settings.dailyAtmLimit)
  if (!(purchase >= 0 && purchase <= card.maxPurchaseLimit) || !(atm >= 0 && atm <= card.maxAtmLimit)) {
    settings.error = `Limit za plaćanje može biti do ${formatMoney(card.maxPurchaseLimit, card.currency)}, a za bankomat do ${formatMoney(card.maxAtmLimit, card.currency)}.`
    return
  }
  settings.saving = true
  settings.error = null
  try {
    replaceCard(await cardsApi.updateSettings(card.id, { onlineEnabled: settings.onlineEnabled, contactlessEnabled: settings.contactlessEnabled, abroadEnabled: settings.abroadEnabled, dailyPurchaseLimit: purchase, dailyAtmLimit: atm }))
    ui.notify('Podešavanja kartice su sačuvana i važe odmah.')
  } catch (caught) {
    settings.error = errorMessage(caught)
  } finally {
    settings.saving = false
  }
}

async function toggleBlock(): Promise<void> {
  const card = selected.value!
  try {
    replaceCard(card.status === 'Blokirana' ? await cardsApi.unblock(card.id) : await cardsApi.block(card.id))
    ui.notify(card.status === 'Blokirana' ? 'Kartica je ponovo aktivna.' : 'Kartica je privremeno blokirana. Možete je odblokirati u svakom trenutku.')
  } catch (caught) {
    ui.notify(errorMessage(caught), 'danger')
  }
}

async function reportLost(): Promise<void> {
  const card = selected.value!
  lost.saving = true
  lost.error = null
  try {
    const result = await reauth.runWithReauth(() => cardsApi.reportLost(card.id, lost.reissue), 'Za trajno ukidanje kartice potrebna je potvrda lozinkom.')
    if (result === null) return
    lost.open = false
    ui.notify(result ? `Kartica je ukinuta. Izdata je zamenska kartica ...${result.last4} sa istim podešavanjima.` : 'Kartica je trajno ukinuta.')
    if (result) selectedId.value = result.id
    await load()
  } catch (caught) {
    lost.error = errorMessage(caught)
  } finally {
    lost.saving = false
  }
}

async function simulate(): Promise<void> {
  const amount = toNumber(simulation.amount)
  if (!(amount > 0) || !simulation.merchantName.trim()) {
    simulation.error = 'Unesite naziv trgovca i iznos veći od nule.'
    return
  }
  simulation.saving = true
  simulation.error = null
  try {
    simulation.result = await cardsApi.simulate(selected.value!.id, {
      channel: simulation.channel,
      merchantName: simulation.merchantName.trim(),
      amount,
      isAbroad: simulation.isAbroad,
      contactless: simulation.channel === 'Prodavnica' && simulation.contactless,
      categoryCode: simulation.channel === 'Bankomat' || !simulation.categoryCode ? null : simulation.categoryCode,
    })
    await loadHistory(1)
  } catch (caught) {
    simulation.error = errorMessage(caught)
  } finally {
    simulation.saving = false
  }
}

onMounted(() => load(false))
</script>

<template>
  <div class="page">
    <header class="page-header">
      <div>
        <h1 class="page-title">Kartice</h1>
        <p class="page-subtitle">Debitne kartice vezane za tekuće račune. Ovde menjate limite i dozvole, privremeno blokirate karticu ili prijavljujete gubitak.</p>
      </div>
    </header>

    <StatusViews v-if="isLoading && !cards.length" state="loading" :rows="4" />
    <StatusViews v-else-if="error" state="error" :message="error" @retry="load()" />
    <StatusViews v-else-if="!cards.length" state="empty" icon="card" title="Nemate kartice" message="Karticu izdaje banka uz tekući račun. Obratite se svom bankaru." />

    <template v-else>
      <div class="card-strip" role="list">
        <button v-for="card in cards" :key="card.id" type="button" role="listitem" class="card-button" :aria-pressed="card.id === selectedId" :aria-label="`${card.brand} kartica ${card.last4}`" @click="select(card)">
          <PaymentCardVisual :card="card" :selected="card.id === selectedId" />
        </button>
      </div>

      <template v-if="selected">
        <AppCard v-if="selected.status !== 'Aktivna'" tone="cream">
          <p v-if="selected.status === 'Ukinuta'"><strong>Kartica je trajno ukinuta.</strong> {{ selected.statusReason }}. Ostaje vidljiva zbog istorije transakcija.</p>
          <p v-else-if="selected.blockedByBank"><strong>Karticu je blokirala banka.</strong> {{ selected.statusReason }}. Za odblokiranje se obratite banci.</p>
          <p v-else><strong>Kartica je privremeno blokirana.</strong> Sve transakcije se odbijaju dok je ne odblokirate.</p>
        </AppCard>

        <div class="grid-2">
          <AppCard title="Podešavanja" subtitle="Promene važe odmah, za sledeću transakciju.">
            <form class="stack" @submit.prevent="saveSettings">
              <label class="check"><input v-model="settings.onlineEnabled" type="checkbox" :disabled="!usable" /> Plaćanje na internetu</label>
              <label class="check"><input v-model="settings.contactlessEnabled" type="checkbox" :disabled="!usable" /> Beskontaktno plaćanje</label>
              <label class="check"><input v-model="settings.abroadEnabled" type="checkbox" :disabled="!usable" /> Plaćanje u inostranstvu</label>
              <AppField v-model="settings.dailyPurchaseLimit" :label="`Dnevni limit za plaćanje (${selected.currency}), najviše ${formatMoney(selected.maxPurchaseLimit, selected.currency)}`" inputmode="decimal" :disabled="!usable" />
              <AppField v-model="settings.dailyAtmLimit" :label="`Dnevni limit na bankomatu (${selected.currency}), najviše ${formatMoney(selected.maxAtmLimit, selected.currency)}`" inputmode="decimal" :disabled="!usable" />
              <p v-if="settings.error" class="form-error" role="alert">{{ settings.error }}</p>
              <div class="row-actions">
                <AppButton type="submit" :loading="settings.saving" :disabled="!usable">Sačuvaj</AppButton>
              </div>
            </form>
          </AppCard>

          <AppCard title="Bezbednost kartice">
            <div class="stack">
              <p class="muted">Privremena blokada je korisna kada niste sigurni gde je kartica. Odblokiranje je trenutno.</p>
              <AppButton
                v-if="selected.status !== 'Ukinuta' && !selected.blockedByBank"
                :variant="selected.status === 'Blokirana' ? 'primary' : 'secondary'"
                icon="lock"
                @click="toggleBlock"
              >
                {{ selected.status === 'Blokirana' ? 'Odblokiraj karticu' : 'Privremeno blokiraj' }}
              </AppButton>
              <p class="muted">Ako je kartica izgubljena ili ukradena, trajno je ukinite. Ukinuta kartica se ne može ponovo aktivirati.</p>
              <AppButton v-if="selected.status !== 'Ukinuta'" variant="danger" icon="alert" @click="Object.assign(lost, { open: true, reissue: true, error: null })">Prijavi gubitak ili krađu</AppButton>
              <dl class="facts">
                <div><dt>Račun</dt><dd class="mono">{{ selected.accountNumber }}</dd></div>
                <div><dt>Važi do</dt><dd>{{ selected.expiry }}</dd></div>
                <div><dt>Izdata</dt><dd>{{ formatDateTime(selected.createdAt) }}</dd></div>
              </dl>
            </div>
          </AppCard>
        </div>

        <AppCard v-if="usable" title="Simulator plaćanja karticom" subtitle="Aplikacija nije povezana sa pravom kartičarskom mrežom. Ovde zadajete transakciju kao da je stigla sa terminala trgovca ili bankomata; provere i knjiženje su stvarni.">
          <form class="sim-grid" @submit.prevent="simulate">
            <AppField v-model="simulation.channel" label="Gde se plaća" :options="channelOptions" />
            <AppField v-model="simulation.merchantName" :label="simulation.channel === 'Bankomat' ? 'Bankomat' : 'Trgovac'" :maxlength="80" :placeholder="simulation.channel === 'Bankomat' ? 'npr. Bankomat Knez Mihailova' : 'npr. Maxi'" />
            <AppField v-model="simulation.amount" :label="`Iznos (${selected.currency})`" inputmode="decimal" />
            <AppField v-if="simulation.channel !== 'Bankomat'" v-model="simulation.categoryCode" label="Kategorija trgovca" :options="categoryOptions" />
            <label class="check"><input v-model="simulation.isAbroad" type="checkbox" /> Transakcija u inostranstvu</label>
            <label v-if="simulation.channel === 'Prodavnica'" class="check"><input v-model="simulation.contactless" type="checkbox" /> Beskontaktno</label>
            <div class="sim-actions">
              <AppButton type="submit" icon="card" :loading="simulation.saving">Izvrši transakciju</AppButton>
            </div>
          </form>
          <p v-if="simulation.error" class="form-error" role="alert">{{ simulation.error }}</p>
          <div v-if="simulation.result" class="sim-result" :class="simulation.result.status === 'Odobrena' ? 'ok' : 'declined'" role="status">
            <strong>{{ simulation.result.status === 'Odobrena' ? 'Odobreno' : 'Odbijeno' }}:</strong>
            {{ formatMoney(simulation.result.amount, simulation.result.currency) }}, {{ simulation.result.merchantName }}.
            <span v-if="simulation.result.declineReason"> {{ simulation.result.declineReason }}</span>
            <span v-else> Iznos je skinut sa računa i vidi se u transakcijama i potrošnji.</span>
          </div>
        </AppCard>

        <AppCard title="Istorija transakcija kartice" subtitle="Sve odobrene i odbijene transakcije, sa razlogom odbijanja." :padded="false">
          <div class="history">
            <StatusViews v-if="!history?.items.length" state="empty" icon="list" title="Još nema transakcija ovom karticom" />
            <ul v-else class="auth-list">
              <li v-for="item in history.items" :key="item.id">
                <div>
                  <strong>{{ item.merchantName }}</strong>
                  <div class="subtle small">{{ formatDateTime(item.createdAt) }} · {{ cardChannelLabels[item.channel] }}<template v-if="item.isAbroad"> · inostranstvo</template></div>
                  <div v-if="item.declineReason" class="decline small">{{ item.declineReason }}</div>
                </div>
                <div class="right">
                  <MoneyAmount :amount="item.amount" :currency="item.currency" :sensitive="false" />
                  <AppBadge :tone="item.status === 'Odobrena' ? 'success' : 'danger'">{{ item.status }}</AppBadge>
                </div>
              </li>
            </ul>
            <AppPagination v-if="history && history.totalPages > 1" :page="historyPage" :total-pages="history.totalPages" :total-count="history.totalCount" @change="loadHistory" />
          </div>
        </AppCard>
      </template>
    </template>

    <AppModal :open="lost.open" title="Prijava gubitka ili krađe" width="460px" @close="lost.open = false">
      <div class="stack">
        <p class="muted">Kartica ...{{ selected?.last4 }} biće <strong>trajno ukinuta</strong> i sve dalje transakcije njome biće odbijene. Novac na računu ostaje netaknut.</p>
        <label class="check"><input v-model="lost.reissue" type="checkbox" /> Izdaj zamensku karticu sa novim brojem i istim podešavanjima</label>
        <p v-if="lost.error" class="form-error" role="alert">{{ lost.error }}</p>
      </div>
      <template #footer>
        <AppButton variant="ghost" @click="lost.open = false">Odustani</AppButton>
        <AppButton variant="danger" :loading="lost.saving" @click="reportLost">Trajno ukini</AppButton>
      </template>
    </AppModal>
  </div>
</template>

<style scoped>
.card-strip {
  display: flex;
  gap: var(--space-4);
  overflow-x: auto;
  padding: var(--space-1) var(--space-1) var(--space-3);
}

.card-button {
  flex: 0 0 min(300px, 85vw);
  padding: 0;
  border: 0;
  background: none;
  cursor: pointer;
  text-align: left;
  border-radius: var(--radius-lg);
}

.card-button:focus-visible {
  outline: 3px solid var(--color-primary);
  outline-offset: 2px;
}

.check {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  font-size: var(--font-size-sm);
}

.row-actions {
  display: flex;
  justify-content: flex-end;
}

.facts {
  display: grid;
  gap: var(--space-2);
  margin: var(--space-2) 0 0;
  padding-top: var(--space-3);
  border-top: 1px solid var(--color-border);
  font-size: var(--font-size-sm);
}

.facts div {
  display: flex;
  justify-content: space-between;
  gap: var(--space-3);
}

.facts dt {
  color: var(--color-text-muted);
}

.facts dd {
  margin: 0;
}

.sim-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: var(--space-3) var(--space-4);
  align-items: end;
}

.sim-actions {
  display: flex;
  justify-content: flex-end;
}

.sim-result {
  margin-top: var(--space-4);
  padding: var(--space-3) var(--space-4);
  border-radius: var(--radius-md);
  font-size: var(--font-size-sm);
}

.sim-result.ok {
  background: var(--color-success-soft, rgb(34 139 34 / 0.12));
}

.sim-result.declined {
  background: var(--color-danger-soft, rgb(200 40 40 / 0.12));
}

.history {
  padding: var(--space-4) var(--space-6) var(--space-5);
}

.auth-list {
  list-style: none;
  margin: 0;
  padding: 0;
}

.auth-list li {
  display: flex;
  justify-content: space-between;
  gap: var(--space-4);
  padding: var(--space-3) 0;
  border-bottom: 1px solid var(--color-border);
}

.auth-list li:last-child {
  border-bottom: 0;
}

.right {
  display: flex;
  flex-direction: column;
  align-items: flex-end;
  gap: var(--space-1);
  white-space: nowrap;
}

.small {
  font-size: var(--font-size-xs);
}

.decline {
  color: var(--color-danger);
}

.form-error {
  color: var(--color-danger);
  font-size: var(--font-size-sm);
}
</style>
