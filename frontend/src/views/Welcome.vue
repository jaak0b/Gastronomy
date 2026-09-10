<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { useSessionStore } from '../stores/session'
import LanguageSwitch from '../components/LanguageSwitch.vue'

const { t } = useI18n()
const session = useSessionStore()
</script>

<template>
  <v-container class="welcome">
    <LanguageSwitch
      :language="session.language"
      label-key="settings.language"
      @select="session.setLanguage"
    />
    <h1 class="text-h4 mt-4">{{ t('welcome.title') }}</h1>
    <v-alert v-if="session.heldDraftExists()" class="order-held mt-4" type="info" variant="tonal">
      {{ t('enrol.orderHeld') }}
    </v-alert>
  </v-container>
</template>
