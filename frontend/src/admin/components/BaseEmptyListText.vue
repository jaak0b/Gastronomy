<script setup lang="ts">
defineProps<{ text: string }>()
</script>

<template>
  <p class="base-empty-list-text none-yet text-medium-emphasis" data-test="none-yet">{{ text }}</p>
</template>
