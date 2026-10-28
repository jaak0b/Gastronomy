<script setup lang="ts">
const { location = 'top' } = defineProps<{
  icon: string
  label: string
  color?: string
  location?: 'top' | 'bottom'
}>()
</script>

<template>
  <v-btn icon variant="text" :color="color">
    <v-icon :icon="icon" />
    <v-tooltip data-test="icon-action-label" activator="parent" :location="location">{{ label }}</v-tooltip>
  </v-btn>
</template>
