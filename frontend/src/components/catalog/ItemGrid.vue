<script setup lang="ts">
import type { AppLanguage, CatalogItem } from '../../core/apiTypes'
import ItemButton from './ItemButton.vue'

defineProps<{
  items: CatalogItem[]
  language: AppLanguage
  quantityFor: (itemId: string) => number
}>()
defineEmits<{ add: [item: CatalogItem]; remove: [item: CatalogItem] }>()
</script>

<template>
  <div class="item-grid d-flex flex-column ga-2">
    <ItemButton
      v-for="item in items"
      :key="item.id"
      :item="item"
      :language="language"
      :quantity="quantityFor(item.id)"
      @add="$emit('add', item)"
      @remove="$emit('remove', item)"
    />
  </div>
</template>
