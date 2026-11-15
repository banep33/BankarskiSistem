<script setup lang="ts">
import AppBadge from '@/components/ui/AppBadge.vue'
import { complaintStatusLabels } from '@/utils/labels'
import { formatDateTime } from '@/utils/format'
import type { ComplaintDetail, ComplaintStatus } from '@/types/api'

defineProps<{ complaint: ComplaintDetail; showInternal?: boolean }>()

function statusLabel(status: string | null): string {
  if (!status) return 'Novo'
  return complaintStatusLabels[status as ComplaintStatus]?.label ?? status
}
</script>

<template>
  <div class="thread">
    <article class="message customer original">
      <header>
        <strong>{{ complaint.summary.customerName ?? 'Klijent' }}</strong>
        <span class="subtle">{{ formatDateTime(complaint.summary.createdAt) }}</span>
      </header>
      <p>{{ complaint.description }}</p>
    </article>
    <template v-for="message in complaint.messages" :key="message.id">
      <article v-if="showInternal || !message.isInternalNote" class="message" :class="{ staff: message.isFromStaff, customer: !message.isFromStaff, internal: message.isInternalNote }">
        <header>
          <strong>{{ message.authorName }}</strong>
          <AppBadge v-if="message.isInternalNote" tone="warning">Interna beleška</AppBadge>
          <AppBadge v-else-if="message.isFromStaff" tone="info">Banka</AppBadge>
          <span class="subtle">{{ formatDateTime(message.createdAt) }}</span>
        </header>
        <p>{{ message.message }}</p>
      </article>
    </template>
    <article v-if="complaint.resolution" class="message resolution">
      <header>
        <strong>Odgovor banke</strong>
        <span class="subtle">{{ formatDateTime(complaint.resolvedAt) }}</span>
      </header>
      <p>{{ complaint.resolution }}</p>
    </article>

    <details class="history">
      <summary>Istorija statusa ({{ complaint.history.length }})</summary>
      <ol>
        <li v-for="(entry, index) in complaint.history" :key="index">
          <span class="subtle">{{ formatDateTime(entry.changedAt) }}</span>
          <span>{{ statusLabel(entry.oldStatus) }} → <strong>{{ statusLabel(entry.newStatus) }}</strong></span>
          <span v-if="entry.note" class="muted">{{ entry.note }}</span>
        </li>
      </ol>
    </details>
  </div>
</template>

<style scoped>
.thread {
  display: flex;
  flex-direction: column;
  gap: var(--space-3);
}

.message {
  max-width: 85%;
  padding: var(--space-4);
  border-radius: var(--radius-lg);
  background: var(--color-surface-muted);
  border: 1px solid var(--color-border);
}

.message header {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: var(--space-2);
  margin-bottom: var(--space-2);
  font-size: var(--font-size-sm);
}

.message p {
  white-space: pre-wrap;
  word-break: break-word;
}

.message.customer {
  align-self: flex-start;
}

.message.staff {
  align-self: flex-end;
  background: var(--color-cream);
  border-color: transparent;
}

.message.internal {
  background: var(--color-warning-soft);
  border-style: dashed;
}

.message.resolution {
  max-width: 100%;
  background: var(--color-success-soft);
  border-color: transparent;
}

.history {
  margin-top: var(--space-3);
  font-size: var(--font-size-sm);
}

.history summary {
  cursor: pointer;
  font-weight: 600;
}

.history ol {
  margin: var(--space-3) 0 0;
  padding-left: var(--space-5);
  display: flex;
  flex-direction: column;
  gap: var(--space-2);
}

.history li {
  display: flex;
  flex-direction: column;
}
</style>
