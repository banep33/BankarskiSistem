<script setup lang="ts">
import { onMounted, ref, watch } from 'vue'
import AppCard from '@/components/ui/AppCard.vue'
import AppBadge from '@/components/ui/AppBadge.vue'
import AppPagination from '@/components/ui/AppPagination.vue'
import StatusViews from '@/components/ui/StatusViews.vue'
import { feedbackApi } from '@/api/bank'
import { errorMessage } from '@/composables/useAsync'
import { formatDateTime, formatNumber, gridColumnsClass } from '@/utils/format'
import type { FeedbackEntry, FeedbackSummary, PagedResponse, SurveyType } from '@/types/api'

const days = ref(90)
const type = ref<SurveyType | ''>('')
const page = ref(1)
const summary = ref<FeedbackSummary | null>(null)
const recent = ref<PagedResponse<FeedbackEntry> | null>(null)
const isLoading = ref(true)
const error = ref<string | null>(null)

const surveyInfo: Record<SurveyType, { title: string; scale: string }> = {
  CSAT: { title: 'Zadovoljstvo (CSAT)', scale: 'udeo ocena 4 i 5' },
  NPS: { title: 'Preporuka (NPS)', scale: 'promoteri minus kritičari' },
  CES: { title: 'Lakoća korišćenja (CES)', scale: 'prosek od 1 do 7, niže je bolje' },
}

async function load(): Promise<void> {
  isLoading.value = true
  error.value = null
  try {
    ;[summary.value, recent.value] = await Promise.all([feedbackApi.summary(days.value), feedbackApi.recent(type.value || undefined, page.value)])
  } catch (caught) {
    error.value = errorMessage(caught)
  } finally {
    isLoading.value = false
  }
}

watch([days, type], () => {
  page.value = 1
  load()
})
watch(page, load)
onMounted(load)
</script>

<template>
  <div class="page">
    <header class="page-header">
      <div>
        <h1 class="page-title">Zadovoljstvo korisnika</h1>
        <p class="page-subtitle">Ocene koje klijenti ostavljaju u aplikaciji, bez prikaza identiteta.</p>
      </div>
      <div class="segmented" role="group" aria-label="Period">
        <button v-for="option in [30, 90, 365]" :key="option" type="button" :class="{ active: days === option }" :aria-pressed="days === option" @click="days = option">
          {{ option === 365 ? 'Godina' : `${option} dana` }}
        </button>
      </div>
    </header>

    <StatusViews v-if="isLoading" state="loading" :rows="4" />
    <StatusViews v-else-if="error" state="error" :message="error" @retry="load" />

    <template v-else-if="summary">
      <div :class="gridColumnsClass(summary.scores.length)">
        <AppCard v-for="score in summary.scores" :key="score.surveyType" :tone="score.surveyType === 'CSAT' ? 'dark' : 'default'">
          <p class="metric-label">{{ surveyInfo[score.surveyType].title }}</p>
          <p class="metric">{{ score.score === null ? 'Nema podataka' : formatNumber(score.score, 1) }}<span v-if="score.score !== null && score.surveyType === 'CSAT'">%</span></p>
          <p class="metric-sub">{{ score.responses }} odgovora · {{ surveyInfo[score.surveyType].scale }}</p>
          <p class="interpretation">{{ score.interpretation }}</p>
        </AppCard>
      </div>

      <AppCard title="Poslednji odgovori">
        <template #actions>
          <select v-model="type" class="select" aria-label="Vrsta ankete">
            <option value="">Sve ankete</option>
            <option value="CSAT">CSAT</option>
            <option value="NPS">NPS</option>
            <option value="CES">CES</option>
          </select>
        </template>
        <StatusViews v-if="!recent?.items.length" state="empty" icon="star" title="Još nema odgovora" />
        <ul v-else class="feedback">
          <li v-for="entry in recent.items" :key="entry.id">
            <div class="row-between">
              <span class="row"><AppBadge tone="info">{{ entry.surveyType }}</AppBadge> <strong>Ocena {{ entry.score }}</strong></span>
              <span class="subtle">{{ formatDateTime(entry.createdAt) }} · {{ entry.context }}</span>
            </div>
            <p v-if="entry.comment" class="muted">{{ entry.comment }}</p>
          </li>
        </ul>
        <AppPagination v-if="recent" :page="recent.page" :total-pages="recent.totalPages" :total-count="recent.totalCount" @change="page = $event" />
      </AppCard>
    </template>
  </div>
</template>

<style scoped>
.segmented {
  display: inline-flex;
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

.metric-label {
  font-size: var(--font-size-sm);
  opacity: 0.75;
}

.metric {
  margin: var(--space-2) 0;
  font-size: var(--font-size-3xl);
  font-weight: 600;
  letter-spacing: -0.03em;
}

.metric-sub {
  font-size: var(--font-size-xs);
  opacity: 0.7;
}

.interpretation {
  margin-top: var(--space-3);
  font-size: var(--font-size-sm);
}

.select {
  padding: 0.45rem 0.8rem;
  border: 1px solid var(--color-input-border);
  border-radius: var(--radius-md);
  background: var(--color-surface);
}

.feedback {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
}

.feedback li {
  padding: var(--space-3) 0;
  border-bottom: 1px solid var(--color-border);
}
</style>
