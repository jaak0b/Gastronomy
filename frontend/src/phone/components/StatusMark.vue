<script setup lang="ts">
defineProps<{
  isDone: boolean
  testName: string
  testId?: string
}>()
</script>

<template>
  <v-icon
    :color="isDone ? 'success' : 'error'"
    :icon="isDone ? 'mdi-check' : 'mdi-close'"
    :data-test="testName"
    :data-test-id="testId"
  />
</template>
