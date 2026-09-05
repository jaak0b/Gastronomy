<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type { CatalogItem } from '../core/apiTypes'
import { portionsOfCategory } from '../core/categoryPortions'
import { positionsForItem, type ItemPosition } from '../core/itemPositions'
import { needsStationChoice } from '../core/routingPreview'
import { isTableNameValid } from '../core/tableName'
import { useCatalogStore } from '../stores/catalog'
import { useOpenItemsStore } from '../stores/openItems'
import { useOrderStore } from '../stores/order'
import { useSessionStore } from '../stores/session'
import { navigate } from '../router'
import CategoryTabs from '../components/catalog/CategoryTabs.vue'
import ItemGrid from '../components/catalog/ItemGrid.vue'
import LineStationSheet from '../components/catalog/LineStationSheet.vue'
import BasketBar from '../components/catalog/BasketBar.vue'
import TableField from '../components/review/TableField.vue'

const { t } = useI18n()
const catalog = useCatalogStore()
const openItems = useOpenItemsStore()
const order = useOrderStore()
const session = useSessionStore()

const tappedCategory = ref<string | null>(null)
const tableField = ref<{ focus: () => void } | null>(null)
const isTableMissing = ref(false)
const itemAwaitingStation = ref<CatalogItem | null>(null)
const noteAwaitingStation = ref<string | null>(null)
const linesAwaitingStation = ref<number[]>([])

onMounted(async () => {
  await openItems.loadTableNames()
})

const categoryNames = computed(() => catalog.groups.map((group) => group.name))

const openCategory = computed<string | null>({
  get: () => {
    const tapped = tappedCategory.value
    if (tapped !== null && categoryNames.value.includes(tapped)) {
      return tapped
    }
    return categoryNames.value[0] ?? null
  },
  set: (value: string | null) => {
    tappedCategory.value = value
  },
})

const itemsOfTheOpenCategory = computed(
  () => catalog.groups.find((group) => group.name === openCategory.value)?.items ?? [],
)

const tableName = computed({
  get: () => order.draft.tableName,
  set: (value: string) => {
    order.setTable(value)
    if (isTableNameValid(value)) {
      isTableMissing.value = false
    }
  },
})

function goToTheSummary(): void {
  if (!isTableNameValid(order.draft.tableName)) {
    isTableMissing.value = true
    tableField.value?.focus()
    return
  }
  navigate('/review')
}

const itemBehindTheStationChoice = computed(() => {
  if (linesAwaitingStation.value.length === 0) {
    return itemAwaitingStation.value
  }
  const line = order.draft.lines[linesAwaitingStation.value[0]]
  return catalog.catalog.items.find((item) => item.id === line?.catalogItemId) ?? null
})

function portionsIn(category: string): number {
  return portionsOfCategory(order.draft, catalog.catalog.items, category)
}

function positionsFor(itemId: string): ItemPosition[] {
  const item = catalog.catalog.items.find((candidate) => candidate.id === itemId)
  return item === undefined ? [] : positionsForItem(order.draft, item, catalog.stationName)
}

function place(item: CatalogItem, note: string | null, stationId: string | null): void {
  order.addItem({
    catalogItemId: item.id,
    note: note,
    stationId: stationId,
    name: item.name,
    unitPriceCents: item.priceCents,
  })
}

function addItem(item: CatalogItem, note: string | null = null): void {
  if (needsStationChoice(item)) {
    itemAwaitingStation.value = item
    noteAwaitingStation.value = note
    return
  }
  place(item, note, item.stationIds[0] ?? null)
}

function addItemWithANote(item: CatalogItem, note: string): void {
  addItem(item, note)
}

function renameNote(indexes: number[], note: string): void {
  indexes.forEach((index) => order.noteLine(index, note))
}

function chooseStation(stationId: string): void {
  if (linesAwaitingStation.value.length > 0) {
    linesAwaitingStation.value.forEach((index) => order.chooseStation(index, stationId))
    linesAwaitingStation.value = []
    return
  }
  const item = itemAwaitingStation.value
  if (item === null) {
    return
  }
  place(item, noteAwaitingStation.value, stationId)
  itemAwaitingStation.value = null
  noteAwaitingStation.value = null
}
</script>

<template>
  <v-container class="catalog">
    <CategoryTabs v-model="openCategory" :categories="categoryNames" :portions-for="portionsIn" />
    <ItemGrid
      v-if="itemsOfTheOpenCategory.length > 0"
      :items="itemsOfTheOpenCategory"
      :language="session.language"
      :positions-for="positionsFor"
      class="my-2"
      @add="addItem"
      @add-with-a-note="addItemWithANote"
      @remove-one="order.dropLine"
      @rename-note="renameNote"
      @change-station="(indexes) => (linesAwaitingStation = indexes)"
    />
    <LineStationSheet
      v-if="itemBehindTheStationChoice !== null"
      :item="itemBehindTheStationChoice"
      :station-name-for="catalog.stationName"
      @choose="chooseStation"
    />
    <TableField
      ref="tableField"
      v-model="tableName"
      :is-missing="isTableMissing"
      :known-table-names="openItems.knownTableNames"
    />
    <v-textarea
      class="order-note"
      :label="t('catalog.orderNote')"
      :model-value="order.draft.note ?? ''"
      @update:model-value="order.setNote($event || null)"
    />
    <BasketBar
      :item-count="order.itemCount"
      :total-cents="order.totalCents"
      :language="session.language"
      @review="goToTheSummary"
    />
  </v-container>
</template>

<style scoped>
.catalog {
  padding-bottom: 96px;
}

.catalog :deep(.category-tabs) {
  position: sticky;
  top: 0;
  z-index: 1;
  background-color: rgb(var(--v-theme-background));
}
</style>
