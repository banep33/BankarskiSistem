<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { RouterLink } from 'vue-router'
import AppCard from '@/components/ui/AppCard.vue'
import AppButton from '@/components/ui/AppButton.vue'
import AppField from '@/components/ui/AppField.vue'
import AppBadge from '@/components/ui/AppBadge.vue'
import AppIcon from '@/components/ui/AppIcon.vue'
import AppPagination from '@/components/ui/AppPagination.vue'
import StatusViews from '@/components/ui/StatusViews.vue'
import { privacyApi } from '@/api/bank'
import { useReauthStore } from '@/stores/reauth'
import { useUiStore } from '@/stores/ui'
import { errorMessage } from '@/composables/useAsync'
import { privacyRequestStatusLabels, privacyRequestTypeLabels } from '@/utils/labels'
import { formatDate, formatDateTime } from '@/utils/format'
import type { ConsentEvent, ConsentRecord, DataAccessEntry, PagedResponse, Preferences, PrivacyRequest, PrivacyRequestType } from '@/types/api'

type Tab = 'podesavanja' | 'dokumenti' | 'pristup' | 'zahtevi' | 'izvoz'

const ui = useUiStore()
const reauth = useReauthStore()
const tab = ref<Tab>('podesavanja')
const tabs: { id: Tab; label: string; icon: string }[] = [
  { id: 'podesavanja', label: 'Podešavanja i saglasnosti', icon: 'settings' },
  { id: 'dokumenti', label: 'Dokumenti', icon: 'file' },
  { id: 'pristup', label: 'Ko je video moje podatke', icon: 'eye' },
  { id: 'zahtevi', label: 'Zahtevi po ZZPL', icon: 'chat' },
  { id: 'izvoz', label: 'Preuzmi moje podatke', icon: 'download' },
]

const preferences = ref<Preferences | null>(null)
const consentHistory = ref<ConsentEvent[]>([])
const consents = ref<ConsentRecord[]>([])
const dataAccess = ref<PagedResponse<DataAccessEntry> | null>(null)
const dataAccessPage = ref(1)
const requests = ref<PrivacyRequest[]>([])
const isLoading = ref(false)
const error = ref<string | null>(null)
const savingSection = ref<'notifications' | 'privacy' | null>(null)
const saved = ref<Preferences | null>(null)
const isExporting = ref(false)
const isSubmittingRequest = ref(false)
const thresholdEnabled = ref(false)
const threshold = ref<string | number>('')
const requestForm = reactive({ type: 'Pristup' as PrivacyRequestType, details: '' })

const notificationToggles: { key: keyof Preferences; label: string; hint: string; locked?: boolean }[] = [
  { key: 'notifyIncomingPayments', label: 'Primljene uplate', hint: 'Obaveštenje kada novac stigne na vaš račun.' },
  { key: 'notifyOutgoingPayments', label: 'Izvršena plaćanja', hint: 'Potvrda za svako plaćanje sa vašeg računa.' },
  { key: 'notifyAccountChanges', label: 'Promene na računu', hint: 'Otvaranje, blokada ili promena statusa računa.' },
  { key: 'notifyComplaintUpdates', label: 'Prigovori', hint: 'Odgovori i promene statusa vaših prigovora.' },
  { key: 'notifySecurityEvents', label: 'Bezbednosna obaveštenja', hint: 'Uvek uključeno radi zaštite naloga.', locked: true },
]

const notificationKeys = ['notifyIncomingPayments', 'notifyOutgoingPayments', 'notifyAccountChanges', 'notifyComplaintUpdates'] as const
const privacyKeys = ['marketingConsent', 'hideBalancesByDefault'] as const

const notificationsDirty = computed(() =>
  preferences.value !== null && saved.value !== null && notificationKeys.some((key) => preferences.value![key] !== saved.value![key]),
)
const privacyDirty = computed(() => {
  if (!preferences.value || !saved.value) return false
  const currentThreshold = thresholdEnabled.value ? String(threshold.value) : ''
  const savedThreshold = saved.value.extraConfirmationThreshold === null ? '' : String(saved.value.extraConfirmationThreshold)
  return privacyKeys.some((key) => preferences.value![key] !== saved.value![key]) || currentThreshold !== savedThreshold
})

