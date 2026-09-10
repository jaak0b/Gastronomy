<script setup lang="ts">
import { useI18n } from 'vue-i18n'

const emit = defineEmits<{ writtenDown: []; tryAgain: [] }>()

const { t } = useI18n()
</script>

<template>
  <v-dialog class="send-failed-twice-dialog" model-value persistent fullscreen>
    <v-card class="card d-flex flex-column">
      <v-card-title class="title">{{ t('review.sendFailedTwiceTitle') }}</v-card-title>
      <v-card-text class="body flex-grow-1">
        <p class="what-happened mb-4">{{ t('review.sendFailedTwiceUnknown') }}</p>
        <p class="write-it-down">{{ t('review.sendFailedTwiceWriteItDown') }}</p>
      </v-card-text>
      <v-card-actions class="actions flex-column align-stretch">
        <v-btn
          class="written-down"
          color="primary"
          variant="flat"
          size="large"
          @click="emit('writtenDown')"
        >
          {{ t('review.sendFailedTwiceWrittenDown') }}
        </v-btn>
        <v-btn class="try-again" variant="outlined" size="large" @click="emit('tryAgain')">
          {{ t('review.sendFailedTwiceTryAgain') }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<style scoped>
.card {
  height: 100%;
  border-radius: 0;
}

.body {
  font-size: 1.05rem;
  line-height: 1.5;
}

.title {
  white-space: normal;
  overflow: visible;
  text-overflow: clip;
  line-height: 1.35;
  padding-block: 1rem;
}

.actions {
  padding: 0 1rem 1rem;
}

.actions .v-btn {
  width: 100%;
  min-width: 0;
  height: auto;
  min-height: 3.5rem;
  padding-block: 0.75rem;
  text-transform: none;
  letter-spacing: normal;
}

.actions .v-btn :deep(.v-btn__content) {
  white-space: normal;
}
</style>
