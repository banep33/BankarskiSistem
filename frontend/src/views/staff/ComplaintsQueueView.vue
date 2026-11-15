<script setup lang="ts">
import { onMounted, reactive, ref, watch } from 'vue'
import AppCard from '@/components/ui/AppCard.vue'
import AppField from '@/components/ui/AppField.vue'
import AppBadge from '@/components/ui/AppBadge.vue'
import AppPagination from '@/components/ui/AppPagination.vue'
import StatusViews from '@/components/ui/StatusViews.vue'
import { complaintsApi } from '@/api/bank'
import { errorMessage } from '@/composables/useAsync'
import { complaintCategoryLabels, complaintStatusLabels } from '@/utils/labels'
import { formatDate } from '@/utils/format'
import type { ComplaintStatus, ComplaintSummary, PagedResponse } from '@/types/api'

const filters = reactive({ status: '' as ComplaintStatus | '', onlyOverdue: false, onlyUnassigned: false })
const page = ref(1)
const result = ref<PagedResponse<ComplaintSummary> | null>(null)
const isLoading = ref(true)
const error = ref<string | null>(null)

const statusOptions = [{ value: '', label: 'Svi statusi' }, ...Object.entries(complaintStatusLabels).map(([value, item]) => ({ value, label: item.label }))]

async function load(): Promise<void> {
  isLoading.value = true
  error.value = null
  try {
    result.value = await complaintsApi.queue({
      status: filters.status || undefined,
      onlyOverdue: filters.onlyOverdue || undefined,
      onlyUnassigned: filters.onlyUnassigned || undefined,
      page: page.value,
    })
  } catch (caught) {
    error.value = errorMessage(caught)
  } finally {
    isLoading.value = false
  }
}

watch(
  () => ({ ...filters }),
  () => {
    page.value = 1
    load()
  },
)
watch(page, load)
onMounted(load)
</script>

<template>
  <div class="page">
    <header class="page-header">
      <div>
        <h1 class="page-title">Prigovori klijenata</h1>
        <p class="page-subtitle">Zakonski rok za odgovor je 15 dana, uz jedno produženje od 15 dana uz obrazloženje.</p>
      </div>
    </header>

    <AppCard>
      <div class="filters">
        <AppField v-model="filters.status" label="Status" :options="statusOptions" />
        <label class="check"><input v-model="filters.onlyOverdue" type="checkbox" /> Samo prekoračen rok</label>
        <label class="check"><input v-model="filters.onlyUnassigned" type="checkbox" /> Samo nedodeljeni</label>
      </div>
    </AppCard>

    <AppCard :padded="false">
      <div class="card-body">
        <StatusViews v-if="isLoading" state="loading" :rows="6" />
        <StatusViews v-else-if="error" state="error" :message="error" @retry="load" />
        <StatusViews v-else-if="!result?.items.length" state="empty" icon="chat" title="Nema prigovora za izabrane filtere" />
        <div v-else class="table-scroll">
          <table class="data-table">
            <thead>
              <tr>
                <th scope="col">Predmet</th>
                <th scope="col">Klijent</th>
                <th scope="col">Oblast</th>
                <th scope="col">Status</th>
                <th scope="col">Rok</th>
                <th scope="col">Zadužen</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="complaint in result.items" :key="complaint.id" :class="{ overdue: complaint.isOverdue }">
                <td>
                  <RouterLink :to="`/bankar/prigovori/${complaint.id}`"><strong>{{ complaint.subject }}</strong></RouterLink>
                  <div class="subtle mono">{{ complaint.referenceNumber }}</div>
                </td>
                <td>{{ complaint.customerName }}</td>
                <td>{{ complaintCategoryLabels[complaint.category] }}</td>
                <td>
                  <AppBadge :tone="complaintStatusLabels[complaint.status].tone">{{ complaintStatusLabels[complaint.status].label }}</AppBadge>
                  <AppBadge v-if="complaint.priority === 'Visok'" tone="danger">Visok prioritet</AppBadge>
                </td>
                <td>
                  <span :class="{ late: complaint.isOverdue }">{{ formatDate(complaint.dueAt) }}</span>
                  <div v-if="complaint.deadlineExtended" class="subtle">produžen</div>
                </td>
                <td>{{ complaint.assignedTo ?? 'Nije dodeljen' }}</td>
              </tr>
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
  display: flex;
  align-items: flex-end;
  gap: var(--space-6);
  flex-wrap: wrap;
}

.filters > :first-child {
  min-width: 220px;
}

.check {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  padding-bottom: var(--space-3);
  font-size: var(--font-size-sm);
  cursor: pointer;
}

.check input {
  accent-color: var(--color-primary);
}

td :deep(.badge) + :deep(.badge) {
  margin-left: 4px;
}

.late {
  color: var(--color-danger);
  font-weight: 600;
}
</style>
