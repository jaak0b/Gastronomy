<script setup lang="ts">
import { useI18n } from 'vue-i18n'

defineProps<{ modelValue: string }>()
const emit = defineEmits<{ 'update:modelValue': [value: string] }>()

const { t } = useI18n()
</script>

<template>
  <div class="table-field my-4">
    <v-text-field
      class="table-input"
      :label="t('review.tableName')"
      :placeholder="t('review.tablePlaceholder')"
      persistent-placeholder
      :model-value="modelValue"
      @update:model-value="emit('update:modelValue', $event)"
    />
    <p class="help text-medium-emphasis">{{ t('review.tableHelp') }}</p>
  </div>
</template>
