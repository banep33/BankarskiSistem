<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { RouterLink } from 'vue-router'
import AppCard from '@/components/ui/AppCard.vue'
import AppIcon from '@/components/ui/AppIcon.vue'
import AppField from '@/components/ui/AppField.vue'
import StatusViews from '@/components/ui/StatusViews.vue'
import { helpApi } from '@/api/bank'
import { useAuthStore } from '@/stores/auth'
import type { FaqArticle, Fee, SupportContact } from '@/types/api'

const auth = useAuthStore()
const faq = ref<FaqArticle[]>([])
const fees = ref<Fee[]>([])
const contact = ref<SupportContact | null>(null)
const isLoading = ref(true)
const error = ref<string | null>(null)
const query = ref('')
const openId = ref<number | null>(null)

const filtered = computed(() => {
  const term = query.value.trim().toLowerCase()
  const matches = term ? faq.value.filter((item) => `${item.question} ${item.answer}`.toLowerCase().includes(term)) : faq.value
  return matches.reduce<Record<string, FaqArticle[]>>((groups, item) => {
    ;(groups[item.category] ??= []).push(item)
    return groups
  }, {})
})

async function load(): Promise<void> {
  isLoading.value = true
  error.value = null
  try {
    ;[faq.value, fees.value, contact.value] = await Promise.all([helpApi.faq(), helpApi.fees(), helpApi.contact()])
  } catch {
    error.value = 'Centar za pomoć trenutno nije dostupan.'
  } finally {
    isLoading.value = false
  }
}

onMounted(load)
</script>

<template>
  <div class="page">
    <header class="page-header">
      <div>
        <h1 class="page-title">Pomoć i cenovnik</h1>
        <p class="page-subtitle">Odgovori na česta pitanja, sve naknade na jednom mestu i kontakt sa bankom.</p>
      </div>
    </header>

    <StatusViews v-if="isLoading" state="loading" :rows="5" />
    <StatusViews v-else-if="error" state="error" :message="error" @retry="load" />

    <template v-else>
      <div class="help-grid">
        <AppCard title="Česta pitanja">
          <AppField v-model="query" label="Pretraga pitanja" type="search" placeholder="npr. naknada, prigovor, lozinka" />
          <div v-for="(items, category) in filtered" :key="category" class="faq-group">
            <h3 class="faq-category">{{ category }}</h3>
            <div v-for="item in items" :key="item.id" class="faq-item">
              <button type="button" class="faq-question" :aria-expanded="openId === item.id" @click="openId = openId === item.id ? null : item.id">
                <span>{{ item.question }}</span>
                <AppIcon :name="openId === item.id ? 'x' : 'plus'" :size="18" />
              </button>
              <p v-if="openId === item.id" class="faq-answer">{{ item.answer }}</p>
            </div>
          </div>
          <StatusViews v-if="Object.keys(filtered).length === 0" state="empty" title="Nema rezultata" message="Pokušajte drugi pojam ili nas kontaktirajte." />
        </AppCard>

        <div class="stack">
          <AppCard v-if="contact" title="Kontakt" tone="dark">
            <ul class="contact">
              <li><span class="subtle-inverse">Telefon</span><strong>{{ contact.phone }}</strong></li>
              <li><span class="subtle-inverse">Email</span><strong>{{ contact.email }}</strong></li>
              <li><span class="subtle-inverse">Radno vreme</span><span>{{ contact.workingHours }}</span></li>
            </ul>
            <RouterLink v-if="auth.isCustomer" to="/prigovori" class="contact-cta">Podnesite prigovor</RouterLink>
          </AppCard>
          <AppCard v-if="contact" tone="cream">
            <div class="row" style="align-items: flex-start">
              <AppIcon name="bank" />
              <p class="regulator">{{ contact.regulatorInfo }}</p>
            </div>
          </AppCard>
        </div>
      </div>

      <AppCard title="Cenovnik usluga" subtitle="Naknada se uvek prikazuje pre potvrde naloga, zajedno sa ukupnim iznosom koji se skida sa računa.">
        <div class="table-scroll">
          <table class="data-table">
            <thead>
              <tr>
                <th scope="col">Usluga</th>
                <th scope="col">Opis</th>
                <th scope="col">Naknada</th>
                <th scope="col">Važi od</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="fee in fees" :key="fee.code">
                <td><strong>{{ fee.name }}</strong></td>
                <td class="muted">{{ fee.description }}</td>
                <td><span class="fee" :class="{ free: fee.amount === 0 }">{{ fee.humanReadable }}</span></td>
                <td class="subtle">{{ fee.validFrom }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </AppCard>
    </template>
  </div>
</template>

<style scoped>
.help-grid {
  display: grid;
  grid-template-columns: minmax(0, 1.6fr) minmax(0, 1fr);
  gap: var(--space-5);
  align-items: start;
}

.faq-group {
  margin-top: var(--space-5);
}

.faq-category {
  font-size: var(--font-size-sm);
  text-transform: uppercase;
  letter-spacing: 0.06em;
  color: var(--color-primary);
  margin-bottom: var(--space-2);
}

.faq-item {
  border-bottom: 1px solid var(--color-border);
}

.faq-question {
  display: flex;
  width: 100%;
  justify-content: space-between;
  align-items: center;
  gap: var(--space-3);
  padding: var(--space-4) 0;
  border: none;
  background: transparent;
  text-align: left;
  font-weight: 600;
  cursor: pointer;
}

.faq-answer {
  padding-bottom: var(--space-4);
  color: var(--color-text-muted);
}

.contact {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: var(--space-3);
}

.contact li {
  display: flex;
  flex-direction: column;
}

.subtle-inverse {
  font-size: var(--font-size-xs);
  color: rgb(255 255 255 / 0.6);
  text-transform: uppercase;
  letter-spacing: 0.06em;
}

.contact-cta {
  display: inline-block;
  margin-top: var(--space-5);
  padding: 0.55rem 1.1rem;
  border-radius: var(--radius-pill);
  background: var(--color-lime-400);
  color: var(--color-forest-900);
  font-weight: 600;
  text-decoration: none;
}

.regulator {
  font-size: var(--font-size-sm);
}

.fee {
  font-weight: 600;
}

.fee.free {
  color: var(--color-success);
}

@media (max-width: 960px) {
  .help-grid {
    grid-template-columns: minmax(0, 1fr);
  }
}
</style>
