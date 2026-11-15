<script setup lang="ts">
import { onMounted, reactive, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import AppCard from '@/components/ui/AppCard.vue'
import AppField from '@/components/ui/AppField.vue'
import AppButton from '@/components/ui/AppButton.vue'
import AppBadge from '@/components/ui/AppBadge.vue'
import AppModal from '@/components/ui/AppModal.vue'
import AppPagination from '@/components/ui/AppPagination.vue'
import StatusViews from '@/components/ui/StatusViews.vue'
import UserFormModal from './UserFormModal.vue'
import { usersApi } from '@/api/bank'
import { useAuthStore } from '@/stores/auth'
import { useUiStore } from '@/stores/ui'
import { errorMessage } from '@/composables/useAsync'
import { kycLabels, roleLabels } from '@/utils/labels'
import { formatDate } from '@/utils/format'
import type { PagedResponse, User, UserRole } from '@/types/api'

const props = defineProps<{ fixedRole?: UserRole; allowRoles: UserRole[] }>()

const router = useRouter()
const ui = useUiStore()
const auth = useAuthStore()
const filters = reactive({ search: '', role: (props.fixedRole ?? '') as UserRole | '', status: '' as '' | 'active' | 'inactive' })
const page = ref(1)
const result = ref<PagedResponse<User> | null>(null)
const isLoading = ref(true)
const error = ref<string | null>(null)
const isFormOpen = ref(false)
const editing = ref<User | null>(null)
const pendingAction = ref<{ user: User; action: 'deactivate' | 'remove' | 'reset2fa' } | null>(null)
const isActing = ref(false)

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

function isSelf(user: User): boolean {
  return user.id === auth.session?.userId
}

async function lifecycle(user: User, action: 'activate' | 'deactivate' | 'unlock'): Promise<void> {
  isActing.value = true
  try {
    const updated = await usersApi[action](user.id)
    if (result.value) result.value.items = result.value.items.map((item) => (item.id === updated.id ? updated : item))
    ui.notify(action === 'unlock' ? 'Nalog je otključan.' : action === 'activate' ? 'Nalog je aktiviran.' : 'Nalog je deaktiviran i sve sesije su prekinute.')
    pendingAction.value = null
  } catch (caught) {
    ui.notify(errorMessage(caught), 'danger')
  } finally {
    isActing.value = false
  }
}

async function resetTwoFactor(user: User): Promise<void> {
  isActing.value = true
  try {
    await usersApi.resetTwoFactor(user.id)
    ui.notify('Dvostepena prijava je isključena, a sve sesije korisnika su odjavljene.')
    pendingAction.value = null
    await load()
  } catch (caught) {
    ui.notify(errorMessage(caught), 'danger')
  } finally {
    isActing.value = false
  }
}

function confirmPending(): void {
  const pending = pendingAction.value
  if (!pending) return
  if (pending.action === 'remove') remove(pending.user)
  else if (pending.action === 'reset2fa') resetTwoFactor(pending.user)
  else lifecycle(pending.user, 'deactivate')
}

async function remove(user: User): Promise<void> {
  isActing.value = true
  try {
    await usersApi.remove(user.id)
    ui.notify(`Korisnik ${user.email} je trajno obrisan.`)
    pendingAction.value = null
    await load()
  } catch (caught) {
    ui.notify(errorMessage(caught), 'danger')
  } finally {
    isActing.value = false
  }
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
                <th scope="col">Korisnik</th>
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
                  <div class="subtle email">{{ user.email }}</div>
                </td>
                <td v-if="!fixedRole">{{ roleLabels[user.role] }}</td>
                <td><AppBadge :tone="kycLabels[user.kycStatus].tone">{{ kycLabels[user.kycStatus].label }}</AppBadge></td>
                <td>
                  <AppBadge v-if="user.twoFactorEnabled" tone="info" class="two-factor-badge">2FA</AppBadge>
                  <AppBadge v-if="user.isLocked" tone="danger">Zaključan</AppBadge>
                  <AppBadge v-else :tone="user.isActive ? 'success' : 'neutral'">{{ user.isActive ? 'Aktivan' : 'Neaktivan' }}</AppBadge>
                </td>
                <td class="subtle">{{ formatDate(user.createdAt) }}</td>
                <td class="actions">
                  <RouterLink v-if="user.role === 'Korisnik'" :to="`/bankar/klijenti/${user.id}`" class="detail-link">Detalji</RouterLink>
                  <AppButton variant="ghost" size="sm" icon="edit" @click="openForm(user)">Izmeni</AppButton>
                  <AppButton v-if="user.isLocked" variant="ghost" size="sm" icon="lock" :disabled="isActing" @click="lifecycle(user, 'unlock')">Otključaj</AppButton>
                  <template v-if="!isSelf(user)">
                    <AppButton v-if="user.isActive" variant="ghost" size="sm" @click="pendingAction = { user, action: 'deactivate' }">Deaktiviraj</AppButton>
                    <AppButton v-else variant="ghost" size="sm" :disabled="isActing" @click="lifecycle(user, 'activate')">Aktiviraj</AppButton>
                    <AppButton v-if="auth.isAdministrator && user.twoFactorEnabled" variant="ghost" size="sm" icon="key" @click="pendingAction = { user, action: 'reset2fa' }">Resetuj 2FA</AppButton>
                    <AppButton v-if="auth.isAdministrator" variant="ghost" size="sm" icon="trash" :aria-label="`Obriši ${user.email}`" @click="pendingAction = { user, action: 'remove' }" />
                  </template>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
        <AppPagination v-if="result" :page="result.page" :total-pages="result.totalPages" :total-count="result.totalCount" @change="page = $event" />
      </div>
    </AppCard>

    <AppModal
      :open="pendingAction !== null"
      :title="pendingAction?.action === 'remove' ? 'Trajno brisanje korisnika' : pendingAction?.action === 'reset2fa' ? 'Reset dvostepene prijave' : 'Deaktivacija naloga'"
      width="480px"
      @close="pendingAction = null"
    >
      <template v-if="pendingAction">
        <p v-if="pendingAction.action === 'deactivate'" class="muted">
          {{ pendingAction.user.firstName }} {{ pendingAction.user.lastName }} neće moći da se prijavi, a sve aktivne sesije se odmah prekidaju. Računi,
          transakcije i istorija ostaju sačuvani, a nalog se može ponovo aktivirati.
        </p>
        <p v-else-if="pendingAction.action === 'reset2fa'" class="muted">
          Koristite samo kada je korisnik izgubio telefon i rezervne kodove, posle provere identiteta nezavisnim kanalom. Dvostepena prijava za
          {{ pendingAction.user.email }} biće isključena, a sve sesije odjavljene. Korisnik dobija bezbednosno obaveštenje.
        </p>
        <div v-else class="stack">
          <p class="muted">
            Brisanje je dozvoljeno samo za nalog koji nema račune ni finansijsku istoriju, na primer nalog napravljen greškom. Za klijenta sa računima
            koristite deaktivaciju, jer banka mora da čuva podatke o poslovanju propisani broj godina.
          </p>
          <p><strong>{{ pendingAction.user.email }}</strong> biće obrisan i to se ne može poništiti.</p>
        </div>
      </template>
      <template #footer>
        <AppButton variant="ghost" @click="pendingAction = null">Odustani</AppButton>
        <AppButton
          variant="danger"
          :loading="isActing"
          @click="confirmPending"
        >
          {{ pendingAction?.action === 'remove' ? 'Obriši trajno' : pendingAction?.action === 'reset2fa' ? 'Isključi 2FA' : 'Deaktiviraj' }}
        </AppButton>
      </template>
    </AppModal>

    <UserFormModal :open="isFormOpen" :user="editing" :allow-roles="allowRoles" @close="isFormOpen = false" @saved="onSaved" />
  </div>
</template>

<style scoped>
.actions {
  min-width: 220px;
  text-align: right;
}

.email {
  overflow-wrap: anywhere;
}

.two-factor-badge {
  margin-right: 4px;
}

.detail-link {
  margin-right: var(--space-2);
  font-size: var(--font-size-sm);
  font-weight: 600;
  color: var(--color-link);
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
