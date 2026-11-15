<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import qrcode from 'qrcode-generator'
import AppCard from '@/components/ui/AppCard.vue'
import AppButton from '@/components/ui/AppButton.vue'
import AppBadge from '@/components/ui/AppBadge.vue'
import AppField from '@/components/ui/AppField.vue'
import AppIcon from '@/components/ui/AppIcon.vue'
import AppModal from '@/components/ui/AppModal.vue'
import StatusViews from '@/components/ui/StatusViews.vue'
import { securityApi } from '@/api/bank'
import { useReauthStore } from '@/stores/reauth'
import { useUiStore } from '@/stores/ui'
import { errorMessage } from '@/composables/useAsync'
import { formatDate } from '@/utils/format'
import type { TwoFactorStatus } from '@/types/api'

type Mode = 'setup' | 'disable' | 'regenerate' | 'codes'

const ui = useUiStore()
const reauth = useReauthStore()
const status = ref<TwoFactorStatus | null>(null)
const isLoading = ref(true)
const error = ref<string | null>(null)
const dialog = reactive({
  mode: null as Mode | null,
  secret: '',
  uri: '',
  code: '',
  codes: [] as string[],
  error: null as string | null,
  saving: false,
})

const qrDataUrl = computed(() => {
  if (!dialog.uri) return ''
  const qr = qrcode(0, 'M')
  qr.addData(dialog.uri)
  qr.make()
  return qr.createDataURL(5, 2)
})
const groupedSecret = computed(() => dialog.secret.match(/.{1,4}/g)?.join(' ') ?? '')
const dialogTitle = computed(() => {
  if (dialog.mode === 'setup') return 'Uključivanje dvostepene prijave'
  if (dialog.mode === 'disable') return 'Isključivanje dvostepene prijave'
  if (dialog.mode === 'regenerate') return 'Novi rezervni kodovi'
  return 'Sačuvajte rezervne kodove'
})

async function load(): Promise<void> {
  isLoading.value = true
  error.value = null
  try {
    status.value = await securityApi.twoFactorStatus()
  } catch (caught) {
    error.value = errorMessage(caught)
  } finally {
    isLoading.value = false
  }
}

function reset(mode: Mode | null): void {
  Object.assign(dialog, { mode, code: '', error: null, saving: false })
}

async function startSetup(): Promise<void> {
  try {
    const setup = await reauth.runWithReauth(() => securityApi.beginTwoFactorSetup(), 'Za uključivanje dvostepene prijave potvrdite lozinku.')
    if (!setup) return
    reset('setup')
    dialog.secret = setup.secret
    dialog.uri = setup.otpAuthUri
  } catch (caught) {
    ui.notify(errorMessage(caught), 'danger')
  }
}

async function confirm(): Promise<void> {
  if (!dialog.code.trim()) return
  dialog.saving = true
  dialog.error = null
  try {
    if (dialog.mode === 'setup') {
      const result = await securityApi.enableTwoFactor(dialog.code.trim())
      dialog.secret = ''
      dialog.uri = ''
      showCodes(result.codes)
      ui.notify('Dvostepena prijava je uključena.')
    } else if (dialog.mode === 'disable') {
      const done = await reauth.runWithReauth(() => securityApi.disableTwoFactor(dialog.code.trim()), 'Za isključivanje potvrdite lozinku.')
      if (done === null) return
      reset(null)
      ui.notify('Dvostepena prijava je isključena.', 'info')
    } else if (dialog.mode === 'regenerate') {
      const result = await reauth.runWithReauth(() => securityApi.regenerateRecoveryCodes(dialog.code.trim()), 'Za nove rezervne kodove potvrdite lozinku.')
      if (!result) return
      showCodes(result.codes)
    }
    await load()
  } catch (caught) {
    dialog.error = errorMessage(caught)
    dialog.code = ''
  } finally {
    dialog.saving = false
  }
}

function showCodes(codes: string[]): void {
  reset('codes')
  dialog.codes = codes
}

async function copyCodes(): Promise<void> {
  try {
    await navigator.clipboard.writeText(dialog.codes.join('\n'))
    ui.notify('Kodovi su kopirani.', 'info')
  } catch {
    ui.notify('Kopiranje nije uspelo. Prepišite kodove ručno.', 'danger')
  }
}

function downloadCodes(): void {
  const content = ['Bankarski Sistem: rezervni kodovi za dvostepenu prijavu', 'Svaki kod važi jednom.', '', ...dialog.codes].join('\n')
  const url = URL.createObjectURL(new Blob([content], { type: 'text/plain' }))
  const link = document.createElement('a')
  link.href = url
  link.download = 'rezervni-kodovi.txt'
  link.click()
  URL.revokeObjectURL(url)
}

onMounted(load)
</script>

