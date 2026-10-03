<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import type { SendFailureMessage } from '../../core/sendFailure'

defineProps<{ failure: SendFailureMessage }>()

const { t } = useI18n()
</script>

<template>
  <v-alert class="send-failure my-4" data-test="send-failure" type="error" variant="tonal">
    <p class="failure-message" data-test="failure-message">{{ t(failure.key, failure.parameters ?? {}) }}</p>
  </v-alert>
</template>
