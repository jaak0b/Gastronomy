<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import type { Invitation } from '../../../stores/admin/people'

defineProps<{ invitation: Invitation }>()
defineEmits<{ close: [] }>()

const { t } = useI18n()
</script>

<template>
  <section class="invitation-panel">
    <h2>{{ t('admin.enrol.title') }}</h2>
    <ol>
      <li>{{ t('admin.enrol.step1') }}</li>
      <li>{{ t('admin.enrol.step2') }}</li>
      <li>{{ t('admin.enrol.step3') }}</li>
    </ol>
    <p class="qr-url">{{ invitation.qrUrl }}</p>
    <p class="validity">{{ t('admin.enrol.validity') }}</p>
    <h3>{{ t('admin.enrol.cameraTitle') }}</h3>
    <p class="camera-step">
      {{ t('admin.enrol.cameraStep', { url: invitation.qrUrl, code: invitation.sixDigitCode }) }}
    </p>
    <button type="button" @click="$emit('close')">{{ t('admin.cancel') }}</button>
  </section>
</template>
