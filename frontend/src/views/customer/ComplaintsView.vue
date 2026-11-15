<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import AppCard from '@/components/ui/AppCard.vue'
import AppButton from '@/components/ui/AppButton.vue'
import AppField from '@/components/ui/AppField.vue'
import AppModal from '@/components/ui/AppModal.vue'
import AppBadge from '@/components/ui/AppBadge.vue'
import AppPagination from '@/components/ui/AppPagination.vue'
import StatusViews from '@/components/ui/StatusViews.vue'
import { complaintsApi } from '@/api/bank'
import { useUiStore } from '@/stores/ui'
import { errorMessage } from '@/composables/useAsync'
import { complaintCategoryLabels, complaintStatusLabels } from '@/utils/labels'
import { formatDate } from '@/utils/format'
import type { ComplaintCategory, ComplaintSummary, PagedResponse } from '@/types/api'

const route = useRoute()
const router = useRouter()
const ui = useUiStore()
const result = ref<PagedResponse<ComplaintSummary> | null>(null)
const page = ref(1)
const isLoading = ref(true)
const error = ref<string | null>(null)
const isFormOpen = ref(false)
const isSubmitting = ref(false)
const formError = ref<string | null>(null)

const form = reactive({
  category: 'PlatniPromet' as ComplaintCategory,
  subject: '',
  description: '',
  relatedTransactionId: null as number | null,
})

const categoryOptions = Object.entries(complaintCategoryLabels).map(([value, label]) => ({ value, label }))

async function load(): Promise<void> {
  isLoading.value = true
  error.value = null
  try {
    result.value = await complaintsApi.mine(page.value)
  } catch (caught) {
    error.value = errorMessage(caught)
  } finally {
    isLoading.value = false
  }
}

async function submit(): Promise<void> {
  formError.value = null
  if (form.subject.trim().length < 5 || form.description.trim().length < 20) {
    formError.value = 'Naslov mora imati najmanje 5, a opis najmanje 20 karaktera.'
    return
  }
  isSubmitting.value = true
  try {
    const created = await complaintsApi.submit({
      category: form.category,
      subject: form.subject.trim(),
      description: form.description.trim(),
      relatedTransactionId: form.relatedTransactionId,
    })
    isFormOpen.value = false
    ui.notify(`Prigovor ${created.summary.referenceNumber} je primljen. Odgovor dobijate najkasnije ${formatDate(created.summary.dueAt)}.`)
    await router.push(`/prigovori/${created.summary.id}`)
  } catch (caught) {
    formError.value = errorMessage(caught)
  } finally {
    isSubmitting.value = false
  }
}

function changePage(value: number): void {
  page.value = value
  load()
}

onMounted(() => {
  const transactionId = Number(route.query.transakcija)
  if (transactionId) {
    form.relatedTransactionId = transactionId
    form.category = 'PlatniPromet'
    isFormOpen.value = true
  }
  load()
})
</script>

<template>
  <div class="page">
    <header class="page-header">
      <div>
        <h1 class="page-title">Prigovori</h1>
        <p class="page-subtitle">Banka je dužna da odgovori u roku od 15 dana, uz mogućnost produženja za još 15 dana uz obrazloženje.</p>
      </div>
      <AppButton icon="plus" @click="isFormOpen = true">Novi prigovor</AppButton>
    </header>

    <div class="info-strip">
      <AppCard tone="cream">
        <h3>Kako teče postupak</h3>
        <ol class="process">
          <li><strong>Podnet</strong> dobijate broj predmeta i rok za odgovor</li>
          <li><strong>U obradi</strong> zaposleni proverava transakcije i dokumentaciju</li>
          <li><strong>Odgovor</strong> dobijate pisani odgovor u aplikaciji</li>
        </ol>
        <p class="subtle">Ako niste zadovoljni odgovorom, možete se obratiti Narodnoj banci Srbije (Centar za zaštitu korisnika finansijskih usluga).</p>
      </AppCard>
    </div>

    <AppCard :padded="false">
      <div class="card-body">
        <StatusViews v-if="isLoading" state="loading" :rows="4" />
        <StatusViews v-else-if="error" state="error" :message="error" @retry="load" />
        <StatusViews v-else-if="!result?.items.length" state="empty" icon="chat" title="Nemate podnetih prigovora" message="Ako nešto nije u redu sa uslugom, javite nam se." />
        <ul v-else class="complaints">
          <li v-for="complaint in result.items" :key="complaint.id">
            <RouterLink :to="`/prigovori/${complaint.id}`" class="complaint">
              <div class="complaint-main">
                <span class="mono subtle">{{ complaint.referenceNumber }}</span>
                <strong>{{ complaint.subject }}</strong>
                <span class="subtle">{{ complaintCategoryLabels[complaint.category] }} · podnet {{ formatDate(complaint.createdAt) }}</span>
              </div>
              <div class="complaint-side">
                <AppBadge :tone="complaintStatusLabels[complaint.status].tone">{{ complaintStatusLabels[complaint.status].label }}</AppBadge>
                <span class="subtle">Rok {{ formatDate(complaint.dueAt) }}<template v-if="complaint.deadlineExtended"> (produžen)</template></span>
              </div>
            </RouterLink>
          </li>
        </ul>
        <AppPagination v-if="result" :page="result.page" :total-pages="result.totalPages" :total-count="result.totalCount" @change="changePage" />
      </div>
    </AppCard>

    <AppModal :open="isFormOpen" title="Novi prigovor" width="620px" @close="isFormOpen = false">
      <form id="complaint-form" class="stack" @submit.prevent="submit">
        <AppField v-model="form.category" label="Oblast" :options="categoryOptions" required />
        <AppField v-model="form.subject" label="Naslov" required :maxlength="150" placeholder="Ukratko, šta je problem" />
        <AppField
          v-model="form.description"
          label="Opis"
          multiline
          :rows="6"
          required
          :maxlength="5000"
          hint="Navedite datum, iznos i šta očekujete od banke. Nikada ne upisujte lozinku ili PIN."
        />
        <p v-if="form.relatedTransactionId" class="linked">Prigovor se odnosi na transakciju #{{ form.relatedTransactionId }}.</p>
        <p v-if="formError" class="form-error" role="alert">{{ formError }}</p>
      </form>
      <template #footer>
        <AppButton variant="ghost" @click="isFormOpen = false">Odustani</AppButton>
        <AppButton type="submit" form="complaint-form" :loading="isSubmitting">Podnesi prigovor</AppButton>
      </template>
    </AppModal>
  </div>
</template>

<style scoped>
.process {
  margin: var(--space-3) 0;
  padding-left: var(--space-5);
  display: flex;
  flex-direction: column;
  gap: var(--space-1);
  font-size: var(--font-size-sm);
}

.complaints {
  list-style: none;
  margin: 0;
  padding: 0;
}

.complaints li + li {
  border-top: 1px solid var(--color-border);
}

.complaint {
  display: flex;
  justify-content: space-between;
  gap: var(--space-4);
  padding: var(--space-4) var(--space-2);
  text-decoration: none;
  border-radius: var(--radius-md);
}

.complaint:hover {
  background: var(--color-surface-muted);
}

.complaint-main,
.complaint-side {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.complaint-side {
  align-items: flex-end;
  text-align: right;
}

.linked {
  padding: var(--space-3);
  border-radius: var(--radius-md);
  background: var(--color-info-soft);
  color: var(--color-info);
  font-size: var(--font-size-sm);
}

.form-error {
  color: var(--color-danger);
  font-size: var(--font-size-sm);
}
</style>
