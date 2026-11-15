<script setup lang="ts">
import { computed, useId } from 'vue'

const props = defineProps<{
  label: string
  hint?: string
  error?: string | null
  required?: boolean
  type?: 'text' | 'email' | 'password' | 'number' | 'date' | 'search'
  multiline?: boolean
  rows?: number
  placeholder?: string
  autocomplete?: string
  inputmode?: 'text' | 'decimal' | 'numeric' | 'email' | 'search'
  maxlength?: number
  min?: string | number
  max?: string | number
  step?: string | number
  disabled?: boolean
  options?: { value: string | number; label: string }[]
}>()

const model = defineModel<string | number | null>()
const id = useId()
const describedBy = computed(() => [props.hint ? `${id}-hint` : null, props.error ? `${id}-error` : null].filter(Boolean).join(' ') || undefined)
</script>

<template>
  <div class="field" :class="{ 'has-error': error }">
    <label :for="id" class="field-label">
      {{ label }}
      <span v-if="required" class="required" aria-hidden="true">*</span>
    </label>
    <select
      v-if="options"
      :id="id"
      v-model="model"
      class="control"
      :required="required"
      :disabled="disabled"
      :aria-invalid="!!error || undefined"
      :aria-describedby="describedBy"
    >
      <option v-for="option in options" :key="option.value" :value="option.value">{{ option.label }}</option>
    </select>
    <textarea
      v-else-if="multiline"
      :id="id"
      v-model="model"
      class="control"
      :rows="rows ?? 4"
      :placeholder="placeholder"
      :maxlength="maxlength"
      :required="required"
      :disabled="disabled"
      :aria-invalid="!!error || undefined"
      :aria-describedby="describedBy"
    />
    <input
      v-else
      :id="id"
      v-model="model"
      class="control"
      :type="type ?? 'text'"
      :placeholder="placeholder"
      :autocomplete="autocomplete"
      :inputmode="inputmode"
      :maxlength="maxlength"
      :min="min"
      :max="max"
      :step="step"
      :required="required"
      :disabled="disabled"
      :aria-invalid="!!error || undefined"
      :aria-describedby="describedBy"
    />
    <p v-if="hint && !error" :id="`${id}-hint`" class="hint">{{ hint }}</p>
    <p v-if="error" :id="`${id}-error`" class="error" role="alert">{{ error }}</p>
  </div>
</template>

<style scoped>
.field {
  display: flex;
  flex-direction: column;
  gap: var(--space-1);
}

.field-label {
  font-size: var(--font-size-sm);
  font-weight: 600;
  color: var(--color-text);
}

.required {
  color: var(--color-primary);
  margin-left: 2px;
}

.control {
  width: 100%;
  padding: 0.7rem 0.9rem;
  border: 1px solid var(--color-border-strong);
  border-radius: var(--radius-md);
  background: var(--color-surface);
  transition:
    border-color var(--transition-fast),
    box-shadow var(--transition-fast);
}

.control:focus {
  outline: none;
  border-color: var(--color-primary);
  box-shadow: var(--focus-ring);
}

.control:disabled {
  background: var(--color-surface-muted);
  color: var(--color-text-subtle);
}

textarea.control {
  resize: vertical;
  min-height: 96px;
}

.has-error .control {
  border-color: var(--color-danger);
}

.hint {
  font-size: var(--font-size-xs);
  color: var(--color-text-subtle);
}

.error {
  font-size: var(--font-size-xs);
  color: var(--color-danger);
  font-weight: 500;
}
</style>
