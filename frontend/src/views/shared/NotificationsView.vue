<script setup lang="ts">
import { onMounted, ref, watch } from 'vue'
import AppCard from '@/components/ui/AppCard.vue'
import AppButton from '@/components/ui/AppButton.vue'
import AppIcon from '@/components/ui/AppIcon.vue'
import AppPagination from '@/components/ui/AppPagination.vue'
import StatusViews from '@/components/ui/StatusViews.vue'
import { notificationsApi } from '@/api/bank'
import { useUiStore } from '@/stores/ui'
import { errorMessage } from '@/composables/useAsync'
import { notificationIcons } from '@/utils/labels'
import { formatDateTime, formatRelative } from '@/utils/format'
import type { AppNotification, PagedResponse } from '@/types/api'

const ui = useUiStore()
const unreadOnly = ref(false)
const page = ref(1)
const result = ref<PagedResponse<AppNotification> | null>(null)
const isLoading = ref(true)
const error = ref<string | null>(null)

async function load(): Promise<void> {
  isLoading.value = true
  error.value = null
  try {
    result.value = await notificationsApi.list(unreadOnly.value, page.value)
  } catch (caught) {
    error.value = errorMessage(caught)
  } finally {
    isLoading.value = false
  }
}

async function markRead(notification: AppNotification): Promise<void> {
  if (notification.isRead) return
  try {
    await notificationsApi.markRead(notification.id)
    notification.isRead = true
  } catch (caught) {
    ui.notify(errorMessage(caught), 'danger')
  }
}

async function markAll(): Promise<void> {
  try {
    const { count } = await notificationsApi.markAllRead()
    ui.notify(count ? `Označeno kao pročitano: ${count}` : 'Sva obaveštenja su već pročitana.', 'info')
    await load()
  } catch (caught) {
    ui.notify(errorMessage(caught), 'danger')
  }
}

watch(unreadOnly, () => {
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
        <h1 class="page-title">Obaveštenja</h1>
        <p class="page-subtitle">Brojevi računa su skraćeni radi zaštite, a obaveštenja se automatski brišu posle propisanog roka.</p>
      </div>
      <div class="row">
        <label class="only-unread">
          <input v-model="unreadOnly" type="checkbox" />
          Samo nepročitana
        </label>
        <AppButton variant="secondary" icon="check" @click="markAll">Označi sve kao pročitano</AppButton>
      </div>
    </header>

    <AppCard>
      <StatusViews v-if="isLoading" state="loading" :rows="6" />
      <StatusViews v-else-if="error" state="error" :message="error" @retry="load" />
      <StatusViews v-else-if="!result?.items.length" state="empty" icon="bell" title="Nema obaveštenja" message="Ovde stižu potvrde plaćanja, uplate i bezbednosna upozorenja." />
      <ul v-else class="notifications">
        <li v-for="notification in result.items" :key="notification.id">
          <button type="button" class="notification" :class="{ unread: !notification.isRead }" @click="markRead(notification)">
            <span class="icon" :class="notification.type === 'Bezbednost' ? 'security' : ''">
              <AppIcon :name="notificationIcons[notification.type]" :size="18" />
            </span>
            <span class="body">
              <span class="title">{{ notification.title }}</span>
              <span class="message">{{ notification.message }}</span>
            </span>
            <span class="time" :title="formatDateTime(notification.createdAt)">{{ formatRelative(notification.createdAt) }}</span>
            <span v-if="!notification.isRead" class="dot" aria-label="Nepročitano" />
          </button>
        </li>
      </ul>
      <AppPagination v-if="result" :page="result.page" :total-pages="result.totalPages" :total-count="result.totalCount" @change="page = $event" />
    </AppCard>
  </div>
</template>

<style scoped>
.only-unread {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  font-size: var(--font-size-sm);
  cursor: pointer;
}

.only-unread input {
  accent-color: var(--color-primary);
}

.notifications {
  list-style: none;
  margin: 0;
  padding: 0;
}

.notifications li + li {
  border-top: 1px solid var(--color-border);
}

.notification {
  display: flex;
  align-items: flex-start;
  gap: var(--space-3);
  width: 100%;
  padding: var(--space-4) var(--space-2);
  border: none;
  border-radius: var(--radius-md);
  background: transparent;
  text-align: left;
  cursor: pointer;
}

.notification:hover {
  background: var(--color-surface-muted);
}

.notification.unread .title {
  font-weight: 700;
}

.icon {
  display: grid;
  place-items: center;
  width: 40px;
  height: 40px;
  flex-shrink: 0;
  border-radius: 50%;
  background: var(--color-cream);
  color: var(--color-coral-700);
}

.icon.security {
  background: var(--color-forest-900);
  color: var(--color-lime-400);
}

.body {
  display: flex;
  flex-direction: column;
  gap: 2px;
  flex: 1;
  min-width: 0;
}

.title {
  font-weight: 600;
}

.message {
  color: var(--color-text-muted);
  font-size: var(--font-size-sm);
}

.time {
  font-size: var(--font-size-xs);
  color: var(--color-text-subtle);
  white-space: nowrap;
}

.dot {
  width: 9px;
  height: 9px;
  margin-top: 6px;
  border-radius: 50%;
  background: var(--color-primary);
}
</style>
