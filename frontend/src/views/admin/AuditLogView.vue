<script setup lang="ts">
import { onMounted, reactive, ref, watch } from 'vue'
import AppCard from '@/components/ui/AppCard.vue'
import AppField from '@/components/ui/AppField.vue'
import AppButton from '@/components/ui/AppButton.vue'
import AppBadge from '@/components/ui/AppBadge.vue'
import AppPagination from '@/components/ui/AppPagination.vue'
import StatusViews from '@/components/ui/StatusViews.vue'
import { adminApi } from '@/api/bank'
import { errorMessage } from '@/composables/useAsync'
import { formatDateTime } from '@/utils/format'
import type { AuditLogEntry, PagedResponse } from '@/types/api'

const filters = reactive({ search: '', from: '', to: '', onlyFailures: false })
const page = ref(1)
const result = ref<PagedResponse<AuditLogEntry> | null>(null)
const isLoading = ref(true)
const error = ref<string | null>(null)
const expanded = ref<number | null>(null)

async function load(): Promise<void> {
  isLoading.value = true
  error.value = null
  try {
    result.value = await adminApi.auditLogs({
      search: filters.search.trim() || undefined,
      from: filters.from || undefined,
      to: filters.to || undefined,
      onlyFailures: filters.onlyFailures || undefined,
      page: page.value,
    })
  } catch (caught) {
    error.value = errorMessage(caught)
  } finally {
    isLoading.value = false
  }
}

function search(): void {
  page.value = 1
  load()
}

watch(page, load)
onMounted(load)
</script>

<template>
  <div class="page">
    <header class="page-header">
      <div>
        <h1 class="page-title">Audit log</h1>
        <p class="page-subtitle">Nepromenljiv zapis osetljivih radnji: ko je, kada i sa koje adrese uradio šta.</p>
      </div>
    </header>

    <AppCard>
      <form class="filters" @submit.prevent="search">
        <AppField v-model="filters.search" label="Pretraga" type="search" placeholder="Radnja, email ili entitet" />
        <AppField v-model="filters.from" label="Od" type="date" />
        <AppField v-model="filters.to" label="Do" type="date" />
        <label class="check"><input v-model="filters.onlyFailures" type="checkbox" /> Samo neuspešne</label>
        <AppButton type="submit" icon="search">Traži</AppButton>
      </form>
    </AppCard>

    <AppCard :padded="false">
      <div class="card-body">
        <StatusViews v-if="isLoading" state="loading" :rows="8" />
        <StatusViews v-else-if="error" state="error" :message="error" @retry="load" />
        <StatusViews v-else-if="!result?.items.length" state="empty" icon="activity" title="Nema zapisa" />
        <div v-else class="table-scroll">
          <table class="data-table">
            <thead>
              <tr>
                <th scope="col">Vreme</th>
                <th scope="col">Korisnik</th>
                <th scope="col">Radnja</th>
                <th scope="col">Entitet</th>
                <th scope="col">IP</th>
                <th scope="col">Ishod</th>
              </tr>
            </thead>
            <tbody>
              <template v-for="entry in result.items" :key="entry.id">
                <tr class="clickable" :aria-expanded="expanded === entry.id" @click="expanded = expanded === entry.id ? null : entry.id">
                  <td class="subtle">{{ formatDateTime(entry.createdAt) }}</td>
                  <td>{{ entry.userEmail ?? 'Sistem' }}</td>
                  <td><strong>{{ entry.action }}</strong></td>
                  <td>{{ entry.entityType ? `${entry.entityType} #${entry.entityId ?? ''}` : '' }}</td>
                  <td class="mono subtle">{{ entry.ipAddress }}</td>
                  <td><AppBadge :tone="entry.isSuccess ? 'success' : 'danger'">{{ entry.isSuccess ? 'Uspešno' : 'Neuspešno' }}</AppBadge></td>
                </tr>
                <tr v-if="expanded === entry.id" class="detail-row">
                  <td colspan="6">
                    <dl class="detail">
                      <div v-if="entry.oldValue"><dt>Pre</dt><dd>{{ entry.oldValue }}</dd></div>
                      <div v-if="entry.newValue"><dt>Posle</dt><dd>{{ entry.newValue }}</dd></div>
                      <div v-if="entry.errorMessage"><dt>Greška</dt><dd>{{ entry.errorMessage }}</dd></div>
                      <div v-if="!entry.oldValue && !entry.newValue && !entry.errorMessage"><dt>Detalji</dt><dd>Nema dodatnih podataka.</dd></div>
                    </dl>
                  </td>
                </tr>
              </template>
            </tbody>
          </table>
        </div>
        <AppPagination v-if="result" :page="result.page" :total-pages="result.totalPages" :total-count="result.totalCount" @change="page = $event" />
      </div>
    </AppCard>
  </div>
</template>

<style scoped>
.card-body {
  padding: var(--space-5) var(--space-6);
}

.filters {
  display: grid;
  grid-template-columns: 2fr 1fr 1fr auto auto;
  gap: var(--space-3);
  align-items: end;
}

.check {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  padding-bottom: var(--space-3);
  font-size: var(--font-size-sm);
  white-space: nowrap;
  cursor: pointer;
}

.clickable {
  cursor: pointer;
}

.detail-row td {
  background: var(--color-surface-muted);
}

.detail {
  margin: 0;
  display: flex;
  flex-direction: column;
  gap: var(--space-2);
  font-size: var(--font-size-sm);
}

.detail div {
  display: grid;
  grid-template-columns: 80px 1fr;
  gap: var(--space-3);
}

.detail dt {
  color: var(--color-text-muted);
}

.detail dd {
  margin: 0;
  word-break: break-word;
}

@media (max-width: 960px) {
  .filters {
    grid-template-columns: minmax(0, 1fr);
  }
}
</style>
