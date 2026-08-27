<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import type { SendFailureMessage } from '../../core/sendFailure'

defineProps<{ failure: SendFailureMessage }>()
defineEmits<{ retry: [] }>()

const { t } = useI18n()
</script>

<template>
  <v-alert class="send-failure my-4" type="error" variant="tonal">
    <p class="failure-message">{{ t(failure.key) }}</p>
    <p v-if="failure.paperFallbackKey !== null" class="paper-fallback mt-2">
      {{ t(failure.paperFallbackKey) }}
    </p>
    <v-btn class="retry mt-2" color="error" @click="$emit('retry')">{{ t('review.retry') }}</v-btn>
  </v-alert>
</template>
