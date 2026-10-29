<script setup lang="ts">
import { computed } from 'vue'
import { paintOfCategoryColour } from '../core/letteringColour'

interface PaintedCategory {
  name: string
  colourHex: string
}

const props = defineProps<{
  category: PaintedCategory
  headingLevel: 2 | 3
}>()

const headingTag = computed(() => `h${props.headingLevel}`)
</script>

<template>
  <component
    :is="headingTag"
    class="category-label app-section-title rounded"
    :style="paintOfCategoryColour(category.colourHex)"
  >
    {{ category.name }}
  </component>
</template>

<style scoped>
.category-label {
  font-weight: var(--app-category-label-weight);
  padding: var(--app-category-label-padding-y) var(--app-category-label-padding-x);
}
</style>
