<script setup lang="ts">
import { computed } from 'vue'
import QrLanding from '../components/QrLanding.vue'
import { currentRoute } from '../router/router'

const code = computed(() => {
  const route = currentRoute.value
  return route.name === 'enrolQr' ? route.code : ''
})
</script>

<template>
  <QrLanding :code="code" />
</template>
