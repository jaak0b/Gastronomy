<script setup lang="ts">
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useSessionStore } from '../stores/session'
import LanguageSwitch from '../components/LanguageSwitch.vue'
import SixDigitFallback from '../components/enrolment/SixDigitFallback.vue'

const { t } = useI18n()
const session = useSessionStore()
const codeEntryIsOpen = ref(false)
</script>

<template>
  <v-container class="welcome">
    <LanguageSwitch
      :language="session.language"
      label-key="settings.language"
      @select="session.setLanguage"
    />
    <h1 class="text-h4 mt-4">{{ t('welcome.title') }}</h1>
    <p class="welcome-body text-body-1 mt-2">{{ t('welcome.body') }}</p>
    <v-alert v-if="session.heldDraftExists" class="order-held mt-4" type="info" variant="tonal">
      {{ t('enrol.orderHeld') }}
    </v-alert>
    <v-btn
      v-if="!codeEntryIsOpen"
      class="open-code-entry mt-4"
      color="primary"
      block
      @click="codeEntryIsOpen = true"
    >
      {{ t('welcome.enterCode') }}
    </v-btn>
    <SixDigitFallback v-else :show-language-switch="false" />
  </v-container>
</template>
