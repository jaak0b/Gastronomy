<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { useSessionStore } from '../../stores/session'

const { t } = useI18n()
const session = useSessionStore()

defineEmits<{ close: [] }>()
</script>

<template>
  <v-dialog :model-value="true" max-width="480" @update:model-value="$emit('close')">
    <v-card class="settings-sheet">
      <v-card-title>{{ t('settings.title') }}</v-card-title>
      <v-card-text>
        <p v-if="session.staffMember !== null">
          {{ t('settings.person', { name: session.staffMember.name }) }}
        </p>
        <div class="text-subtitle-2 mt-4">{{ t('settings.language') }}</div>
        <v-btn class="language-de me-2" variant="tonal" @click="session.setLanguage('de')">
          {{ t('settings.languageGerman') }}
        </v-btn>
        <v-btn class="language-en" variant="tonal" @click="session.setLanguage('en')">
          {{ t('settings.languageEnglish') }}
        </v-btn>
      </v-card-text>
      <v-card-actions>
        <v-btn class="close" variant="text" @click="$emit('close')">{{ t('review.back') }}</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>
