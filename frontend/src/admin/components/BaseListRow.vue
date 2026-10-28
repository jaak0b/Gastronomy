<script setup lang="ts">
defineProps<{ position: number }>()
</script>

<template>
  <div class="base-list-row" :class="{ 'tinted-row': position % 2 === 1 }">
    <slot />
  </div>
</template>

<style scoped>
.base-list-row + .base-list-row {
  border-top: 1px solid rgb(var(--v-border-color), var(--v-border-opacity));
}

.base-list-row.tinted-row {
  background-color: rgba(var(--v-theme-on-surface), 0.08);
  border-radius: 6px;
}
</style>
