<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { useSessionStore } from '../../shared/stores/session'
import LanguageSwitch from '../../shared/components/LanguageSwitch.vue'

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
        <LanguageSwitch
          class="mt-2"
          :language="session.language"
          @select="session.setLanguage"
        />
      </v-card-text>
      <v-card-actions>
        <v-btn class="close" variant="text" @click="$emit('close')">{{ t('review.back') }}</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>
