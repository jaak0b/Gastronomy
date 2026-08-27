<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { suggestionMatches } from '../../core/tableLabel'

const props = defineProps<{ modelValue: string; suggestions: string[] }>()
const emit = defineEmits<{ 'update:modelValue': [value: string] }>()

const { t } = useI18n()

const matches = computed(() => suggestionMatches(props.modelValue, props.suggestions))
</script>

<template>
  <div class="table-field my-4">
    <v-chip-group class="suggestions">
      <v-chip
        v-for="suggestion in matches"
        :key="suggestion"
        @click="emit('update:modelValue', suggestion)"
      >
        {{ suggestion }}
      </v-chip>
    </v-chip-group>
    <v-text-field
      class="table-input"
      :label="t('review.tableLabel')"
      :placeholder="t('review.tablePlaceholder')"
      :model-value="modelValue"
      @update:model-value="emit('update:modelValue', $event)"
    />
    <p class="help text-medium-emphasis">{{ t('review.tableHelp') }}</p>
  </div>
</template>
