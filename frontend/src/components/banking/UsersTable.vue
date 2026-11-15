<script setup lang="ts">
import { onMounted, reactive, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import AppCard from '@/components/ui/AppCard.vue'
import AppField from '@/components/ui/AppField.vue'
import AppButton from '@/components/ui/AppButton.vue'
import AppBadge from '@/components/ui/AppBadge.vue'
import AppPagination from '@/components/ui/AppPagination.vue'
import StatusViews from '@/components/ui/StatusViews.vue'
import UserFormModal from './UserFormModal.vue'
import { usersApi } from '@/api/bank'
import { useUiStore } from '@/stores/ui'
import { errorMessage } from '@/composables/useAsync'
import { kycLabels, roleLabels } from '@/utils/labels'
import { formatDate } from '@/utils/format'
import type { PagedResponse, User, UserRole } from '@/types/api'

const props = defineProps<{ fixedRole?: UserRole; allowRoles: UserRole[] }>()

const router = useRouter()
const ui = useUiStore()
const filters = reactive({ search: '', role: (props.fixedRole ?? '') as UserRole | '', status: '' as '' | 'active' | 'inactive' })
const page = ref(1)
const result = ref<PagedResponse<User> | null>(null)
const isLoading = ref(true)
const error = ref<string | null>(null)
const isFormOpen = ref(false)
const editing = ref<User | null>(null)

const roleOptions = [{ value: '', label: 'Sve uloge' }, ...Object.entries(roleLabels).map(([value, label]) => ({ value, label }))]
const statusOptions = [
  { value: '', label: 'Svi statusi' },
  { value: 'active', label: 'Aktivni' },
  { value: 'inactive', label: 'Neaktivni' },
]

async function load(): Promise<void> {
  isLoading.value = true
  error.value = null
  try {
    result.value = await usersApi.list({
      search: filters.search.trim() || undefined,
      role: filters.role || undefined,
      isActive: filters.status === '' ? undefined : filters.status === 'active',
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

function openForm(user: User | null): void {
  editing.value = user
  isFormOpen.value = true
}

function onSaved(user: User): void {
  isFormOpen.value = false
  ui.notify(editing.value ? 'Korisnik je izmenjen.' : `Korisnik ${user.email} je kreiran.`)
  if (!editing.value && user.role === 'Korisnik') router.push(`/bankar/klijenti/${user.id}`)
  else load()
}

watch(page, load)
onMounted(load)
</script>

<template>
  <div class="stack">
    <AppCard>
      <form class="filters" @submit.prevent="search">
        <AppField v-model="filters.search" label="Pretraga" type="search" placeholder="Ime, prezime ili email" />
        <AppField v-if="!fixedRole" v-model="filters.role" label="Uloga" :options="roleOptions" />
        <AppField v-model="filters.status" label="Status" :options="statusOptions" />
        <div class="row">
          <AppButton type="submit" icon="search">Traži</AppButton>
          <AppButton variant="dark" icon="plus" @click="openForm(null)">{{ fixedRole === 'Korisnik' ? 'Novi klijent' : 'Novi korisnik' }}</AppButton>
        </div>
      </form>
    </AppCard>

    <AppCard :padded="false">
      <div class="card-body">
        <StatusViews v-if="isLoading" state="loading" :rows="6" />
        <StatusViews v-else-if="error" state="error" :message="error" @retry="load" />
        <StatusViews v-else-if="!result?.items.length" state="empty" icon="users" title="Nema rezultata" />
        <div v-else class="table-scroll">
          <table class="data-table">
            <thead>
              <tr>
                <th scope="col">Ime i prezime</th>
                <th scope="col">Email</th>
                <th v-if="!fixedRole" scope="col">Uloga</th>
                <th scope="col">KYC</th>
                <th scope="col">Status</th>
                <th scope="col">Kreiran</th>
                <th scope="col"><span class="visually-hidden">Radnje</span></th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="user in result.items" :key="user.id">
                <td>
                  <RouterLink v-if="user.role === 'Korisnik'" :to="`/bankar/klijenti/${user.id}`"><strong>{{ user.firstName }} {{ user.lastName }}</strong></RouterLink>
                  <strong v-else>{{ user.firstName }} {{ user.lastName }}</strong>
                </td>
                <td class="muted">{{ user.email }}</td>
                <td v-if="!fixedRole">{{ roleLabels[user.role] }}</td>
                <td><AppBadge :tone="kycLabels[user.kycStatus].tone">{{ kycLabels[user.kycStatus].label }}</AppBadge></td>
                <td>
                  <AppBadge v-if="user.isLocked" tone="danger">Zaključan</AppBadge>
                  <AppBadge v-else :tone="user.isActive ? 'success' : 'neutral'">{{ user.isActive ? 'Aktivan' : 'Neaktivan' }}</AppBadge>
                </td>
                <td class="subtle">{{ formatDate(user.createdAt) }}</td>
                <td>
                  <AppButton variant="ghost" size="sm" icon="edit" @click="openForm(user)">Izmeni</AppButton>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
        <AppPagination v-if="result" :page="result.page" :total-pages="result.totalPages" :total-count="result.totalCount" @change="page = $event" />
      </div>
    </AppCard>

    <UserFormModal :open="isFormOpen" :user="editing" :allow-roles="allowRoles" @close="isFormOpen = false" @saved="onSaved" />
  </div>
</template>

<style scoped>
.card-body {
  padding: var(--space-5) var(--space-6);
}

.filters {
  display: grid;
  grid-template-columns: 2fr 1fr 1fr auto;
  gap: var(--space-3);
  align-items: end;
}

@media (max-width: 960px) {
  .filters {
    grid-template-columns: minmax(0, 1fr);
  }
}
</style>
