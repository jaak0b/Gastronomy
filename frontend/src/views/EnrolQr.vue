<script setup lang="ts">
import { computed } from 'vue'
import QrLanding from '../components/enrolment/QrLanding.vue'
import { currentRoute } from '../router'

const code = computed(() => {
  const route = currentRoute.value
  return route.name === 'enrolQr' ? route.code : ''
})
</script>

<template>
  <QrLanding :code="code" />
</template>