const requestTypeOptions = Object.entries(privacyRequestTypeLabels).map(([value, label]) => ({ value, label }))

async function loadTab(): Promise<void> {
  isLoading.value = true
  error.value = null
  try {
    if (tab.value === 'podesavanja') {
      ;[preferences.value, consentHistory.value] = await Promise.all([privacyApi.preferences(), privacyApi.consentHistory()])
      saved.value = { ...preferences.value }
      thresholdEnabled.value = preferences.value.extraConfirmationThreshold !== null
      threshold.value = preferences.value.extraConfirmationThreshold ?? ''
    } else if (tab.value === 'dokumenti') {
      consents.value = await privacyApi.consents()
    } else if (tab.value === 'pristup') {
      dataAccess.value = await privacyApi.dataAccess(dataAccessPage.value)
    } else if (tab.value === 'zahtevi') {
      requests.value = await privacyApi.requests()
    }
  } catch (caught) {
    error.value = errorMessage(caught)
  } finally {
    isLoading.value = false
  }
}

async function savePreferences(section: 'notifications' | 'privacy'): Promise<void> {
  if (!preferences.value || !saved.value) return
  const value = Number(String(threshold.value).replace(',', '.'))
  if (section === 'privacy' && thresholdEnabled.value && (!Number.isFinite(value) || value <= 0)) {
    ui.notify('Prag za dodatnu potvrdu mora biti veći od nule.', 'danger')
    return
  }
  const current = preferences.value
  const base = saved.value
  const source = section === 'notifications' ? current : base
  const privacySource = section === 'privacy' ? current : base
  savingSection.value = section
  try {
    const result = await privacyApi.updatePreferences({
      notifyIncomingPayments: source.notifyIncomingPayments,
      notifyOutgoingPayments: source.notifyOutgoingPayments,
      notifyAccountChanges: source.notifyAccountChanges,
      notifyComplaintUpdates: source.notifyComplaintUpdates,
      marketingConsent: privacySource.marketingConsent,
      hideBalancesByDefault: privacySource.hideBalancesByDefault,
      extraConfirmationThreshold: section === 'privacy' ? (thresholdEnabled.value ? value : null) : base.extraConfirmationThreshold,
    })
    saved.value = { ...result }
    const keep = section === 'notifications' ? privacyKeys : notificationKeys
    preferences.value = { ...result, ...Object.fromEntries(keep.map((key) => [key, current[key]])) }
    if (section === 'privacy') {
      if (result.hideBalancesByDefault && !ui.hideBalances) ui.toggleBalances()
      consentHistory.value = await privacyApi.consentHistory()
    }
    ui.notify(section === 'notifications' ? 'Podešavanja obaveštenja su sačuvana.' : 'Podešavanja privatnosti su sačuvana.')
  } catch (caught) {
    ui.notify(errorMessage(caught), 'danger')
  } finally {
    savingSection.value = null
  }
}

async function exportData(): Promise<void> {
  isExporting.value = true
  try {
    const data = await reauth.runWithReauth(() => privacyApi.exportData(), 'Izvoz ličnih podataka zahteva potvrdu lozinkom.')
    if (!data) return
    const blob = new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' })
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = `moji-podaci-${new Date().toISOString().slice(0, 10)}.json`
    link.click()
    URL.revokeObjectURL(url)
    ui.notify('Vaši podaci su preuzeti. Čuvajte fajl na bezbednom mestu.')
  } catch (caught) {
    ui.notify(errorMessage(caught), 'danger')
  } finally {
    isExporting.value = false
  }
}

async function submitRequest(): Promise<void> {
  if (requestForm.details.trim().length < 10) {
    ui.notify('Opis zahteva mora imati najmanje 10 karaktera.', 'danger')
    return
  }
  isSubmittingRequest.value = true
  try {
    const created = await privacyApi.submitRequest(requestForm.type, requestForm.details.trim())
    requestForm.details = ''
    ui.notify(`Zahtev ${created.referenceNumber} je primljen. Rok za odgovor je ${formatDate(created.dueAt)}.`)
    requests.value = await privacyApi.requests()
  } catch (caught) {
    ui.notify(errorMessage(caught), 'danger')
  } finally {
    isSubmittingRequest.value = false
  }
}

