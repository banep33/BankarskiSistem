<script setup lang="ts">
import AppButton from './AppButton.vue'

defineProps<{ page: number; totalPages: number; totalCount: number }>()
defineEmits<{ change: [page: number] }>()
</script>

<template>
  <nav v-if="totalPages > 1" class="pagination" aria-label="Stranice">
    <span class="subtle">Ukupno {{ totalCount }}, strana {{ page }} od {{ totalPages }}</span>
    <div class="row">
      <AppButton variant="secondary" size="sm" icon="arrow-left" :disabled="page <= 1" @click="$emit('change', page - 1)">Prethodna</AppButton>
      <AppButton variant="secondary" size="sm" icon-right="arrow-right" :disabled="page >= totalPages" @click="$emit('change', page + 1)">Sledeća</AppButton>
    </div>
  </nav>
</template>

<style scoped>
.pagination {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-3);
  flex-wrap: wrap;
  padding-top: var(--space-4);
}
</style>
