<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import type { AppLanguage } from '../core/apiTypes'

defineProps<{ language: AppLanguage; labelKey: string }>()
defineEmits<{ select: [language: AppLanguage] }>()

const { t } = useI18n()
</script>

<template>
  <div class="language-switch">
    <span class="label">{{ t(labelKey) }}</span>
    <div class="options">
      <button
        type="button"
        class="option option-de"
        :class="{ 'is-selected': language === 'de' }"
        :aria-pressed="language === 'de'"
        @click="$emit('select', 'de')"
      >
        {{ t('language.german') }}
      </button>
      <button
        type="button"
        class="option option-en"
        :class="{ 'is-selected': language === 'en' }"
        :aria-pressed="language === 'en'"
        @click="$emit('select', 'en')"
      >
        {{ t('language.english') }}
      </button>
    </div>
  </div>
</template>
