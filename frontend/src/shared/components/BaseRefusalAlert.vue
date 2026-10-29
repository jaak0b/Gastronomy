<script setup lang="ts">
defineProps<{ text: string }>()
</script>

<template>
  <v-alert class="base-refusal-alert refusal" data-test="refusal" type="warning" variant="tonal">
    {{ text }}
  </v-alert>
</template>
