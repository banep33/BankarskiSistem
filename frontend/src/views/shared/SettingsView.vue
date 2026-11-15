<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import AppCard from '@/components/ui/AppCard.vue'
import AppButton from '@/components/ui/AppButton.vue'
import AppField from '@/components/ui/AppField.vue'
import AppBadge from '@/components/ui/AppBadge.vue'
import StatusViews from '@/components/ui/StatusViews.vue'
import { meApi } from '@/api/bank'
import { useAuthStore } from '@/stores/auth'
import { useUiStore } from '@/stores/ui'
import { errorMessage } from '@/composables/useAsync'
import { kycLabels, riskLabels, roleLabels } from '@/utils/labels'
import { formatDate, initials } from '@/utils/format'
import { passwordIssues } from '@/utils/validation'
import type { User } from '@/types/api'

const auth = useAuthStore()
const ui = useUiStore()
const user = ref<User | null>(null)
const isLoading = ref(true)
const error = ref<string | null>(null)
const isSavingProfile = ref(false)
const isSavingPassword = ref(false)
const passwordError = ref<string | null>(null)

const profile = reactive({ firstName: '', lastName: '' })
const password = reactive({ current: '', next: '', confirm: '' })

const issues = computed(() => (password.next ? passwordIssues(password.next) : []))
const strength = computed(() => (password.next ? Math.max(0, 4 - issues.value.length) : 0))

async function load(): Promise<void> {
  isLoading.value = true
  error.value = null
  try {
    user.value = (await meApi.profile()).user
    profile.firstName = user.value.firstName
    profile.lastName = user.value.lastName
  } catch (caught) {
    error.value = errorMessage(caught)
  } finally {
    isLoading.value = false
  }
}

async function saveProfile(): Promise<void> {
  isSavingProfile.value = true
  try {
    user.value = await meApi.updateProfile(profile.firstName.trim(), profile.lastName.trim())
    auth.updateName(user.value.firstName, user.value.lastName)
    ui.notify('Lični podaci su sačuvani.')
  } catch (caught) {
    ui.notify(errorMessage(caught), 'danger')
  } finally {
    isSavingProfile.value = false
  }
}

async function changePassword(): Promise<void> {
  passwordError.value = null
  if (issues.value.length) {
    passwordError.value = `Nova lozinka mora sadržati: ${issues.value.join(', ')}.`
    return
  }
  if (password.next !== password.confirm) {
    passwordError.value = 'Nova lozinka i potvrda se ne poklapaju.'
    return
  }
  isSavingPassword.value = true
  try {
    const response = await meApi.changePassword(password.current, password.next, password.confirm)
    auth.applyLogin(response)
    Object.assign(password, { current: '', next: '', confirm: '' })
    ui.notify('Lozinka je promenjena. Odjavljeni ste sa svih drugih uređaja.')
  } catch (caught) {
    passwordError.value = errorMessage(caught)
  } finally {
    isSavingPassword.value = false
  }
}

onMounted(load)
</script>

