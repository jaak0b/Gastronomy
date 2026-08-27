<script setup lang="ts">
import { useI18n } from 'vue-i18n'

const { t } = useI18n()
const laptopUrl = `${window.location.protocol}//${window.location.hostname}:5000/admin`
</script>

<template>
  <v-container>
    <v-alert class="not-on-laptop" type="info" variant="tonal">
      {{ t('admin.notOnLaptop', { url: laptopUrl }) }}
    </v-alert>
  </v-container>
</template>