function consentLabel(type: string): string {
  const labels: Record<string, string> = { Marketing: 'Marketinške poruke', SakrivanjeStanja: 'Sakrivanje stanja' }
  return labels[type] ?? type
}

watch(tab, loadTab)
watch(dataAccessPage, loadTab)
onMounted(loadTab)
</script>

<template>
  <div class="page">
    <header class="page-header">
      <div>
        <h1 class="page-title">Privatnost</h1>
        <p class="page-subtitle">Vaši podaci, vaša pravila. Ovde ostvarujete prava iz Zakona o zaštiti podataka o ličnosti.</p>
      </div>
      <RouterLink to="/javno/dokumenti/privatnost" class="policy-link">Politika privatnosti</RouterLink>
    </header>

    <div class="tabs" role="tablist" aria-label="Odeljci privatnosti">
      <button v-for="item in tabs" :key="item.id" type="button" role="tab" :aria-selected="tab === item.id" :class="{ active: tab === item.id }" @click="tab = item.id">
        <AppIcon :name="item.icon" :size="16" /> {{ item.label }}
      </button>
    </div>

    <StatusViews v-if="isLoading" state="loading" :rows="4" />
    <StatusViews v-else-if="error" state="error" :message="error" @retry="loadTab" />

    <template v-else-if="tab === 'podesavanja' && preferences">
      <div class="grid-2 align-start">
        <AppCard title="Obaveštenja" subtitle="Birate šta želite da dobijate. Bezbednosna obaveštenja se ne mogu isključiti.">
          <label v-for="toggle in notificationToggles" :key="toggle.key" class="switch-row">
            <span>
              <strong>{{ toggle.label }}</strong>
              <small>{{ toggle.hint }}</small>
            </span>
            <input v-model="preferences[toggle.key]" type="checkbox" role="switch" :disabled="toggle.locked" />
          </label>
          <div class="card-footer">
            <span v-if="notificationsDirty" class="subtle">Imate nesačuvane izmene.</span>
            <AppButton :loading="savingSection === 'notifications'" :disabled="!notificationsDirty || savingSection !== null" @click="savePreferences('notifications')">Sačuvaj obaveštenja</AppButton>
          </div>
        </AppCard>

        <div class="stack">
          <AppCard title="Privatnost i zaštita">
            <label class="switch-row">
              <span>
                <strong>Marketinške poruke</strong>
                <small>Podrazumevano isključeno. Saglasnost možete povući u svakom trenutku.</small>
              </span>
              <input v-model="preferences.marketingConsent" type="checkbox" role="switch" />
            </label>
            <label class="switch-row">
              <span>
                <strong>Sakrij stanja pri prijavi</strong>
                <small>Korisno na javnim mestima. Iznosi se prikazuju klikom na ikonicu oka.</small>
              </span>
              <input v-model="preferences.hideBalancesByDefault" type="checkbox" role="switch" />
            </label>
            <label class="switch-row">
              <span>
                <strong>Lični prag za dodatnu potvrdu</strong>
                <small>Za plaćanja iznad ovog iznosa uvek tražimo lozinku, i kada je prag banke viši.</small>
              </span>
              <input v-model="thresholdEnabled" type="checkbox" role="switch" />
            </label>
            <AppField v-if="thresholdEnabled" v-model="threshold" label="Iznos praga" inputmode="decimal" placeholder="npr. 10000" />
            <div class="card-footer">
              <span v-if="privacyDirty" class="subtle">Imate nesačuvane izmene.</span>
              <AppButton :loading="savingSection === 'privacy'" :disabled="!privacyDirty || savingSection !== null" @click="savePreferences('privacy')">Sačuvaj podešavanja privatnosti</AppButton>
            </div>
          </AppCard>

          <AppCard title="Istorija saglasnosti">
            <ul v-if="consentHistory.length" class="history">
              <li v-for="(event, index) in consentHistory" :key="index">
                <AppBadge :tone="event.granted ? 'success' : 'neutral'">{{ event.granted ? 'Data' : 'Povučena' }}</AppBadge>
                <span>{{ consentLabel(event.consentType) }}</span>
                <span class="subtle">{{ formatDateTime(event.createdAt) }}</span>
              </li>
            </ul>
            <p v-else class="muted">Još niste menjali saglasnosti.</p>
          </AppCard>
        </div>
      </div>
    </template>

    <AppCard v-else-if="tab === 'dokumenti'" title="Prihvaćeni dokumenti" subtitle="Verzije koje ste prihvatili i kada.">
      <div class="table-scroll">
        <table class="data-table">
          <thead>
            <tr>
              <th scope="col">Dokument</th>
              <th scope="col">Verzija</th>
              <th scope="col">Prihvaćeno</th>
              <th scope="col"><span class="visually-hidden">Link</span></th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="consent in consents" :key="consent.legalDocumentId">
              <td><strong>{{ consent.title }}</strong></td>
              <td>{{ consent.version }}</td>
              <td>{{ formatDateTime(consent.acceptedAt) }}</td>
              <td>
                <RouterLink :to="`/javno/dokumenti/${consent.documentType === 'UsloviKoriscenja' ? 'uslovi' : 'privatnost'}`">Pročitaj</RouterLink>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </AppCard>

    <AppCard v-else-if="tab === 'pristup'" title="Ko je video moje podatke" subtitle="Svaki put kada zaposleni banke otvori vaše podatke, to se evidentira sa svrhom pristupa.">
      <StatusViews v-if="!dataAccess?.items.length" state="empty" icon="eye" title="Nema evidentiranih pristupa" message="Zaposleni do sada nisu otvarali vaše podatke." />
      <div v-else class="table-scroll">
        <table class="data-table">
          <thead>
            <tr>
              <th scope="col">Vreme</th>
              <th scope="col">Ko</th>
              <th scope="col">Šta</th>
              <th scope="col">Svrha</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="(entry, index) in dataAccess.items" :key="index">
              <td>{{ formatDateTime(entry.accessedAt) }}</td>
              <td>{{ entry.viewerLabel }} <span class="subtle">({{ entry.viewerRole }})</span></td>
              <td>{{ entry.resource }}</td>
              <td class="muted">{{ entry.purpose }}</td>
            </tr>
          </tbody>
        </table>
      </div>
      <AppPagination v-if="dataAccess" :page="dataAccess.page" :total-pages="dataAccess.totalPages" :total-count="dataAccess.totalCount" @change="dataAccessPage = $event" />
    </AppCard>

    <div v-else-if="tab === 'zahtevi'" class="grid-2 align-start">
      <AppCard title="Novi zahtev" subtitle="Banka odgovara u roku od 30 dana od prijema zahteva.">
        <form class="stack" @submit.prevent="submitRequest">
          <AppField v-model="requestForm.type" label="Vrsta zahteva" :options="requestTypeOptions" />
          <AppField v-model="requestForm.details" label="Opis zahteva" multiline :rows="5" :maxlength="5000" hint="Npr. koje podatke treba ispraviti ili na koju obradu se odnosi prigovor." />
          <p class="subtle">Napomena: podaci o transakcijama se čuvaju propisani broj godina radi sprečavanja pranja novca, pa brisanje može biti delimično.</p>
          <AppButton type="submit" :loading="isSubmittingRequest">Podnesi zahtev</AppButton>
        </form>
      </AppCard>
      <AppCard title="Moji zahtevi">
        <StatusViews v-if="!requests.length" state="empty" icon="chat" title="Nema podnetih zahteva" />
        <ul v-else class="requests">
          <li v-for="request in requests" :key="request.id">
            <div class="row-between">
              <strong>{{ privacyRequestTypeLabels[request.requestType] }}</strong>
              <AppBadge :tone="privacyRequestStatusLabels[request.status].tone">{{ privacyRequestStatusLabels[request.status].label }}</AppBadge>
            </div>
            <p class="subtle"><span class="mono">{{ request.referenceNumber }}</span> · podnet {{ formatDate(request.createdAt) }} · rok {{ formatDate(request.dueAt) }}</p>
            <p class="muted">{{ request.details }}</p>
            <p v-if="request.response" class="response"><strong>Odgovor:</strong> {{ request.response }}</p>
          </li>
        </ul>
      </AppCard>
    </div>

    <AppCard v-else-if="tab === 'izvoz'" title="Preuzmite svoje podatke" subtitle="Pravo na pristup i prenosivost podataka.">
      <div class="export">
        <div>
          <p>Fajl sadrži vaš profil, račune, transakcije, naloge, primaoce, prigovore, saglasnosti i bezbednosne događaje u mašinski čitljivom JSON formatu.</p>
          <ul class="export-list">
            <li><AppIcon name="lock" :size="16" /> Zbog osetljivosti podataka tražimo ponovni unos lozinke.</li>
            <li><AppIcon name="activity" :size="16" /> Svako preuzimanje se evidentira u bezbednosnom dnevniku.</li>
          </ul>
        </div>
        <AppButton icon="download" size="lg" :loading="isExporting" @click="exportData">Preuzmi JSON</AppButton>
      </div>
    </AppCard>
  </div>
