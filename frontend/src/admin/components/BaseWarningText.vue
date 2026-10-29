<script setup lang="ts">
defineProps<{ text: string }>()
</script>

<template>
  <span class="base-warning-text app-small-text text-warning d-flex align-center ga-1">
    <v-icon icon="mdi-alert" color="warning" size="small" />
    <span>{{ text }}</span>
  </span>
</template>
