<script setup lang="ts">
import { computed, ref } from 'vue'
import { RouterLink, useRoute, useRouter } from 'vue-router'
import AppField from '@/components/ui/AppField.vue'
import AppButton from '@/components/ui/AppButton.vue'
import AppIcon from '@/components/ui/AppIcon.vue'
import { useAuthStore } from '@/stores/auth'
import { errorMessage } from '@/composables/useAsync'
import { ApiError } from '@/api/http'

const auth = useAuthStore()
const route = useRoute()
const router = useRouter()

const email = ref('')
const password = ref('')
const error = ref<string | null>(null)
const isSubmitting = ref(false)
const challengeToken = ref<string | null>(null)
const code = ref('')
const useRecoveryCode = ref(false)

const reasonMessage = computed(() => {
  if (route.query.razlog === 'neaktivnost') return 'Odjavljeni ste zbog neaktivnosti. Prijavite se ponovo.'
  if (route.query.razlog === 'sesija') return 'Vaša sesija više nije važeća. Prijavite se ponovo.'
  return null
})

async function goToTarget(): Promise<void> {
  const target = typeof route.query.povratak === 'string' && route.query.povratak.startsWith('/') ? route.query.povratak : auth.homeRoute
  await router.replace(target)
}

async function submit(): Promise<void> {
  error.value = null
  isSubmitting.value = true
  try {
    const step = await auth.login(email.value.trim(), password.value)
    password.value = ''
    if (step.kind === 'two-factor') {
      challengeToken.value = step.challengeToken
      code.value = ''
      return
    }
    await goToTarget()
  } catch (caught) {
    error.value = errorMessage(caught)
    password.value = ''
  } finally {
    isSubmitting.value = false
  }
}

async function submitCode(): Promise<void> {
  if (!challengeToken.value) return
  error.value = null
  isSubmitting.value = true
  try {
    await auth.completeTwoFactor(challengeToken.value, code.value.trim())
    challengeToken.value = null
    await goToTarget()
  } catch (caught) {
    error.value = errorMessage(caught)
    code.value = ''
    if (caught instanceof ApiError && (caught.message.includes('zaključan') || caught.message.includes('deaktiviran'))) challengeToken.value = null
  } finally {
    isSubmitting.value = false
  }
}

function backToPassword(): void {
  challengeToken.value = null
  code.value = ''
  error.value = null
}
</script>

