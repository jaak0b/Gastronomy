<script setup lang="ts">
import { useI18n } from 'vue-i18n'

const { t } = useI18n()
</script>

<template>
  <v-container>
    <v-alert class="not-on-laptop" type="info" variant="tonal">
      {{ t('admin.notOnLaptop') }}
    </v-alert>
  </v-container>
</template>
