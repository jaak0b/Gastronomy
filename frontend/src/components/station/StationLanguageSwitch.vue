<script setup lang="ts">
import { useI18n } from 'vue-i18n'

defineProps<{ language: 'de' | 'en' }>()
defineEmits<{ select: [language: 'de' | 'en'] }>()

const { t } = useI18n()
</script>

<template>
  <div class="station-language">
    <span class="label">{{ t('station.language') }}</span>
    <button
      type="button"
      :class="{ 'is-selected': language === 'de' }"
      @click="$emit('select', 'de')"
    >
      {{ t('station.languageGerman') }}
    </button>
    <button
      type="button"
      :class="{ 'is-selected': language === 'en' }"
      @click="$emit('select', 'en')"
    >
      {{ t('station.languageEnglish') }}
    </button>
  </div>
</template>
