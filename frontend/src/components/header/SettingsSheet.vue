<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { useSessionStore } from '../../stores/session'

const { t } = useI18n()
const session = useSessionStore()

defineEmits<{ close: [] }>()
</script>

<template>
  <section class="settings-sheet">
    <h2>{{ t('settings.title') }}</h2>
    <p v-if="session.serverPerson !== null">
      {{ t('settings.person', { name: session.serverPerson.name }) }}
    </p>
    <fieldset>
      <legend>{{ t('settings.language') }}</legend>
      <button type="button" @click="session.setLanguage('de')">
        {{ t('settings.languageGerman') }}
      </button>
      <button type="button" @click="session.setLanguage('en')">
        {{ t('settings.languageEnglish') }}
      </button>
    </fieldset>
    <button type="button" @click="$emit('close')">{{ t('review.back') }}</button>
  </section>
</template>
