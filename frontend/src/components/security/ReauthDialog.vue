<script setup lang="ts">
import { ref, watch } from 'vue'
import AppModal from '@/components/ui/AppModal.vue'
import AppField from '@/components/ui/AppField.vue'
import AppButton from '@/components/ui/AppButton.vue'
import AppIcon from '@/components/ui/AppIcon.vue'
import { useReauthStore } from '@/stores/reauth'
import { errorMessage } from '@/composables/useAsync'

const reauth = useReauthStore()
const password = ref('')
const error = ref<string | null>(null)
const isSubmitting = ref(false)

watch(
  () => reauth.isOpen,
  (open) => {
    if (open) {
      password.value = ''
      error.value = null
    }
  },
)

async function submit(): Promise<void> {
  isSubmitting.value = true
  error.value = null
  try {
    await reauth.confirm(password.value)
  } catch (caught) {
    error.value = errorMessage(caught)
  } finally {
    isSubmitting.value = false
  }
}
</script>

<template>
  <AppModal :open="reauth.isOpen" title="Potvrdite svoj identitet" width="440px" @close="reauth.close(false)">
    <form class="stack" @submit.prevent="submit">
      <div class="notice">
        <AppIcon name="shield" />
        <p>{{ reauth.reason || 'Radi vaše bezbednosti, za ovu radnju potrebno je da ponovo unesete lozinku.' }}</p>
      </div>
      <AppField v-model="password" label="Lozinka" type="password" autocomplete="current-password" required :error="error" />
      <div class="row-between">
        <AppButton variant="ghost" @click="reauth.close(false)">Odustani</AppButton>
        <AppButton type="submit" :loading="isSubmitting" :disabled="!password">Potvrdi</AppButton>
      </div>
    </form>
  </AppModal>
</template>

<style scoped>
.notice {
  display: flex;
  gap: var(--space-3);
  padding: var(--space-4);
  border-radius: var(--radius-md);
  background: var(--color-primary-soft);
  color: var(--color-text);
  font-size: var(--font-size-sm);
}

.notice :deep(svg) {
  color: var(--color-link);
}
</style>
