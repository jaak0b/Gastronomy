<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import type { SendFailureMessage } from '../../core/sendFailure'

defineProps<{ failure: SendFailureMessage }>()
defineEmits<{ retry: [] }>()

const { t } = useI18n()
</script>

<template>
  <section class="send-failure">
    <p class="failure-message">{{ t(failure.key) }}</p>
    <button type="button" class="retry" @click="$emit('retry')">{{ t('review.retry') }}</button>
    <p v-if="failure.paperFallbackKey !== null" class="paper-fallback">
      {{ t(failure.paperFallbackKey) }}
    </p>
  </section>
</template>
