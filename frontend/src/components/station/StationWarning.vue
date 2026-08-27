<script setup lang="ts">
import { useI18n } from 'vue-i18n'

const { t } = useI18n()
</script>

<template>
  <v-alert class="station-warning mb-4" type="warning" variant="tonal">
    <div class="text-subtitle-1">{{ t('station.warningTitle') }}</div>
    <p>{{ t('station.warningBody') }}</p>
  </v-alert>
</template>
