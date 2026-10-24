<script setup lang="ts">
import { useTemplateRef, watch } from 'vue'

const props = defineProps<{ position: number; isSelected?: boolean }>()

const row = useTemplateRef<HTMLElement>('row')

watch(
  () => props.isSelected === true,
  (isSelected) => {
    if (isSelected) {
      row.value?.scrollIntoView({ block: 'nearest' })
    }
  },
  { flush: 'post' },
)
</script>

<template>
  <div
    ref="row"
    class="base-list-row"
    :class="{ 'tinted-row': position % 2 === 1, 'selected-row': isSelected === true }"
  >
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

.base-list-row.selected-row {
  background-color: rgba(var(--v-theme-on-surface), 0.16);
  border-radius: 6px;
}
</style>
