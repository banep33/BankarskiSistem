<script setup lang="ts">
import { computed, reactive, ref } from 'vue'
import { RouterLink } from 'vue-router'
import AppCard from '@/components/ui/AppCard.vue'
import AppField from '@/components/ui/AppField.vue'
import AppButton from '@/components/ui/AppButton.vue'
import AppIcon from '@/components/ui/AppIcon.vue'
import { authApi } from '@/api/bank'
import { errorMessage } from '@/composables/useAsync'
import { passwordIssues } from '@/utils/validation'

const form = reactive({ email: '', password: '', confirm: '' })
const error = ref<string | null>(null)
const isSubmitting = ref(false)
const confirmation = ref<string | null>(null)

const issues = computed(() => (form.password ? passwordIssues(form.password) : []))
const strength = computed(() => (form.password ? Math.max(0, 4 - issues.value.length) : 0))

async function submit(): Promise<void> {
  error.value = null
  if (!form.email.trim()) {
    error.value = 'Unesite email adresu naloga.'
    return
  }
  if (issues.value.length) {
    error.value = `Nova lozinka mora sadržati: ${issues.value.join(', ')}.`
    return
  }
  if (form.password !== form.confirm) {
    error.value = 'Lozinka i potvrda se ne poklapaju.'
    return
  }
  isSubmitting.value = true
  try {
    const response = await authApi.requestPasswordReset(form.email.trim(), form.password, form.confirm)
    confirmation.value = response.message
    Object.assign(form, { password: '', confirm: '' })
  } catch (caught) {
    error.value = errorMessage(caught)
  } finally {
    isSubmitting.value = false
  }
}
</script>

<template>
  <div class="page forgot">
    <header>
      <h1 class="page-title">Zaboravljena lozinka</h1>
      <p class="page-subtitle">Predložite novu lozinku. Ona počinje da važi tek kada banka potvrdi vaš identitet i odobri zahtev.</p>
    </header>

    <div class="layout">
      <AppCard v-if="confirmation" tone="cream">
        <div class="done">
          <span class="done-icon"><AppIcon name="check" :size="28" /></span>
          <h2>Zahtev je poslat</h2>
          <p>{{ confirmation }}</p>
          <p class="muted">
            Do odobrenja važi vaša stara lozinka. Banka vas može kontaktirati telefonom radi provere identiteta, ali nikada neće tražiti
            da joj saopštite lozinku.
          </p>
          <RouterLink to="/prijava" class="back-link">Nazad na prijavu</RouterLink>
        </div>
      </AppCard>

      <AppCard v-else title="Nova lozinka">
        <form class="stack" novalidate @submit.prevent="submit">
          <AppField v-model="form.email" label="Email adresa naloga" type="email" autocomplete="username" inputmode="email" required />
          <AppField v-model="form.password" label="Nova lozinka" type="password" autocomplete="new-password" required />
          <div v-if="form.password" class="strength" :data-level="strength" aria-hidden="true">
            <span v-for="index in 4" :key="index" :class="{ filled: index <= strength }" />
          </div>
          <p v-if="form.password && issues.length" class="subtle">Nedostaje: {{ issues.join(', ') }}</p>
          <AppField v-model="form.confirm" label="Potvrda nove lozinke" type="password" autocomplete="new-password" required />
          <div v-if="error" class="error-banner" role="alert">
            <AppIcon name="alert" :size="18" />
            <span>{{ error }}</span>
          </div>
          <AppButton type="submit" size="lg" block :loading="isSubmitting">Pošalji zahtev</AppButton>
          <RouterLink to="/prijava" class="back-link">Setio/la sam se lozinke</RouterLink>
        </form>
      </AppCard>

      <AppCard tone="dark" title="Kako teče postupak">
        <ol class="steps">
          <li><strong>Šaljete zahtev</strong> sa novom lozinkom. Lozinka se odmah šifruje i niko je ne može pročitati.</li>
          <li><strong>Banka proverava identitet</strong>, na primer povratnim pozivom na broj iz evidencije ili u ekspozituri.</li>
          <li><strong>Administrator odobrava</strong> zahtev i nova lozinka postaje aktivna, a sve ranije sesije se odjavljuju.</li>
        </ol>
        <p class="note">Zahtev važi 72 sata. Ako pošaljete novi zahtev, prethodni se poništava. Ako niste vi poslali zahtev, obavestiće vas
          bezbednosno obaveštenje u aplikaciji.</p>
      </AppCard>
    </div>
  </div>
</template>

<style scoped>
.forgot {
  max-width: 920px;
}

.layout {
  display: grid;
  grid-template-columns: minmax(0, 1.2fr) minmax(0, 1fr);
  gap: var(--space-5);
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

.error-banner {
  display: flex;
  gap: var(--space-2);
  padding: var(--space-3) var(--space-4);
  border-radius: var(--radius-md);
  background: var(--color-danger-soft);
  color: var(--color-danger);
  font-size: var(--font-size-sm);
}

.back-link {
  align-self: center;
  font-size: var(--font-size-sm);
  font-weight: 600;
  color: var(--color-primary-hover);
}

.steps {
  margin: 0;
  padding-left: var(--space-5);
  display: flex;
  flex-direction: column;
  gap: var(--space-3);
  font-size: var(--font-size-sm);
  color: rgb(255 255 255 / 0.85);
}

.steps strong {
  color: var(--color-lime-400);
}

.note {
  margin-top: var(--space-4);
  font-size: var(--font-size-xs);
  color: rgb(255 255 255 / 0.6);
}

.done {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: var(--space-3);
  text-align: center;
}

.done-icon {
  display: grid;
  place-items: center;
  width: 60px;
  height: 60px;
  border-radius: 50%;
  background: var(--color-lime-400);
  color: var(--color-forest-900);
}

@media (max-width: 860px) {
  .layout {
    grid-template-columns: minmax(0, 1fr);
  }
}
</style>
