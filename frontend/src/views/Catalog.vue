<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import type { CatalogCategory, CatalogItem } from '../core/apiTypes'
import { portionsOfCategory } from '../core/categoryPortions'
import { positionsForItem, type ItemPosition } from '../core/itemPositions'
import { pickerEstimateMinutes } from '../core/estimates'
import { letteringColourOn } from '../core/letteringColour'
import { needsStationChoice } from '../core/routingPreview'
import { isTableNameValid } from '../core/tableName'
import { useCatalogStore } from '../stores/catalog'
import { useEstimatesStore } from '../stores/estimates'
import { useOpenItemsStore } from '../stores/openItems'
import { useOrderStore } from '../stores/order'
import { useSessionStore } from '../stores/session'
import { closeTheStepInsideTheScreen, navigate, openAStepInsideTheScreen } from '../router'
import DockedStrip from '../components/DockedStrip.vue'
import ItemGrid from '../components/catalog/ItemGrid.vue'
import LineStationSheet from '../components/catalog/LineStationSheet.vue'
import BasketBar from '../components/catalog/BasketBar.vue'
import TableField from '../components/review/TableField.vue'

const { t } = useI18n()
const catalog = useCatalogStore()
const estimates = useEstimatesStore()
const openItems = useOpenItemsStore()
const order = useOrderStore()
const session = useSessionStore()

const tappedCategory = ref<string | null>(null)
const tableField = ref<{ focus: () => void } | null>(null)
const isTableMissing = ref(false)
const itemAwaitingStation = ref<CatalogItem | null>(null)
const noteAwaitingStation = ref<string | null>(null)
const linesAwaitingStation = ref<number[]>([])

onMounted(() => {
  if (order.changesAreRefused) {
    navigate('/review')
  }
})

onMounted(async () => {
  await openItems.loadTableNames()
  await estimates.load()
})

onUnmounted(() => {
  closeTheStepInsideTheScreen()
})

const openCategory = computed<CatalogCategory | null>(
  () =>
    catalog.catalog.categories.find(
      (category) => category.categoryId === tappedCategory.value,
    ) ?? null,
)

watch(openCategory, (category) => {
  if (category === null) {
    closeTheStepInsideTheScreen()
  }
})

function forgetTheStationQuestion(): void {
  itemAwaitingStation.value = null
  noteAwaitingStation.value = null
  linesAwaitingStation.value = []
}

function closeTheOpenCategory(): void {
  tappedCategory.value = null
  forgetTheStationQuestion()
}

function openTheCategory(category: CatalogCategory): void {
  tappedCategory.value = category.categoryId
  openAStepInsideTheScreen(closeTheOpenCategory)
}

const itemsOfTheOpenCategory = computed(
  () =>
    catalog.groups.find(
      (group) => group.category.categoryId === openCategory.value?.categoryId,
    )?.items ?? [],
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

function portionsIn(categoryId: string): number {
  return portionsOfCategory(order.draft, catalog.catalog.items, categoryId)
}

function labelFor(category: CatalogCategory): string {
  const portions = portionsIn(category.categoryId)
  if (portions < 1) {
    return category.name
  }
  return t('catalog.categoryWithCount', { count: portions, name: category.name })
}

function paintedIn(colourHex: string): Record<string, string> {
  return { backgroundColor: colourHex, color: letteringColourOn(colourHex) }
}

function positionsFor(itemId: string): ItemPosition[] {
  const item = catalog.catalog.items.find((candidate) => candidate.id === itemId)
  return item === undefined ? [] : positionsForItem(order.draft, item)
}

function readyInMinutesFor(itemId: string): number | null {
  const item = catalog.catalog.items.find((candidate) => candidate.id === itemId)
  return item === undefined ? null : pickerEstimateMinutes(item, estimates.stations)
}

function place(item: CatalogItem, note: string | null, stationId: string | null): void {
  order.addItem({
    catalogItemId: item.id,
    note: note,
    stationId: stationId,
    name: item.name,
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
    <template v-if="openCategory === null">
      <v-btn
        v-for="category in catalog.catalog.categories"
        :key="category.categoryId"
        class="category-button my-2"
        block
        size="x-large"
        variant="flat"
        :style="paintedIn(category.colourHex)"
        @click="openTheCategory(category)"
      >
        {{ labelFor(category) }}
      </v-btn>
      <TableField
        ref="tableField"
        v-model="tableName"
        :is-missing="isTableMissing"
        :known-table-names="openItems.knownTableNames"
      />
      <v-textarea
        class="order-note"
        maxlength="200"
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
    </template>
    <template v-else>
      <h2
        class="open-category-name text-h6 px-3 py-2 rounded"
        :style="paintedIn(openCategory.colourHex)"
      >
        {{ openCategory.name }}
      </h2>
      <v-alert v-if="estimates.loadFailed" class="estimates-failed my-2" type="info" variant="tonal">
        {{ t('estimates.loadFailed') }}
      </v-alert>
      <ItemGrid
        :items="itemsOfTheOpenCategory"
        :language="session.language"
        :positions-for="positionsFor"
        :ready-in-minutes-for="readyInMinutesFor"
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
        @cancel="forgetTheStationQuestion"
      />
      <DockedStrip class="back-strip">
        <div class="py-3">
          <v-btn
            class="back-to-categories"
            block
            size="x-large"
            variant="outlined"
            @click="closeTheStepInsideTheScreen()"
          >
            {{ t('catalog.backToCategories') }}
          </v-btn>
        </div>
      </DockedStrip>
    </template>
  </v-container>
</template>

<style scoped>
.catalog {
  padding-bottom: 96px;
}
</style>
