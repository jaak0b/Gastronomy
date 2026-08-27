<script setup lang="ts">
import { useI18n } from 'vue-i18n'

const { t } = useI18n()
</script>

<template>
  <section class="station-warning">
    <h2>{{ t('station.warningTitle') }}</h2>
    <p>{{ t('station.warningBody') }}</p>
  </section>
</template>
