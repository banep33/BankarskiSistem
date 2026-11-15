<script setup lang="ts">
import { ref } from 'vue'
import AppCard from '@/components/ui/AppCard.vue'
import AppButton from '@/components/ui/AppButton.vue'
import AppField from '@/components/ui/AppField.vue'
import { feedbackApi } from '@/api/bank'
import { useUiStore } from '@/stores/ui'
import { errorMessage } from '@/composables/useAsync'

const props = defineProps<{ context: string }>()

const ui = useUiStore()
const storageKey = `bs.feedback.${props.context}`
const score = ref<number | null>(null)
const comment = ref('')
const isSubmitting = ref(false)
const isDone = ref(readDone())

function readDone(): boolean {
  try {
    return localStorage.getItem(storageKey) === new Date().toISOString().slice(0, 7)
  } catch {
    return false
  }
}

async function submit(): Promise<void> {
  if (score.value === null) return
  isSubmitting.value = true
  try {
    await feedbackApi.submit('CSAT', score.value, comment.value.trim() || null, props.context)
    try {
      localStorage.setItem(storageKey, new Date().toISOString().slice(0, 7))
    } catch {
      isDone.value = true
    }
    isDone.value = true
    ui.notify('Hvala na oceni. Pomaže nam da budemo bolji.')
  } catch (error) {
    ui.notify(errorMessage(error), 'danger')
  } finally {
    isSubmitting.value = false
  }
}
</script>

<template>
  <AppCard v-if="!isDone" tone="cream" title="Kako vam se dopada aplikacija?" subtitle="Ocena je anonimna u izveštajima i nije obavezna.">
    <div class="scores" role="radiogroup" aria-label="Ocena od 1 do 5">
      <button
        v-for="value in 5"
        :key="value"
        type="button"
        role="radio"
        class="score"
        :class="{ active: score === value }"
        :aria-checked="score === value"
        :aria-label="`Ocena ${value}`"
        @click="score = value"
      >
        {{ value }}
      </button>
    </div>
    <template v-if="score !== null">
      <AppField v-model="comment" class="comment" label="Komentar (nije obavezno)" multiline :rows="2" :maxlength="1000" />
      <AppButton class="submit" size="sm" variant="dark" :loading="isSubmitting" @click="submit">Pošalji ocenu</AppButton>
    </template>
  </AppCard>
</template>

<style scoped>
.scores {
  display: flex;
  gap: var(--space-2);
}

.score {
  flex: 1;
  height: 44px;
  border: 1px solid rgb(11 35 32 / 0.15);
  border-radius: var(--radius-md);
  background: var(--color-surface);
  font-weight: 700;
  cursor: pointer;
}

.score.active {
  background: var(--color-contrast-bg);
  color: var(--color-contrast-text);
  border-color: var(--color-contrast-bg);
}

.comment {
  margin-top: var(--space-4);
}

.submit {
  margin-top: var(--space-3);
}
</style>
