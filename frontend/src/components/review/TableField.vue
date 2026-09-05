<script setup lang="ts">
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'

defineProps<{ modelValue: string; isMissing: boolean; knownTableNames: string[] }>()
const emit = defineEmits<{ 'update:modelValue': [value: string] }>()

const { t } = useI18n()
const input = ref<HTMLElement | null>(null)

function focus(): void {
  input.value?.querySelector('input')?.focus()
}

function announce(typed: string | null): void {
  emit('update:modelValue', typed ?? '')
}

defineExpose({ focus })
</script>

<template>
  <div ref="input" class="table-field my-4" :class="{ 'is-missing': isMissing }">
    <v-combobox
      class="table-input"
      :label="t('catalog.tableName')"
      :placeholder="t('catalog.tablePlaceholder')"
      persistent-placeholder
      :error="isMissing"
      :items="knownTableNames"
      :model-value="modelValue"
      @update:model-value="announce($event)"
    />
  </div>
</template>