<template>
  <div class="login">
    <section class="hero" aria-hidden="true">
      <div class="hero-top">
        <span class="badge">● ELEKTRONSKO BANKARSTVO</span>
      </div>
      <div class="hero-copy">
        <h1>Bankarstvo,<br />po vašoj meri.</h1>
        <p>Plaćanja, računi i uvid u troškove na jednom mestu, uz jasne naknade i zaštitu koju možete da proverite.</p>
      </div>
      <div class="hero-cards">
        <div class="glass-card">
          <span class="glass-label">Plaćanje u banci</span>
          <strong>Besplatno</strong>
          <span class="glass-sub">izvršava se odmah</span>
        </div>
        <div class="glass-card dark">
          <span class="glass-label">Vaša sesija</span>
          <strong>Zaštićena</strong>
          <span class="glass-sub">odjava posle neaktivnosti</span>
        </div>
      </div>
      <div class="orb" />
    </section>

    <section class="form-side">
      <div class="form-wrapper">
        <div class="mark">
          <svg viewBox="0 0 32 32" width="44" height="44" aria-hidden="true">
            <rect width="32" height="32" rx="9" fill="#0B2320" />
            <path d="M9 21.5 14.2 10.5h3.3L12.3 21.5zM15.6 21.5l5.2-11h2.8l-5.2 11z" fill="#F05A3C" />
            <circle cx="23.5" cy="21" r="2" fill="#C8F169" />
          </svg>
        </div>
        <h2>{{ challengeToken ? 'Potvrda prijave' : 'Prijava' }}</h2>
        <p class="muted">
          {{
            challengeToken
              ? useRecoveryCode
                ? 'Unesite jedan od rezervnih kodova koje ste sačuvali pri uključivanju dvostepene prijave.'
                : 'Unesite šestocifreni kod iz aplikacije za autentifikaciju na telefonu.'
              : 'Dobro došli nazad. Unesite podatke za pristup.'
          }}
        </p>

        <div v-if="reasonMessage" class="info-banner" role="status">
          <AppIcon name="info" :size="18" />
          <span>{{ reasonMessage }}</span>
        </div>

        <form v-if="challengeToken" class="stack form" novalidate @submit.prevent="submitCode">
          <AppField
            v-model="code"
            :label="useRecoveryCode ? 'Rezervni kod' : 'Kod iz aplikacije'"
            :placeholder="useRecoveryCode ? 'XXXXX-XXXXX' : '000000'"
            :inputmode="useRecoveryCode ? 'text' : 'numeric'"
            autocomplete="one-time-code"
            :maxlength="useRecoveryCode ? 11 : 7"
            required
          />
          <div v-if="error" class="error-banner" role="alert">
            <AppIcon name="alert" :size="18" />
            <span>{{ error }}</span>
          </div>
          <AppButton type="submit" size="lg" block :loading="isSubmitting" :disabled="!code.trim()">Potvrdi</AppButton>
          <div class="row-between code-links">
            <button type="button" class="text-button" @click="useRecoveryCode = !useRecoveryCode; code = ''">
              {{ useRecoveryCode ? 'Koristi kod iz aplikacije' : 'Nemam telefon, koristiću rezervni kod' }}
            </button>
            <button type="button" class="text-button" @click="backToPassword">Nazad</button>
          </div>
        </form>

        <form v-else class="stack form" novalidate @submit.prevent="submit">
          <AppField v-model="email" label="Email adresa" type="email" autocomplete="username" required inputmode="email" />
          <AppField v-model="password" label="Lozinka" type="password" autocomplete="current-password" required />
          <RouterLink to="/javno/zaboravljena-lozinka" class="forgot">Zaboravili ste lozinku?</RouterLink>
          <div v-if="error" class="error-banner" role="alert">
            <AppIcon name="alert" :size="18" />
            <span>{{ error }}</span>
          </div>
          <AppButton type="submit" size="lg" block :loading="isSubmitting" :disabled="!email || !password">Prijavi se</AppButton>
        </form>

        <ul class="safety">
          <li><AppIcon name="shield" :size="16" /> Banka nikada ne traži lozinku telefonom, porukom ili emailom.</li>
          <li><AppIcon name="lock" :size="16" /> Posle 5 pogrešnih pokušaja nalog se privremeno zaključava.</li>
        </ul>

        <nav class="links">
          <RouterLink to="/javno/pomoc">Pomoć i cenovnik</RouterLink>
          <RouterLink to="/javno/dokumenti/privatnost">Politika privatnosti</RouterLink>
          <RouterLink to="/javno/dokumenti/uslovi">Uslovi korišćenja</RouterLink>
        </nav>
      </div>
    </section>
  </div>
</template>

<style scoped>
.login {
  display: grid;
  grid-template-columns: 1.1fr 1fr;
  min-height: 100vh;
  padding: var(--space-4);
  gap: var(--space-4);
}

