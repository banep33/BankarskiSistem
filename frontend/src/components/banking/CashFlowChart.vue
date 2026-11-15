<script setup lang="ts">
import { computed, nextTick, onMounted, ref, watch } from 'vue'
import { useUiStore } from '@/stores/ui'
import { formatMoney } from '@/utils/format'

const props = defineProps<{ buckets: { label: string; income: number; expense: number }[]; currency: string }>()

const ui = useUiStore()
const height = 180
const max = computed(() => Math.max(1, ...props.buckets.flatMap((bucket) => [bucket.income, bucket.expense])))
const barWidth = computed(() => Math.max(6, Math.min(22, 320 / Math.max(1, props.buckets.length) / 2.6)))
const groupWidth = computed(() => barWidth.value * 2 + 10)
const width = computed(() => Math.max(320, props.buckets.length * groupWidth.value + 20))

const scroller = ref<HTMLElement | null>(null)
const labelStep = computed(() => Math.max(1, Math.ceil(34 / groupWidth.value)))

function showLabel(index: number): boolean {
  return (props.buckets.length - 1 - index) % labelStep.value === 0
}

async function scrollToLatest(): Promise<void> {
  await nextTick()
  if (scroller.value) scroller.value.scrollLeft = scroller.value.scrollWidth
}

watch(() => props.buckets, scrollToLatest)
onMounted(scrollToLatest)

function barHeight(value: number): number {
  return value <= 0 ? 0 : Math.max(3, (value / max.value) * (height - 30))
}

const summary = computed(() =>
  props.buckets.map((bucket) => `${bucket.label}: priliv ${formatMoney(bucket.income, props.currency)}, odliv ${formatMoney(bucket.expense, props.currency)}`).join('; '),
)
</script>

<template>
  <figure class="chart">
    <div class="legend" aria-hidden="true">
      <span><i class="dot income" /> Priliv</span>
      <span><i class="dot expense" /> Odliv</span>
    </div>
    <div ref="scroller" class="scroll">
      <svg :viewBox="`0 0 ${width} ${height + 24}`" :width="width" :height="height + 24" role="img" :aria-label="ui.hideBalances ? 'Grafikon prometa, iznosi su sakriveni' : `Grafikon prometa. ${summary}`">
        <line x1="0" :y1="height" :x2="width" :y2="height" class="axis" />
        <g v-for="(bucket, index) in buckets" :key="bucket.label" :transform="`translate(${10 + index * groupWidth}, 0)`">
          <rect :x="0" :y="height - barHeight(bucket.income)" :width="barWidth" :height="barHeight(bucket.income)" rx="4" class="bar income">
            <title>{{ ui.hideBalances ? bucket.label : `${bucket.label}, priliv ${formatMoney(bucket.income, currency)}` }}</title>
          </rect>
          <rect :x="barWidth + 3" :y="height - barHeight(bucket.expense)" :width="barWidth" :height="barHeight(bucket.expense)" rx="4" class="bar expense">
            <title>{{ ui.hideBalances ? bucket.label : `${bucket.label}, odliv ${formatMoney(bucket.expense, currency)}` }}</title>
          </rect>
          <text v-if="showLabel(index)" :x="barWidth + 1" :y="height + 17" class="label" text-anchor="middle">{{ bucket.label }}</text>
        </g>
      </svg>
    </div>
  </figure>
</template>

<style scoped>
.chart {
  margin: 0;
}

.legend {
  display: flex;
  gap: var(--space-4);
  font-size: var(--font-size-xs);
  color: var(--color-text-muted);
  margin-bottom: var(--space-3);
}

.dot {
  display: inline-block;
  width: 10px;
  height: 10px;
  border-radius: 3px;
  margin-right: 4px;
}

.scroll {
  overflow-x: auto;
}

.axis {
  stroke: var(--color-border);
}

.bar.income,
.dot.income {
  fill: var(--color-chart-income);
  background: var(--color-chart-income);
}

.bar.expense,
.dot.expense {
  fill: var(--color-chart-expense);
  background: var(--color-chart-expense);
}

.label {
  font-size: 10px;
  fill: var(--color-text-subtle);
}
</style>
