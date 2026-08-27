<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import type { Invitation } from '../../../stores/admin/people'

defineProps<{ invitation: Invitation }>()
defineEmits<{ close: [] }>()

const { t } = useI18n()
const QR_IMAGE_URL = '/api/admin/enrolment/invitations/current/qr.svg'
</script>

<template>
  <v-card class="invitation-panel mt-4">
    <v-card-title>{{ t('admin.enrol.title') }}</v-card-title>
    <v-card-text>
      <img class="qr-image" :src="QR_IMAGE_URL" :alt="t('admin.enrol.step1')" />
      <ol class="ms-4">
        <li>{{ t('admin.enrol.step1') }}</li>
        <li>{{ t('admin.enrol.step2') }}</li>
        <li>{{ t('admin.enrol.step3') }}</li>
      </ol>
      <p class="validity text-medium-emphasis mt-2">{{ t('admin.enrol.validity') }}</p>
      <div class="text-subtitle-1 mt-4">{{ t('admin.enrol.cameraTitle') }}</div>
      <p class="six-digit-code text-h5">{{ invitation.sixDigitCode }}</p>
      <p class="camera-step">
        {{ t('admin.enrol.cameraStep', { url: invitation.qrUrl, code: invitation.sixDigitCode }) }}
      </p>
      <p class="qr-url">{{ invitation.qrUrl }}</p>
    </v-card-text>
    <v-card-actions>
      <v-btn variant="text" @click="$emit('close')">{{ t('admin.cancel') }}</v-btn>
    </v-card-actions>
  </v-card>
</template>