<template>
  <AppCard title="Dvostepena prijava" subtitle="Pored lozinke, pri prijavi se traži i šestocifreni kod iz aplikacije na telefonu (Google Authenticator, Microsoft Authenticator, Authy).">
    <StatusViews v-if="isLoading" state="loading" :rows="1" />
    <StatusViews v-else-if="error" state="error" :message="error" @retry="load" />
    <div v-else-if="status" class="two-factor">
      <div class="state">
        <span class="state-icon" :class="{ on: status.enabled }"><AppIcon name="shield" /></span>
        <div>
          <AppBadge :tone="status.enabled ? 'success' : 'warning'">{{ status.enabled ? 'Uključena' : 'Isključena' }}</AppBadge>
          <p v-if="status.enabled" class="subtle">
            Od {{ formatDate(status.enabledAt) }} · preostalo rezervnih kodova: {{ status.recoveryCodesRemaining }}
          </p>
          <p v-else class="subtle">Preporučujemo da je uključite. Čak i ako neko sazna vašu lozinku, bez telefona ne može da se prijavi.</p>
        </div>
      </div>
      <div class="row actions">
        <AppButton v-if="!status.enabled" icon="shield" @click="startSetup">Uključi</AppButton>
        <template v-else>
          <AppButton variant="secondary" icon="refresh" @click="reset('regenerate')">Novi rezervni kodovi</AppButton>
          <AppButton variant="ghost" @click="reset('disable')">Isključi</AppButton>
        </template>
      </div>
      <p v-if="status.enabled && status.recoveryCodesRemaining <= 2" class="low-codes" role="status">
        <AppIcon name="alert" :size="16" /> Ostalo vam je malo rezervnih kodova. Napravite nove.
      </p>
    </div>
  </AppCard>

  <AppModal :open="dialog.mode !== null" :title="dialogTitle" width="520px" :dismissible="dialog.mode !== 'codes'" @close="reset(null)">
    <div v-if="dialog.mode === 'codes'" class="stack">
      <p class="muted">
        Ako izgubite telefon, sa ovim kodovima se i dalje možete prijaviti. Svaki kod važi jednom. Sačuvajte ih van telefona, na primer odštampane.
        Kodovi se više neće prikazati.
      </p>
      <ol class="codes mono">
        <li v-for="item in dialog.codes" :key="item">{{ item }}</li>
      </ol>
    </div>
    <form v-else id="two-factor-form" class="stack" @submit.prevent="confirm">
      <template v-if="dialog.mode === 'setup'">
        <ol class="setup-steps">
          <li>Otvorite aplikaciju za autentifikaciju i skenirajte QR kod.</li>
          <li>Ako ne možete da skenirate, unesite ključ ručno.</li>
          <li>Upišite kod koji aplikacija prikazuje.</li>
        </ol>
        <div class="qr">
          <img :src="qrDataUrl" alt="QR kod za aplikaciju za autentifikaciju" width="200" height="200" />
          <div>
            <p class="subtle">Ključ za ručni unos</p>
            <p class="secret mono">{{ groupedSecret }}</p>
          </div>
        </div>
      </template>
      <p v-else class="muted">
        {{ dialog.mode === 'disable' ? 'Unesite trenutni kod iz aplikacije ili jedan rezervni kod.' : 'Unesite trenutni kod iz aplikacije. Prethodni rezervni kodovi prestaju da važe.' }}
      </p>
      <AppField
        v-model="dialog.code"
        :label="dialog.mode === 'disable' ? 'Kod iz aplikacije ili rezervni kod' : 'Kod iz aplikacije'"
        inputmode="numeric"
        autocomplete="one-time-code"
        :maxlength="11"
        required
        :error="dialog.error"
      />
    </form>
    <template #footer>
      <template v-if="dialog.mode === 'codes'">
        <AppButton variant="secondary" icon="file" @click="copyCodes">Kopiraj</AppButton>
        <AppButton variant="secondary" icon="download" @click="downloadCodes">Preuzmi</AppButton>
        <AppButton @click="reset(null)">Sačuvao/la sam kodove</AppButton>
      </template>
      <template v-else>
        <AppButton variant="ghost" @click="reset(null)">Odustani</AppButton>
        <AppButton type="submit" form="two-factor-form" :variant="dialog.mode === 'disable' ? 'danger' : 'primary'" :loading="dialog.saving" :disabled="!dialog.code.trim()">
          {{ dialog.mode === 'setup' ? 'Uključi' : dialog.mode === 'disable' ? 'Isključi' : 'Napravi nove kodove' }}
        </AppButton>
      </template>
    </template>
  </AppModal>
</template>

<style scoped>
.two-factor {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-4);
  flex-wrap: wrap;
}

.state {
  display: flex;
  gap: var(--space-3);
  align-items: center;
  max-width: 560px;
}

.state-icon {
  display: grid;
  place-items: center;
  width: 46px;
  height: 46px;
  flex-shrink: 0;
  border-radius: 50%;
  background: var(--color-warning-soft);
  color: var(--color-warning);
}

.state-icon.on {
  background: var(--color-forest-900);
  color: var(--color-lime-400);
}

.actions {
  flex-wrap: wrap;
}

.low-codes {
  display: flex;
  gap: var(--space-2);
  align-items: center;
  width: 100%;
  font-size: var(--font-size-sm);
  color: var(--color-warning);
}

.setup-steps {
  margin: 0;
  padding-left: var(--space-5);
  font-size: var(--font-size-sm);
  color: var(--color-text-muted);
}

.qr {
  display: flex;
  align-items: center;
  gap: var(--space-4);
  flex-wrap: wrap;
  padding: var(--space-4);
  border-radius: var(--radius-md);
  background: var(--color-cream);
}

.qr img {
  border-radius: var(--radius-sm);
  background: #fff;
  image-rendering: pixelated;
}

.secret {
  font-size: var(--font-size-md);
  font-weight: 600;
  word-break: break-all;
  user-select: all;
}

.codes {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: var(--space-2) var(--space-6);
  margin: 0;
  padding: var(--space-4) var(--space-4) var(--space-4) var(--space-8);
  border-radius: var(--radius-md);
  background: var(--color-surface-muted);
  font-size: var(--font-size-md);
  font-weight: 600;
}
</style>
