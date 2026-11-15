<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import AppModal from '@/components/ui/AppModal.vue'
import AppField from '@/components/ui/AppField.vue'
import AppButton from '@/components/ui/AppButton.vue'
import { usersApi } from '@/api/bank'
import { errorMessage } from '@/composables/useAsync'
import { passwordIssues } from '@/utils/validation'
import { roleLabels } from '@/utils/labels'
import type { User, UserRole } from '@/types/api'

const props = defineProps<{ open: boolean; user: User | null; allowRoles: UserRole[] }>()
const emit = defineEmits<{ close: []; saved: [user: User] }>()

const form = reactive({ firstName: '', lastName: '', email: '', password: '', confirmPassword: '', role: 'Korisnik' as UserRole })
const error = ref<string | null>(null)
const isSaving = ref(false)
const roleOptions = computed(() => props.allowRoles.map((role) => ({ value: role, label: roleLabels[role] })))

watch(
  () => props.open,
  (open) => {
    if (!open) return
    error.value = null
    Object.assign(form, {
      firstName: props.user?.firstName ?? '',
      lastName: props.user?.lastName ?? '',
      email: props.user?.email ?? '',
      password: '',
      confirmPassword: '',
      role: props.user?.role ?? props.allowRoles[0] ?? 'Korisnik',
    })
  },
)

async function save(): Promise<void> {
  error.value = null
  if (!props.user) {
    const issues = passwordIssues(form.password)
    if (issues.length) {
      error.value = `Lozinka mora sadržati: ${issues.join(', ')}.`
      return
    }
    if (form.password !== form.confirmPassword) {
      error.value = 'Lozinka i potvrda se ne poklapaju.'
      return
    }
  }
  isSaving.value = true
  try {
    const saved = props.user
      ? await usersApi.update(props.user.id, { firstName: form.firstName.trim(), lastName: form.lastName.trim(), email: form.email.trim(), role: form.role })
      : await usersApi.create({ ...form, firstName: form.firstName.trim(), lastName: form.lastName.trim(), email: form.email.trim() })
    emit('saved', saved)
  } catch (caught) {
    error.value = errorMessage(caught)
  } finally {
    isSaving.value = false
  }
}
</script>

<template>
  <AppModal :open="open" :title="user ? 'Izmena korisnika' : 'Novi korisnik'" width="560px" @close="emit('close')">
    <form id="user-form" class="stack" @submit.prevent="save">
      <div class="grid-2">
        <AppField v-model="form.firstName" label="Ime" required :maxlength="50" />
        <AppField v-model="form.lastName" label="Prezime" required :maxlength="50" />
      </div>
      <AppField v-model="form.email" label="Email" type="email" required :maxlength="100" />
      <AppField v-if="roleOptions.length > 1" v-model="form.role" label="Uloga" :options="roleOptions" />
      <template v-if="!user">
        <div class="grid-2">
          <AppField v-model="form.password" label="Početna lozinka" type="password" autocomplete="new-password" required />
          <AppField v-model="form.confirmPassword" label="Potvrda lozinke" type="password" autocomplete="new-password" required />
        </div>
        <p class="subtle">Lozinku predajte klijentu lično i savetujte promenu pri prvoj prijavi.</p>
      </template>
      <p v-if="error" class="form-error" role="alert">{{ error }}</p>
    </form>
    <template #footer>
      <AppButton variant="ghost" @click="emit('close')">Odustani</AppButton>
      <AppButton type="submit" form="user-form" :loading="isSaving">Sačuvaj</AppButton>
    </template>
  </AppModal>
</template>

<style scoped>
.form-error {
  color: var(--color-danger);
  font-size: var(--font-size-sm);
}
</style>
