<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute } from 'vue-router'
import AppCard from '@/components/ui/AppCard.vue'
import AppBadge from '@/components/ui/AppBadge.vue'
import AppButton from '@/components/ui/AppButton.vue'
import AppField from '@/components/ui/AppField.vue'
import StatusViews from '@/components/ui/StatusViews.vue'
import ComplaintThread from '@/components/banking/ComplaintThread.vue'
import FeedbackPrompt from '@/components/banking/FeedbackPrompt.vue'
import { complaintsApi } from '@/api/bank'
import { useUiStore } from '@/stores/ui'
import { errorMessage } from '@/composables/useAsync'
import { complaintCategoryLabels, complaintStatusLabels } from '@/utils/labels'
import { formatDate } from '@/utils/format'
import type { ComplaintDetail } from '@/types/api'

const route = useRoute()
const ui = useUiStore()
const complaint = ref<ComplaintDetail | null>(null)
const isLoading = ref(true)
const error = ref<string | null>(null)
const reply = ref('')
const isSending = ref(false)

const isClosed = computed(() => ['Resen', 'Odbijen', 'Zatvoren'].includes(complaint.value?.summary.status ?? ''))

async function load(): Promise<void> {
  isLoading.value = true
  error.value = null
  try {
    complaint.value = await complaintsApi.mineDetail(Number(route.params.id))
  } catch (caught) {
    error.value = errorMessage(caught)
  } finally {
    isLoading.value = false
  }
}

async function send(): Promise<void> {
  if (!complaint.value || reply.value.trim().length < 2) return
  isSending.value = true
  try {
    complaint.value = await complaintsApi.reply(complaint.value.summary.id, reply.value.trim())
    reply.value = ''
    ui.notify('Poruka je poslata.')
  } catch (caught) {
    ui.notify(errorMessage(caught), 'danger')
  } finally {
    isSending.value = false
  }
}

onMounted(load)
</script>

<template>
  <div class="page">
    <StatusViews v-if="isLoading" state="loading" :rows="5" />
    <StatusViews v-else-if="error" state="error" :message="error" @retry="load" />

    <template v-else-if="complaint">
      <header class="page-header">
        <div>
          <RouterLink to="/prigovori" class="back">← Svi prigovori</RouterLink>
          <h1 class="page-title">{{ complaint.summary.subject }}</h1>
          <p class="page-subtitle">
            <span class="mono">{{ complaint.summary.referenceNumber }}</span> · {{ complaintCategoryLabels[complaint.summary.category] }}
          </p>
        </div>
        <AppBadge :tone="complaintStatusLabels[complaint.summary.status].tone">{{ complaintStatusLabels[complaint.summary.status].label }}</AppBadge>
      </header>

      <div class="layout">
        <AppCard title="Prepiska">
          <ComplaintThread :complaint="complaint" />
          <form v-if="!isClosed" class="reply" @submit.prevent="send">
            <AppField v-model="reply" label="Vaša poruka" multiline :rows="3" :maxlength="5000" />
            <AppButton type="submit" icon="send" :loading="isSending" :disabled="reply.trim().length < 2">Pošalji</AppButton>
          </form>
        </AppCard>

        <aside class="stack">
          <AppCard title="Rokovi">
            <dl class="facts">
              <div><dt>Podnet</dt><dd>{{ formatDate(complaint.summary.createdAt) }}</dd></div>
              <div><dt>Rok za odgovor</dt><dd :class="{ overdue: complaint.summary.isOverdue }">{{ formatDate(complaint.summary.dueAt) }}</dd></div>
              <div v-if="complaint.summary.deadlineExtended"><dt>Produženje</dt><dd>{{ complaint.deadlineExtensionReason }}</dd></div>
            </dl>
          </AppCard>
          <FeedbackPrompt v-if="isClosed" :context="`prigovor-${complaint.summary.id}`" />
        </aside>
      </div>
    </template>
  </div>
</template>

<style scoped>
.back {
  font-size: var(--font-size-sm);
  color: var(--color-text-muted);
  text-decoration: none;
}

.layout {
  display: grid;
  grid-template-columns: minmax(0, 1.8fr) minmax(0, 1fr);
  gap: var(--space-5);
  align-items: start;
}

.reply {
  display: flex;
  flex-direction: column;
  align-items: flex-end;
  gap: var(--space-3);
  margin-top: var(--space-5);
  padding-top: var(--space-5);
  border-top: 1px solid var(--color-border);
}

.reply > :first-child {
  width: 100%;
}

.facts {
  margin: 0;
}

.facts div {
  display: flex;
  justify-content: space-between;
  gap: var(--space-3);
  padding: var(--space-2) 0;
  border-bottom: 1px solid var(--color-border);
}

.facts dt {
  color: var(--color-text-muted);
  font-size: var(--font-size-sm);
}

.facts dd {
  margin: 0;
  font-weight: 600;
  text-align: right;
}

.overdue {
  color: var(--color-danger);
}

@media (max-width: 960px) {
  .layout {
    grid-template-columns: minmax(0, 1fr);
  }
}
</style>
