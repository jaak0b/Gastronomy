<script setup lang="ts">
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'

defineProps<{ modelValue: string; isMissing: boolean }>()
const emit = defineEmits<{ 'update:modelValue': [value: string] }>()

const { t } = useI18n()
const input = ref<HTMLElement | null>(null)

function focus(): void {
  input.value?.querySelector('input')?.focus()
}

defineExpose({ focus })
</script>

<template>
  <div ref="input" class="table-field my-4" :class="{ 'is-missing': isMissing }">
    <v-text-field
      class="table-input"
      :label="t('catalog.tableName')"
      :placeholder="t('catalog.tablePlaceholder')"
      persistent-placeholder
      :error="isMissing"
      :model-value="modelValue"
      @update:model-value="emit('update:modelValue', $event)"
    />
  </div>
</template>
