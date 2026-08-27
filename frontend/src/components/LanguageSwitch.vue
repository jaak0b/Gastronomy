<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import type { AppLanguage } from '../core/apiTypes'

defineProps<{ language: AppLanguage; labelKey: string }>()
defineEmits<{ select: [language: AppLanguage] }>()

const { t } = useI18n()
</script>

<template>
  <div class="language-switch d-flex align-center ga-2">
    <span class="label text-medium-emphasis">{{ t(labelKey) }}</span>
    <v-btn-toggle :model-value="language" density="comfortable" variant="outlined" divided>
      <v-btn
        value="de"
        class="option option-de"
        :aria-pressed="language === 'de'"
        @click="$emit('select', 'de')"
      >
        {{ t('language.german') }}
      </v-btn>
      <v-btn
        value="en"
        class="option option-en"
        :aria-pressed="language === 'en'"
        @click="$emit('select', 'en')"
      >
        {{ t('language.english') }}
      </v-btn>
    </v-btn-toggle>
  </div>
</template>
