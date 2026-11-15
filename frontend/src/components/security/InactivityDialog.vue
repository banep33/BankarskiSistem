<script setup lang="ts">
import AppModal from '@/components/ui/AppModal.vue'
import AppButton from '@/components/ui/AppButton.vue'

defineProps<{ open: boolean; secondsLeft: number }>()
defineEmits<{ stay: []; logout: [] }>()
</script>

<template>
  <AppModal :open="open" title="Da li ste još tu?" width="420px" :dismissible="false">
    <p class="muted">
      Zbog neaktivnosti bićete automatski odjavljeni za <strong class="countdown">{{ secondsLeft }} s</strong>. Tako štitimo
      vaš nalog ako ste se udaljili od uređaja.
    </p>
    <template #footer>
      <AppButton variant="ghost" @click="$emit('logout')">Odjavi me</AppButton>
      <AppButton @click="$emit('stay')">Ostajem prijavljen/a</AppButton>
    </template>
  </AppModal>
</template>

<style scoped>
.countdown {
  color: var(--color-link);
  font-variant-numeric: tabular-nums;
}
</style>