</template>

<style scoped>
.policy-link {
  font-weight: 600;
  color: var(--color-link);
}

.tabs {
  display: flex;
  gap: var(--space-2);
  overflow-x: auto;
  padding-bottom: 2px;
}

.tabs button {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 0.55rem 1rem;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-pill);
  background: var(--color-surface);
  font-weight: 500;
  font-size: var(--font-size-sm);
  white-space: nowrap;
  cursor: pointer;
}

.tabs button.active {
  background: var(--color-contrast-bg);
  border-color: var(--color-contrast-bg);
  color: var(--color-contrast-text);
}

.align-start {
  align-items: start;
}

.switch-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: var(--space-4);
  padding: var(--space-3) 0;
  border-bottom: 1px solid var(--color-border);
  cursor: pointer;
}

.switch-row small {
  display: block;
  color: var(--color-text-muted);
  font-size: var(--font-size-xs);
}

.switch-row input {
  appearance: none;
  flex-shrink: 0;
  position: relative;
  width: 44px;
  height: 26px;
  border-radius: var(--radius-pill);
  background: var(--color-border-strong);
  cursor: pointer;
  transition: background var(--transition-fast);
}

.switch-row input::after {
  content: '';
  position: absolute;
  top: 3px;
  left: 3px;
  width: 20px;
  height: 20px;
  border-radius: 50%;
  background: #fff;
  transition: transform var(--transition-fast);
}