.hero {
  position: relative;
  overflow: hidden;
  display: flex;
  flex-direction: column;
  justify-content: space-between;
  padding: var(--space-10);
  border-radius: var(--radius-xl);
  color: #fff;
  background:
    radial-gradient(circle at 85% 90%, #ff8a5b 0%, transparent 45%),
    radial-gradient(circle at 10% 0%, #ffd7c7 0%, transparent 40%),
    linear-gradient(150deg, #f7b9a3 0%, var(--color-coral-500) 48%, var(--color-coral-700) 100%);
}

.badge {
  display: inline-block;
  padding: 0.35rem 0.85rem;
  border-radius: var(--radius-pill);
  background: rgb(255 255 255 / 0.18);
  font-size: var(--font-size-xs);
  font-weight: 600;
  letter-spacing: 0.08em;
}

.hero-copy h1 {
  font-size: clamp(2.4rem, 4.2vw, 3.6rem);
  font-weight: 600;
  letter-spacing: -0.04em;
  line-height: 1.02;
}

.hero-copy p {
  max-width: 440px;
  margin-top: var(--space-4);
  font-size: var(--font-size-lg);
  color: rgb(255 255 255 / 0.88);
}

.hero-cards {
  position: relative;
  z-index: 1;
  display: flex;
  gap: var(--space-4);
  flex-wrap: wrap;
}

.glass-card {
  display: flex;
  flex-direction: column;
  gap: 2px;
  min-width: 190px;
  padding: var(--space-5);
  border: 1px solid rgb(255 255 255 / 0.35);
  border-radius: var(--radius-lg);
  background: rgb(255 255 255 / 0.16);
  backdrop-filter: blur(14px);
}

.glass-card.dark {
  background: rgb(11 35 32 / 0.55);
  border-color: rgb(200 241 105 / 0.35);
}

.glass-card.dark strong {
  color: var(--color-lime-400);
}

.glass-label {
  font-size: var(--font-size-xs);
  text-transform: uppercase;
  letter-spacing: 0.08em;
  opacity: 0.8;
}

.glass-card strong {
  font-size: var(--font-size-2xl);
  font-weight: 600;
}

.glass-sub {
  font-size: var(--font-size-sm);
  opacity: 0.8;
}

.orb {
  position: absolute;
  right: -120px;
  top: -80px;
  width: 420px;
  height: 420px;
  border-radius: 50%;
  border: 1px solid rgb(255 255 255 / 0.35);
  background: radial-gradient(circle at 30% 30%, rgb(255 255 255 / 0.35), transparent 60%);
}

.form-side {
  display: grid;
  place-items: center;
  padding: var(--space-6);
}

.form-wrapper {
  width: 100%;
  max-width: 400px;
}

.mark {
  margin-bottom: var(--space-6);
}

.form-wrapper h2 {
  font-size: var(--font-size-3xl);
  letter-spacing: -0.03em;
}

.form {
  margin-top: var(--space-6);
}

.info-banner,
.error-banner {
  display: flex;
  gap: var(--space-2);
  align-items: flex-start;
  margin-top: var(--space-4);
  padding: var(--space-3) var(--space-4);
  border-radius: var(--radius-md);
  font-size: var(--font-size-sm);
}

.info-banner {
  background: var(--color-info-soft);
  color: var(--color-info);
}

.error-banner {
  margin-top: 0;
  background: var(--color-danger-soft);
  color: var(--color-danger);
}

.code-links {
  flex-wrap: wrap;
  gap: var(--space-2);
}

.text-button {
  border: none;
  background: none;
  padding: 0;
  font-size: var(--font-size-sm);
  font-weight: 600;
  color: var(--color-primary-hover);
  cursor: pointer;
}

.forgot {
  align-self: flex-end;
  margin-top: calc(var(--space-2) * -1);
  font-size: var(--font-size-sm);
  font-weight: 600;
  color: var(--color-primary-hover);
}

.safety {
  list-style: none;
  margin: var(--space-6) 0 0;
  padding: var(--space-4);
  border-radius: var(--radius-md);
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  display: flex;
  flex-direction: column;
  gap: var(--space-2);
  font-size: var(--font-size-sm);
  color: var(--color-text-muted);
}

.safety li {
  display: flex;
  gap: var(--space-2);
  align-items: flex-start;
}

.safety :deep(svg) {
  color: var(--color-success);
  margin-top: 3px;
}

.links {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-4);
  margin-top: var(--space-6);
  font-size: var(--font-size-sm);
}

.links a {
  color: var(--color-text-muted);
}

@media (max-width: 960px) {
  .login {
    grid-template-columns: minmax(0, 1fr);
  }

  .hero {
    min-height: 280px;
    padding: var(--space-6);
  }

  .hero-cards {
    display: none;
  }
}
</style>
