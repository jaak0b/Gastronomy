<script setup lang="ts">
import { CatalogItemView } from '../../../shared/api/generatedSchemas'
import type { AppLanguage } from '../../../shared/core/deviceLanguage'
import { stripeClassesOf } from '../../../shared/core/stripedRows'
import type { EstimateRange } from '../../core/estimates'
import type { ItemPosition } from '../../core/itemPositions'
import ItemRow from './ItemRow.vue'

defineProps<{
  items: CatalogItemView[]
  language: AppLanguage
  positionsFor: (itemId: string) => ItemPosition[]
  estimateRangeFor: (itemId: string) => EstimateRange | null
  stationNameFor: (stationId: string) => string
}>()
defineEmits<{
  add: [item: CatalogItemView]
  addWithANote: [item: CatalogItemView, note: string]
  addWithANoteAtAStation: [item: CatalogItemView]
  addLikeGroup: [item: CatalogItemView, note: string | null, stationId: string | null]
  removeOne: [index: number]
  renameNote: [indexes: number[], note: string]
  changeStation: [indexes: number[]]
}>()
</script>

<template>
  <div class="item-grid rounded border">
    <ItemRow
      v-for="(item, position) in items"
      :key="item.id"
      :class="stripeClassesOf(position)"
      :item="item"
      :language="language"
      :positions="positionsFor(item.id)"
      :estimate-range="estimateRangeFor(item.id)"
      :station-name-for="stationNameFor"
      @add="$emit('add', item)"
      @add-with-a-note="(note) => $emit('addWithANote', item, note)"
      @add-with-a-note-at-a-station="$emit('addWithANoteAtAStation', item)"
      @add-like-group="(note, stationId) => $emit('addLikeGroup', item, note, stationId)"
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
</style>