.switch-row input:checked {
  background: var(--color-forest-700);
}

.switch-row input:checked::after {
  transform: translateX(18px);
}

.switch-row input:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.switch-row input:focus-visible {
  box-shadow: var(--focus-ring);
}

.card-footer {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: flex-end;
  gap: var(--space-3);
  margin-top: var(--space-4);
}

.card-footer .subtle {
  margin-right: auto;
}

@media (max-width: 720px) {
  .tabs {
    display: grid;
    grid-template-columns: repeat(2, minmax(0, 1fr));
    overflow: visible;
  }

  .tabs button {
    justify-content: center;
    padding: 0.55rem 0.5rem;
    font-size: var(--font-size-xs);
  }

  .tabs button:last-child:nth-child(odd) {
    grid-column: span 2;
  }
}

@media (max-width: 520px) {
  .card-footer > :deep(.app-button) {
    width: 100%;
  }
}

.history {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: var(--space-2);
}

.history li {
  display: flex;
  align-items: center;
  gap: var(--space-3);
  flex-wrap: wrap;
  font-size: var(--font-size-sm);
}

.requests {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
}

.requests li + li {
  padding-top: var(--space-4);
  border-top: 1px solid var(--color-border);
}

.response {
  margin-top: var(--space-2);
  padding: var(--space-3);
  border-radius: var(--radius-md);
  background: var(--color-success-soft);
  font-size: var(--font-size-sm);
}

.export {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: var(--space-6);
  flex-wrap: wrap;
}

.export-list {
  list-style: none;
  margin: var(--space-4) 0 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: var(--space-2);
  font-size: var(--font-size-sm);
  color: var(--color-text-muted);
}

.export-list li {
  display: flex;
  gap: var(--space-2);
  align-items: center;
}
</style>
