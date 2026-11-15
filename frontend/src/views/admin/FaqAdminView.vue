<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import AppCard from '@/components/ui/AppCard.vue'
import AppBadge from '@/components/ui/AppBadge.vue'
import AppButton from '@/components/ui/AppButton.vue'
import AppField from '@/components/ui/AppField.vue'
import AppModal from '@/components/ui/AppModal.vue'
import StatusViews from '@/components/ui/StatusViews.vue'
import { adminApi } from '@/api/bank'
import { useAsync, errorMessage } from '@/composables/useAsync'
import { useUiStore } from '@/stores/ui'
import type { FaqArticle } from '@/types/api'

const ui = useUiStore()
const { data: articles, isLoading, error, load } = useAsync(() => adminApi.faq(), [] as FaqArticle[])
const editor = reactive({ open: false, id: undefined as number | undefined, category: '', question: '', answer: '', sortOrder: 0 as number | string, isPublished: true, saving: false })
const toDelete = ref<FaqArticle | null>(null)

function edit(article: FaqArticle | null): void {
  Object.assign(editor, {
    open: true,
    id: article?.id,
    category: article?.category ?? '',
    question: article?.question ?? '',
    answer: article?.answer ?? '',
    sortOrder: article?.sortOrder ?? (articles.value.length + 1) * 10,
    isPublished: article?.isPublished ?? true,
  })
}

async function save(): Promise<void> {
  editor.saving = true
  try {
    await adminApi.saveFaq(
      { category: editor.category.trim(), question: editor.question.trim(), answer: editor.answer.trim(), sortOrder: Number(editor.sortOrder) || 0, isPublished: editor.isPublished },
      editor.id,
    )
    editor.open = false
    ui.notify('Pitanje je sačuvano.')
    await load()
  } catch (caught) {
    ui.notify(errorMessage(caught), 'danger')
  } finally {
    editor.saving = false
  }
}

async function remove(): Promise<void> {
  if (!toDelete.value) return
  try {
    await adminApi.deleteFaq(toDelete.value.id)
    toDelete.value = null
    ui.notify('Pitanje je obrisano.')
    await load()
  } catch (caught) {
    ui.notify(errorMessage(caught), 'danger')
  }
}

onMounted(load)
</script>

<template>
  <div class="page">
    <header class="page-header">
      <div>
        <h1 class="page-title">Centar za pomoć</h1>
        <p class="page-subtitle">Česta pitanja koja klijenti vide u aplikaciji i na javnoj stranici pomoći.</p>
      </div>
      <AppButton icon="plus" @click="edit(null)">Novo pitanje</AppButton>
    </header>

    <AppCard :padded="false">
      <div class="card-body">
        <StatusViews v-if="isLoading" state="loading" :rows="6" />
        <StatusViews v-else-if="error" state="error" :message="error" @retry="load" />
        <StatusViews v-else-if="!articles.length" state="empty" icon="help" title="Nema pitanja" />
        <div v-else class="table-scroll">
          <table class="data-table">
            <thead>
              <tr>
                <th scope="col">Redosled</th>
                <th scope="col">Kategorija</th>
                <th scope="col">Pitanje</th>
                <th scope="col">Status</th>
                <th scope="col"><span class="visually-hidden">Radnje</span></th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="article in articles" :key="article.id">
                <td class="subtle">{{ article.sortOrder }}</td>
                <td>{{ article.category }}</td>
                <td><strong>{{ article.question }}</strong></td>
                <td><AppBadge :tone="article.isPublished ? 'success' : 'neutral'">{{ article.isPublished ? 'Objavljeno' : 'Skriveno' }}</AppBadge></td>
                <td class="actions">
                  <AppButton variant="ghost" size="sm" icon="edit" @click="edit(article)">Izmeni</AppButton>
                  <AppButton variant="ghost" size="sm" icon="trash" @click="toDelete = article">Obriši</AppButton>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </AppCard>

    <AppModal :open="editor.open" :title="editor.id ? 'Izmena pitanja' : 'Novo pitanje'" width="640px" @close="editor.open = false">
      <form id="faq-form" class="stack" @submit.prevent="save">
        <div class="grid-2">
          <AppField v-model="editor.category" label="Kategorija" required :maxlength="60" />
          <AppField v-model="editor.sortOrder" label="Redosled" type="number" />
        </div>
        <AppField v-model="editor.question" label="Pitanje" required :maxlength="255" />
        <AppField v-model="editor.answer" label="Odgovor" multiline :rows="6" required :maxlength="10000" />
        <label class="check"><input v-model="editor.isPublished" type="checkbox" /> Objavljeno</label>
      </form>
      <template #footer>
        <AppButton variant="ghost" @click="editor.open = false">Odustani</AppButton>
        <AppButton type="submit" form="faq-form" :loading="editor.saving">Sačuvaj</AppButton>
      </template>
    </AppModal>

    <AppModal :open="toDelete !== null" title="Obrisati pitanje?" width="420px" @close="toDelete = null">
      <p class="muted">„{{ toDelete?.question }}“ više neće biti vidljivo klijentima.</p>
      <template #footer>
        <AppButton variant="ghost" @click="toDelete = null">Odustani</AppButton>
        <AppButton variant="danger" @click="remove">Obriši</AppButton>
      </template>
    </AppModal>
  </div>
</template>

<style scoped>
.card-body {
  padding: var(--space-5) var(--space-6);
}

.actions {
  display: flex;
  gap: var(--space-1);
  justify-content: flex-end;
}

.check {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  font-size: var(--font-size-sm);
  cursor: pointer;
}
</style>
