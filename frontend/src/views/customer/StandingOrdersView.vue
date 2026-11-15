<script setup lang="ts">
import { computed, onMounted } from 'vue'
import { RouterLink } from 'vue-router'
import AppCard from '@/components/ui/AppCard.vue'
import AppBadge from '@/components/ui/AppBadge.vue'
import AppIcon from '@/components/ui/AppIcon.vue'
import MoneyAmount from '@/components/ui/MoneyAmount.vue'
import StatusViews from '@/components/ui/StatusViews.vue'
import { standingOrdersApi } from '@/api/bank'
import { useAsync } from '@/composables/useAsync'
import { frequencyLabels, standingOrderStatusLabels } from '@/utils/labels'
import { formatAccountNumber, formatDate } from '@/utils/format'

const { data: orders, isLoading, error, load } = useAsync(() => standingOrdersApi.list(), [])

const active = computed(() => orders.value.filter((order) => order.status === 'Aktivan' || order.status === 'Pauziran'))
const closed = computed(() => orders.value.filter((order) => order.status === 'Zavrsen' || order.status === 'Otkazan'))
const monthlyTotal = computed(() =>
  active.value
    .filter((order) => order.status === 'Aktivan' && order.currency === 'RSD')
    .reduce((sum, order) => {
      const perMonth = order.frequency === 'Nedeljno' ? (order.amount * 52) / 12 : order.frequency === 'Mesecno' ? order.amount : order.frequency === 'Kvartalno' ? order.amount / 3 : order.amount / 12
      return sum + perMonth
    }, 0),
)

onMounted(load)
</script>

<template>
  <div class="page">
    <header class="page-header">
      <div>
        <h1 class="page-title">Trajni nalozi</h1>
        <p class="page-subtitle">Plaćanja koja banka automatski izvršava u zadato vreme, na primer stanarina ili rata kredita.</p>
      </div>
      <RouterLink to="/trajni-nalozi/novi" class="new-link"><AppIcon name="plus" :size="18" /> Novi trajni nalog</RouterLink>
    </header>

    <StatusViews v-if="isLoading" state="loading" :rows="4" />
    <StatusViews v-else-if="error" state="error" :message="error" @retry="load" />
    <StatusViews
      v-else-if="!orders.length"
      state="empty"
      icon="repeat"
      title="Nemate trajne naloge"
      message="Napravite trajni nalog za račune koji se plaćaju redovno i ne morate da mislite na rokove."
    />

    <template v-else>
      <div class="summary">
        <AppCard tone="dark">
          <p class="label">Aktivnih naloga</p>
          <p class="value">{{ active.filter((order) => order.status === 'Aktivan').length }}</p>
        </AppCard>
        <AppCard>
          <p class="label muted">Prosečno mesečno (RSD)</p>
          <MoneyAmount class="value" :amount="monthlyTotal" currency="RSD" />
        </AppCard>
        <AppCard tone="cream">
          <p class="note"><AppIcon name="info" :size="16" /> Nalog se izvršava na dan dospeća. Ako tada nema dovoljno sredstava, dobijate obaveštenje, a posle 3 neuspeha zaredom nalog se pauzira.</p>
        </AppCard>
      </div>

      <div class="grid-2">
        <RouterLink v-for="order in active" :key="order.id" :to="`/trajni-nalozi/${order.id}`" class="order-card" :class="{ paused: order.status === 'Pauziran' }">
          <div class="row-between">
            <span class="recipient">{{ order.recipientName }}</span>
            <AppBadge :tone="standingOrderStatusLabels[order.status].tone">{{ standingOrderStatusLabels[order.status].label }}</AppBadge>
          </div>
          <p class="subtle">{{ order.purpose }} · {{ frequencyLabels[order.frequency].label }}</p>
          <MoneyAmount class="amount" :amount="order.amount" :currency="order.currency" />
          <div class="row-between meta">
            <span>
              <template v-if="order.nextExecutionDate">Sledeće plaćanje {{ formatDate(order.nextExecutionDate) }}</template>
            </span>
            <span class="mono subtle">{{ order.fromAccountNumber ? formatAccountNumber(order.fromAccountNumber) : '' }}</span>
          </div>
          <p v-if="order.consecutiveFailures > 0" class="failure"><AppIcon name="alert" :size="14" /> {{ order.lastFailureReason }}</p>
        </RouterLink>
      </div>

      <AppCard v-if="closed.length" title="Završeni i otkazani" :padded="false">
        <div class="card-body">
          <div class="table-scroll">
            <table class="data-table">
              <thead>
                <tr>
                  <th scope="col">Primalac</th>
                  <th scope="col">Učestalost</th>
                  <th scope="col">Izvršeno puta</th>
                  <th scope="col">Status</th>
                  <th scope="col" class="numeric">Iznos</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="order in closed" :key="order.id">
                  <td><RouterLink :to="`/trajni-nalozi/${order.id}`">{{ order.recipientName }}</RouterLink></td>
                  <td>{{ frequencyLabels[order.frequency].label }}</td>
                  <td>{{ order.executionsCount }}</td>
                  <td><AppBadge :tone="standingOrderStatusLabels[order.status].tone">{{ standingOrderStatusLabels[order.status].label }}</AppBadge></td>
                  <td class="numeric"><MoneyAmount :amount="order.amount" :currency="order.currency" /></td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </AppCard>
    </template>
  </div>
</template>

<style scoped>
.new-link {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 0.65rem 1.2rem;
  border-radius: var(--radius-pill);
  background: var(--color-primary);
  color: #fff;
  font-weight: 600;
  text-decoration: none;
}

.summary {
  display: grid;
  grid-template-columns: 1fr 1fr 2fr;
  gap: var(--space-5);
}

.label {
  font-size: var(--font-size-sm);
  opacity: 0.8;
}

.value {
  display: block;
  margin-top: var(--space-2);
  font-size: var(--font-size-2xl);
  font-weight: 600;
}

.note {
  display: flex;
  gap: var(--space-2);
  font-size: var(--font-size-sm);
}

.order-card {
  display: flex;
  flex-direction: column;
  gap: var(--space-2);
  padding: var(--space-5);
  border-radius: var(--radius-lg);
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  box-shadow: var(--shadow-sm);
  text-decoration: none;
  transition: box-shadow var(--transition-fast);
}

.order-card:hover {
  box-shadow: var(--shadow-md);
}

.order-card.paused {
  border-style: dashed;
}

.recipient {
  font-weight: 700;
  font-size: var(--font-size-lg);
}

.amount {
  font-size: var(--font-size-xl);
  font-weight: 600;
}

.meta {
  font-size: var(--font-size-sm);
}

.failure {
  display: flex;
  gap: 6px;
  align-items: center;
  font-size: var(--font-size-xs);
  color: var(--color-danger);
}

.card-body {
  padding: var(--space-5) var(--space-6);
}

.numeric {
  text-align: right;
}

@media (max-width: 960px) {
  .summary {
    grid-template-columns: minmax(0, 1fr);
  }
}
</style>
