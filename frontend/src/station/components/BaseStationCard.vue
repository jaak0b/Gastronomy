<script setup lang="ts">
defineProps<{ modeColour: string }>()
</script>

<template>
  <div class="station-card" :style="{ borderColor: modeColour }">
    <slot />
  </div>
</template>

<style scoped>
.station-card {
  border-width: 3px;
  border-style: solid;
  border-radius: 0.5rem;
  box-sizing: border-box;
  min-width: 0;
  min-inline-size: 0;
  max-width: 100%;
  padding: 0.75rem;
}

.station-card :deep(.station-order-head) {
  min-width: 0;
}

.station-card :deep(.station-order-head > span) {
  min-width: 0;
}

.station-card :deep(.station-order-head .table-name) {
  overflow-wrap: anywhere;
}

.station-card :deep(.station-order-mode-row) {
  min-width: 0;
}

.station-card :deep(.station-order-mode-row .delivery-mode) {
  min-width: 0;
  overflow-wrap: anywhere;
}
</style>
