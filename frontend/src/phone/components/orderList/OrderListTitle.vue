<script setup lang="ts">
defineProps<{ text: string }>()
</script>

<template>
  <h1 class="order-list-title text-body-1 font-weight-bold mb-2">{{ text }}</h1>
</template>
