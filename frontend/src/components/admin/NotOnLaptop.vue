<script setup lang="ts">
import { useI18n } from 'vue-i18n'

const { t } = useI18n()
const laptopUrl = `${window.location.protocol}//${window.location.hostname}:5000/admin`
</script>

<template>
  <p class="not-on-laptop">{{ t('admin.notOnLaptop', { url: laptopUrl }) }}</p>
</template>
