<script setup lang="ts">
import { computed } from 'vue'
import AppCard from '@/components/ui/AppCard.vue'
import MoneyAmount from '@/components/ui/MoneyAmount.vue'
import { formatNumber, pluralSr } from '@/utils/format'
import type { SystemOverview } from '@/types/api'

const props = defineProps<{ overview: SystemOverview }>()

const maxActivity = computed(() => Math.max(1, ...props.overview.activityLast7Days.map((day) => day.deposits + day.withdrawals + day.transfers)))

function dayLabel(value: string): string {
  return new Intl.DateTimeFormat('sr-Latn-RS', { weekday: 'short', day: '2-digit' }).format(new Date(value))
}
</script>

<template>
  <div class="stack">
    <div class="grid-4">
      <AppCard tone="dark">
        <p class="label">Transakcije danas</p>
        <p class="value">{{ formatNumber(overview.transactionsToday) }}</p>
        <p v-for="item in overview.volumeTodayByCurrency" :key="item.currency" class="sub">
          <MoneyAmount :amount="item.amount" :currency="item.currency" :sensitive="false" />
        </p>
      </AppCard>
      <AppCard>
        <p class="label">Klijenti</p>
        <p class="value">{{ formatNumber(overview.customers) }}</p>
        <p class="sub muted">{{ overview.activeUsers }} {{ pluralSr(overview.activeUsers, 'aktivan korisnik', 'aktivna korisnika', 'aktivnih korisnika') }} ukupno</p>
      </AppCard>
      <AppCard>
        <p class="label">Računi</p>
        <p class="value">{{ formatNumber(overview.totalAccounts) }}</p>
        <p class="sub muted">{{ overview.activeAccounts }} {{ pluralSr(overview.activeAccounts, 'aktivan', 'aktivna', 'aktivnih') }}, {{ overview.blockedAccounts }} {{ pluralSr(overview.blockedAccounts, 'blokiran', 'blokirana', 'blokiranih') }}</p>
      </AppCard>
      <AppCard :tone="overview.failedLoginsLast24Hours > 20 ? 'cream' : 'default'">
        <p class="label">Neuspešne prijave (24 h)</p>
        <p class="value">{{ formatNumber(overview.failedLoginsLast24Hours) }}</p>
        <p class="sub muted">{{ overview.lockedUsers }} {{ pluralSr(overview.lockedUsers, 'zaključan nalog', 'zaključana naloga', 'zaključanih naloga') }}</p>
      </AppCard>
    </div>

    <div class="grid-2 align-start">
      <AppCard title="Aktivnost u poslednjih 7 dana">
        <div class="legend" aria-hidden="true">
          <span><i class="dot deposits" /> Uplate</span>
          <span><i class="dot withdrawals" /> Isplate</span>
          <span><i class="dot transfers" /> Plaćanja</span>
        </div>
        <ul class="activity">
          <li v-for="day in overview.activityLast7Days" :key="day.day">
            <span class="day">{{ dayLabel(day.day) }}</span>
            <span class="bar" :aria-label="`${dayLabel(day.day)}: uplate ${day.deposits}, isplate ${day.withdrawals}, plaćanja ${day.transfers}`">
              <span class="segment deposits" :style="{ width: `${(day.deposits / maxActivity) * 100}%` }" />
              <span class="segment withdrawals" :style="{ width: `${(day.withdrawals / maxActivity) * 100}%` }" />
              <span class="segment transfers" :style="{ width: `${(day.transfers / maxActivity) * 100}%` }" />
            </span>
            <span class="count">{{ day.deposits + day.withdrawals + day.transfers }}</span>
          </li>
        </ul>
      </AppCard>
      <AppCard title="Depoziti po valutama">
        <ul class="deposits-list">
          <li v-for="item in overview.depositsByCurrency" :key="item.currency">
            <span class="currency">{{ item.currency }}</span>
            <MoneyAmount :amount="item.amount" :currency="item.currency" :sensitive="false" />
          </li>
        </ul>
        <div class="roles">
          <span>Bankari: <strong>{{ overview.bankers }}</strong></span>
          <span>Administratori: <strong>{{ overview.administrators }}</strong></span>
        </div>
      </AppCard>
    </div>
  </div>
</template>

<style scoped>
.label {
  font-size: var(--font-size-sm);
  opacity: 0.75;
}

.value {
  margin: var(--space-2) 0 var(--space-1);
  font-size: var(--font-size-3xl);
  font-weight: 600;
  letter-spacing: -0.03em;
}

.sub {
  font-size: var(--font-size-sm);
}

.align-start {
  align-items: start;
}

.legend {
  display: flex;
  gap: var(--space-4);
  margin-bottom: var(--space-4);
  font-size: var(--font-size-xs);
  color: var(--color-text-muted);
}

.dot {
  display: inline-block;
  width: 10px;
  height: 10px;
  margin-right: 4px;
  border-radius: 3px;
}

.deposits {
  background: var(--color-forest-700);
}

.withdrawals {
  background: var(--color-coral-500);
}

.transfers {
  background: var(--color-lime-500);
}

.activity {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: var(--space-3);
}

.activity li {
  display: grid;
  grid-template-columns: 64px 1fr 40px;
  align-items: center;
  gap: var(--space-3);
  font-size: var(--font-size-sm);
}

.day {
  color: var(--color-text-muted);
}

.bar {
  display: flex;
  height: 12px;
  border-radius: var(--radius-pill);
  overflow: hidden;
  background: var(--color-surface-muted);
}

.count {
  text-align: right;
  font-weight: 600;
}

.deposits-list {
  list-style: none;
  margin: 0;
  padding: 0;
}

.deposits-list li {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: var(--space-3) 0;
  border-bottom: 1px solid var(--color-border);
  font-weight: 600;
}

.currency {
  padding: 0.2rem 0.6rem;
  border-radius: var(--radius-pill);
  background: var(--color-cream);
  font-size: var(--font-size-xs);
}

.roles {
  display: flex;
  gap: var(--space-5);
  margin-top: var(--space-4);
  font-size: var(--font-size-sm);
  color: var(--color-text-muted);
}
</style>
