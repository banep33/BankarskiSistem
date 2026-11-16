<script setup lang="ts">
import { computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import AppButton from '@/components/ui/AppButton.vue'
import { useAuthStore } from '@/stores/auth'
import AppCard from '@/components/ui/AppCard.vue'
import StatusViews from '@/components/ui/StatusViews.vue'
import { helpApi } from '@/api/bank'
import { useAsync } from '@/composables/useAsync'
import { formatDate } from '@/utils/format'

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()

function goBack(): void {
  const previous = (window.history.state as { back?: string | null } | null)?.back
  if (previous) router.back()
  else router.push(auth.isAuthenticated ? auth.homeRoute : '/prijava')
}
const { data: documents, isLoading, error, load } = useAsync(() => helpApi.legalDocuments(), [])

const document = computed(() => {
  const type = route.params.type === 'uslovi' ? 'UsloviKoriscenja' : 'PolitikaPrivatnosti'
  return documents.value.find((item) => item.documentType === type)
})

onMounted(load)
</script>

<template>
  <div class="page">
    <StatusViews v-if="isLoading" state="loading" :rows="6" />
    <StatusViews v-else-if="error" state="error" :message="error" @retry="load" />
    <template v-else-if="document">
      <AppButton class="back" variant="ghost" size="sm" icon="arrow-left" @click="goBack">Nazad</AppButton>
      <header>
        <h1 class="page-title">{{ document.title }}</h1>
        <p class="page-subtitle">Verzija {{ document.version }}, objavljena {{ formatDate(document.publishedAt) }}</p>
      </header>
      <AppCard>
        <article class="legal">
          <p v-for="(paragraph, index) in document.content.split('\n').filter(Boolean)" :key="index" :class="{ heading: /^\d+\./.test(paragraph) }">
            {{ paragraph }}
          </p>
        </article>
      </AppCard>
    </template>
  </div>
</template>

<style scoped>
.back {
  align-self: flex-start;
}

.legal {
  display: flex;
  flex-direction: column;
  gap: var(--space-3);
  line-height: 1.7;
  color: var(--color-text-muted);
}

.heading {
  margin-top: var(--space-4);
  font-weight: 600;
  color: var(--color-text);
  font-size: var(--font-size-lg);
}
</style>
