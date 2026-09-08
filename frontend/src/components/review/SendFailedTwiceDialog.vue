<script setup lang="ts">
import { useI18n } from 'vue-i18n'

const emit = defineEmits<{ writtenDown: []; tryAgain: [] }>()

const { t } = useI18n()
</script>

<template>
  <v-dialog class="send-failed-twice-dialog" model-value persistent max-width="480">
    <v-card>
      <v-card-title class="title">{{ t('review.sendFailedTwiceTitle') }}</v-card-title>
      <v-card-text>
        <p class="what-happened mb-4">{{ t('review.sendFailedTwiceUnknown') }}</p>
        <p class="write-it-down mb-4">{{ t('review.sendFailedTwiceWriteItDown') }}</p>
        <p class="check-the-tablet">{{ t('review.sendFailedTwiceCheckTheTablet') }}</p>
      </v-card-text>
      <v-card-actions>
        <v-btn
          class="written-down"
          color="primary"
          variant="flat"
          size="large"
          @click="emit('writtenDown')"
        >
          {{ t('review.sendFailedTwiceWrittenDown') }}
        </v-btn>
        <v-btn class="try-again" variant="text" size="large" @click="emit('tryAgain')">
          {{ t('review.sendFailedTwiceTryAgain') }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>
