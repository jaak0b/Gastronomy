<script setup lang="ts">
defineProps<{ icon: string; label: string; color?: string }>()
</script>

<template>
  <v-btn icon variant="text" :color="color">
    <v-icon :icon="icon" />
    <v-tooltip data-test="icon-action-label" activator="parent" location="top">{{ label }}</v-tooltip>
  </v-btn>
</template>
