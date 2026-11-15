<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import AppModal from '@/components/ui/AppModal.vue'
import AppButton from '@/components/ui/AppButton.vue'
import { privacyApi } from '@/api/bank'
import { useAuthStore } from '@/stores/auth'
import { useUiStore } from '@/stores/ui'
import { errorMessage } from '@/composables/useAsync'
import type { LegalDocument } from '@/types/api'

const auth = useAuthStore()
const ui = useUiStore()
const documents = ref<LegalDocument[]>([])
const accepted = ref<Record<number, boolean>>({})
const expanded = ref<number | null>(null)
const isSubmitting = ref(false)

const isOpen = computed(() => auth.isAuthenticated && auth.pendingDocuments.length > 0)
const allAccepted = computed(() => documents.value.length > 0 && documents.value.every((document) => accepted.value[document.id]))

watch(
  isOpen,
  async (open) => {
    if (!open) return
    documents.value = await privacyApi.pendingDocuments()
    accepted.value = {}
    expanded.value = documents.value[0]?.id ?? null
  },
  { immediate: true },
)

async function acceptAll(): Promise<void> {
  isSubmitting.value = true
  try {
    await privacyApi.acceptDocuments(documents.value.map((document) => document.id))
    auth.pendingDocuments = []
    ui.notify('Hvala. Vaš pristanak je evidentiran.')
  } catch (error) {
    ui.notify(errorMessage(error), 'danger')
  } finally {
    isSubmitting.value = false
  }
}
</script>

<template>
  <AppModal :open="isOpen" title="Pre nastavka" width="680px" :dismissible="false">
    <div class="stack">
      <p class="muted">
        Da biste koristili elektronsko bankarstvo, pročitajte i prihvatite važeće dokumente. Pristanak se evidentira sa
        datumom i verzijom dokumenta, a kopiju uvek možete pogledati u odeljku Privatnost.
      </p>
      <article v-for="document in documents" :key="document.id" class="document">
        <button type="button" class="document-toggle" :aria-expanded="expanded === document.id" @click="expanded = expanded === document.id ? null : document.id">
          <span>{{ document.title }}</span>
          <span class="subtle">verzija {{ document.version }}</span>
        </button>
        <div v-if="expanded === document.id" class="document-content" tabindex="0">
          <p v-for="(paragraph, index) in document.content.split('\n').filter(Boolean)" :key="index">{{ paragraph }}</p>
        </div>
        <label class="consent">
          <input v-model="accepted[document.id]" type="checkbox" />
          <span>Pročitao/la sam i prihvatam dokument „{{ document.title }}“.</span>
        </label>
      </article>
    </div>
    <template #footer>
      <AppButton variant="ghost" @click="auth.logout()">Odjavi se</AppButton>
      <AppButton :disabled="!allAccepted" :loading="isSubmitting" @click="acceptAll">Prihvatam i nastavljam</AppButton>
    </template>
  </AppModal>
</template>

<style scoped>
.document {
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  overflow: hidden;
}

.document-toggle {
  display: flex;
  width: 100%;
  justify-content: space-between;
  align-items: center;
  gap: var(--space-3);
  padding: var(--space-4);
  border: none;
  background: var(--color-surface-muted);
  font-weight: 600;
  cursor: pointer;
  text-align: left;
}

.document-content {
  max-height: 260px;
  overflow-y: auto;
  padding: var(--space-4);
  display: flex;
  flex-direction: column;
  gap: var(--space-2);
  font-size: var(--font-size-sm);
  color: var(--color-text-muted);
}

.consent {
  display: flex;
  align-items: flex-start;
  gap: var(--space-3);
  padding: var(--space-4);
  border-top: 1px solid var(--color-border);
  font-size: var(--font-size-sm);
  cursor: pointer;
}

.consent input {
  margin-top: 3px;
  accent-color: var(--color-primary);
  width: 18px;
  height: 18px;
}
</style>
