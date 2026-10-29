<script setup lang="ts">
import BaseRefusalAlert from '../../shared/components/BaseRefusalAlert.vue'

const { refusalText = null } = defineProps<{
  position: number
  name: string
  nameTestName: string
  refusalText?: string | null
}>()
const slots = defineSlots<{
  facts?: () => unknown
  actions?: () => unknown
  default?: () => unknown
}>()
</script>

<template>
  <div class="base-list-row" :class="{ 'tinted-row': position % 2 === 1 }">
    <div class="row-line admin-row" data-test="row-line">
      <span class="row-name app-item-name" :data-test="nameTestName">{{ name }}</span>
      <slot name="facts" />
      <v-spacer v-if="slots.actions !== undefined" />
      <slot name="actions" />
    </div>
    <BaseRefusalAlert v-if="refusalText !== null" class="mb-2" :text="refusalText" />
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

.row-line {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
}
</style>
