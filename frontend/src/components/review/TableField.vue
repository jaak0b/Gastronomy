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
  <div class="table-field">
    <div class="suggestions">
      <button
        v-for="suggestion in matches"
        :key="suggestion"
        type="button"
        @click="emit('update:modelValue', suggestion)"
      >
        {{ suggestion }}
      </button>
    </div>
    <label>
      <span>{{ t('review.tableLabel') }}</span>
      <input
        type="text"
        :value="modelValue"
        :placeholder="t('review.tablePlaceholder')"
        @input="emit('update:modelValue', ($event.target as HTMLInputElement).value)"
      />
    </label>
    <p class="help">{{ t('review.tableHelp') }}</p>
  </div>
</template>
