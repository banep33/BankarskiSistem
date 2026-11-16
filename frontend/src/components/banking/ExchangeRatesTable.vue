<script setup lang="ts">
import { formatDateTime, formatNumber } from '@/utils/format'
import type { ExchangeRate } from '@/types/api'

defineProps<{ rates: ExchangeRate[] }>()
</script>

<template>
  <div class="table-scroll">
    <table class="data-table rates">
      <thead>
        <tr>
          <th scope="col">Valuta</th>
          <th scope="col" class="numeric">Kupovni</th>
          <th scope="col" class="numeric">Srednji</th>
          <th scope="col" class="numeric">Prodajni</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="rate in rates" :key="rate.id">
          <td><strong>{{ rate.currency }}</strong></td>
          <td class="numeric mono">{{ formatNumber(rate.buyRate, 4) }}</td>
          <td class="numeric mono">{{ formatNumber(rate.middleRate, 4) }}</td>
          <td class="numeric mono">{{ formatNumber(rate.sellRate, 4) }}</td>
        </tr>
      </tbody>
    </table>
    <p v-if="rates.length" class="subtle note">
      Dinara za 1 jedinicu valute. Važi od {{ formatDateTime(rates[0]!.validFrom) }}. Kupovni kurs važi kada prodajete devize banci, a prodajni kada ih kupujete. Kursna lista je simulirana.
    </p>
  </div>
</template>

<style scoped>

.note {
  margin-top: var(--space-3);
}
</style>
