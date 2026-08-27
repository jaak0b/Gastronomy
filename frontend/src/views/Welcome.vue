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
  <section class="welcome">
    <LanguageSwitch
      :language="session.language"
      label-key="settings.language"
      @select="session.setLanguage"
    />
    <h1>{{ t('welcome.title') }}</h1>
    <p class="welcome-body">{{ t('welcome.body') }}</p>
    <p v-if="session.heldDraftExists" class="order-held">{{ t('enrol.orderHeld') }}</p>
    <button
      v-if="!codeEntryIsOpen"
      type="button"
      class="open-code-entry"
      @click="codeEntryIsOpen = true"
    >
      {{ t('welcome.enterCode') }}
    </button>
    <SixDigitFallback v-else :show-language-switch="false" />
  </section>
</template>
