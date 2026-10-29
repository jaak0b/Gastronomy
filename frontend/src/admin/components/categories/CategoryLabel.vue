<script setup lang="ts">
import { computed } from 'vue'
import type { AdminCategoryView } from '../../../shared/api/generatedSchemas'
import { letteringColourOn } from '../../../shared/core/letteringColour'

const props = defineProps<{
  category: AdminCategoryView
  headingLevel: 2 | 3
}>()

const headingTag = computed(() => `h${props.headingLevel}`)

const paint = computed(() => ({
  backgroundColor: props.category.colourHex,
  color: letteringColourOn(props.category.colourHex),
}))
</script>

<template>
  <component
    :is="headingTag"
    class="category-label app-section-title font-weight-bold px-3 py-1 rounded"
    :style="paint"
  >
    {{ category.name }}
  </component>
</template>