<template>
  <div class="page">
    <header class="page-header">
      <div>
        <h1 class="page-title">Postavke</h1>
        <p class="page-subtitle">Lični podaci, lozinka i izgled aplikacije.</p>
      </div>
    </header>

    <StatusViews v-if="isLoading" state="loading" :rows="4" />
    <StatusViews v-else-if="error" state="error" :message="error" @retry="load" />

    <template v-else-if="user">
      <AppCard tone="dark">
        <div class="identity">
          <span class="avatar">{{ initials(user.firstName, user.lastName) }}</span>
          <div>
            <h2>{{ user.firstName }} {{ user.lastName }}</h2>
            <p class="email">{{ user.email }}</p>
            <div class="row badges">
              <AppBadge tone="brand">{{ roleLabels[user.role] }}</AppBadge>
              <AppBadge v-if="user.role === 'Korisnik'" :tone="kycLabels[user.kycStatus].tone">Identitet: {{ kycLabels[user.kycStatus].label }}</AppBadge>
              <AppBadge v-if="user.role === 'Korisnik'" :tone="riskLabels[user.riskRating].tone">{{ riskLabels[user.riskRating].label }}</AppBadge>
            </div>
            <p v-if="user.role === 'Korisnik'" class="since">Klijent od {{ formatDate(user.createdAt) }}</p>
          </div>
        </div>
      </AppCard>

      <div class="grid-2 align-start">
        <AppCard title="Lični podaci" subtitle="Email adresu menja banka nakon provere identiteta.">
          <form class="stack" @submit.prevent="saveProfile">
            <div class="grid-2">
              <AppField v-model="profile.firstName" label="Ime" required :maxlength="50" autocomplete="given-name" />
              <AppField v-model="profile.lastName" label="Prezime" required :maxlength="50" autocomplete="family-name" />
            </div>
            <AppField :model-value="user.email" label="Email" disabled />
            <AppButton type="submit" :loading="isSavingProfile">Sačuvaj</AppButton>
          </form>
        </AppCard>

        <AppCard title="Promena lozinke" subtitle="Posle promene, sve druge sesije se automatski odjavljuju.">
          <form class="stack" @submit.prevent="changePassword">
            <AppField v-model="password.current" label="Trenutna lozinka" type="password" autocomplete="current-password" required />
            <AppField v-model="password.next" label="Nova lozinka" type="password" autocomplete="new-password" required />
            <div v-if="password.next" class="strength" :data-level="strength" aria-hidden="true">
              <span v-for="index in 4" :key="index" :class="{ filled: index <= strength }" />
            </div>
            <p v-if="password.next && issues.length" class="subtle">Nedostaje: {{ issues.join(', ') }}</p>
            <AppField v-model="password.confirm" label="Potvrda nove lozinke" type="password" autocomplete="new-password" required />
            <p v-if="passwordError" class="form-error" role="alert">{{ passwordError }}</p>
            <AppButton type="submit" :loading="isSavingPassword" :disabled="!password.current || !password.next || !password.confirm">Promeni lozinku</AppButton>
          </form>
        </AppCard>
      </div>

      <AppCard title="Izgled" subtitle="Sistemska tema prati podešavanje uređaja.">
        <div class="segmented" role="group" aria-label="Tema aplikacije">
          <button v-for="option in [{ v: 'system', l: 'Sistemska' }, { v: 'light', l: 'Svetla' }, { v: 'dark', l: 'Tamna' }] as const" :key="option.v" type="button" :class="{ active: ui.theme === option.v }" :aria-pressed="ui.theme === option.v" @click="ui.theme = option.v">
            {{ option.l }}
          </button>
        </div>
      </AppCard>
    </template>
  </div>
</template>

<style scoped>
.identity {
  display: flex;
  align-items: center;
  gap: var(--space-5);
  flex-wrap: wrap;
}

.avatar {
  display: grid;
  place-items: center;
  width: 72px;
  height: 72px;
  border-radius: 50%;
  background: var(--color-lime-400);
  color: var(--color-forest-900);
  font-size: var(--font-size-2xl);
  font-weight: 700;
}

.identity > div {
  min-width: 0;
}

.identity h2 {
  font-size: var(--font-size-2xl);
  overflow-wrap: anywhere;
}

.email {
  color: var(--color-emphasis-text-muted);
  overflow-wrap: anywhere;
}

.badges {
  margin-top: var(--space-2);
  flex-wrap: wrap;
}

.since {
  margin-top: var(--space-2);
  color: var(--color-emphasis-text-muted);
  font-size: var(--font-size-sm);
}

.align-start {
  align-items: start;
}

.strength {
  display: flex;
  gap: 4px;
}

.strength span {
  flex: 1;
  height: 6px;
  border-radius: var(--radius-pill);
  background: var(--color-border);
}

.strength span.filled {
  background: var(--color-coral-500);
}

.strength[data-level='4'] span.filled {
  background: var(--color-success);
}

.form-error {
  color: var(--color-danger);
  font-size: var(--font-size-sm);
}

.segmented {
  display: inline-flex;
  flex-wrap: wrap;
  padding: 3px;
  border-radius: var(--radius-pill);
  background: var(--color-surface-muted);
}

.segmented button {
  padding: 0.35rem 0.9rem;
  border: none;
  border-radius: var(--radius-pill);
  background: transparent;
  font-size: var(--font-size-sm);
  font-weight: 600;
  color: var(--color-text-muted);
  cursor: pointer;
}

.segmented button.active {
  background: var(--color-surface);
  color: var(--color-text);
  box-shadow: var(--shadow-sm);
}

@media (max-width: 520px) {
  .segmented {
    width: 100%;
  }

  .segmented button {
    flex: 1;
  }
}
</style>
