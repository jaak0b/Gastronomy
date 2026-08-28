<script setup lang="ts">
import type { AppLanguage, CatalogItem } from '../../core/apiTypes'
import type { ItemPosition } from '../../core/itemPositions'
import ItemRow from './ItemRow.vue'

defineProps<{
  items: CatalogItem[]
  language: AppLanguage
  positionsFor: (itemId: string) => ItemPosition[]
}>()
defineEmits<{
  add: [item: CatalogItem]
  addWithANote: [item: CatalogItem, note: string]
  removeOne: [index: number]
  renameNote: [indexes: number[], note: string]
  changeStation: [indexes: number[]]
}>()
</script>

<template>
  <div class="item-grid rounded border">
    <ItemRow
      v-for="item in items"
      :key="item.id"
      :item="item"
      :language="language"
      :positions="positionsFor(item.id)"
      @add="$emit('add', item)"
      @add-with-a-note="(note) => $emit('addWithANote', item, note)"
      @remove-one="(index) => $emit('removeOne', index)"
      @rename-note="(indexes, note) => $emit('renameNote', indexes, note)"
      @change-station="(indexes) => $emit('changeStation', indexes)"
    />
  </div>
</template>

<style scoped>
.item-grid {
  overflow: hidden;
}

.item-grid :deep(.item-row:nth-child(even)) {
  background-color: rgba(var(--v-theme-on-surface), 0.05);
}
</style>
