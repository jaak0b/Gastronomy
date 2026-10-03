<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { startingUpMessageKey } from '../core/startingUp'
import { useSessionStore } from '../stores/session'

const { t } = useI18n()
const session = useSessionStore()

const isStillWaiting = computed(() => session.startingUpFailure === null)
const message = computed(() => {
  const key = startingUpMessageKey(session.startingUpFailure)
  return key === null ? null : t(key)
})

async function askTheLaptopAgain(): Promise<void> {
  await session.loadSession()
}
</script>

<template>
  <v-container class="starting-up" data-test="starting-up">
    <v-progress-circular
      v-if="isStillWaiting"
      class="starting-up-spinner mb-4"
      data-test="starting-up-spinner"
      indeterminate
      size="48"
    />
    <p v-if="message !== null" class="starting-up-message text-body-1" data-test="starting-up-message">{{ message }}</p>
    <v-btn
      v-if="!isStillWaiting"
      class="starting-up-try-again mt-4"
      data-test="starting-up-try-again"
      color="primary"
      block
      size="x-large"
      @click="askTheLaptopAgain"
    >
      {{ t('shared.startup.actions.tryAgain') }}
    </v-btn>
  </v-container>
</template>
