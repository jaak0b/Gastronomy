<script setup lang="ts">
import { useI18n } from 'vue-i18n'

const { t } = useI18n()

defineProps<{ modelValue: string }>()
defineEmits<{ 'update:modelValue': [value: string] }>()
</script>

<template>
  <v-text-field
    class="name-field"
    maxlength="40"
    :label="t('enrol.nameLabel')"
    autocomplete="name"
    :model-value="modelValue"
    @update:model-value="$emit('update:modelValue', $event)"
  />
</template>
