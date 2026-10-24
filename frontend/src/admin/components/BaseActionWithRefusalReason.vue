<script setup lang="ts">
defineProps<{ refusalReason: string | null }>()
</script>

<template>
  <span class="base-action-with-refusal-reason">
    <slot :refused="refusalReason !== null" />
    <v-tooltip
      data-test="refusal-reason"
      activator="parent"
      location="top"
      :disabled="refusalReason === null"
    >
      {{ refusalReason }}
    </v-tooltip>
  </span>
</template>

<style scoped>
.base-action-with-refusal-reason {
  display: inline-flex;
}
</style>
