<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { CatalogCategoryView, CatalogItemView } from '../../shared/api/generatedSchemas'
import { countCategoryPortions } from '../core/categoryPortions'
import { positionsForItem, type ItemPosition } from '../core/itemPositions'
import { estimateRangeForItem, type EstimateRange } from '../core/estimates'
import { paintOfCategoryColour } from '../../shared/core/letteringColour'
import { needsStationChoice } from '../core/routingPreview'
import { useCatalogStore } from '../stores/catalog'
import { useEstimatesStore } from '../stores/estimates'
import { useOrderStore } from '../stores/order'
import { useSessionStore } from '../../shared/stores/session'
import { closeOpenStep, navigate, registerOpenStepCloser } from '../../shared/router/router'
import { useStationChoiceQuotes } from '../composables/useStationChoiceQuotes'
import CategoryLabel from '../../shared/components/CategoryLabel.vue'
import DockedStrip from '../components/DockedStrip.vue'
import ItemGrid from '../components/catalog/ItemGrid.vue'
import LineStationSheet from '../components/catalog/LineStationSheet.vue'
import BasketBar from '../components/catalog/BasketBar.vue'

const { t } = useI18n()
const catalog = useCatalogStore()
const estimates = useEstimatesStore()
const order = useOrderStore()
const session = useSessionStore()

const tappedCategory = ref<string | null>(null)
const itemAwaitingStation = ref<CatalogItemView | null>(null)
const linesAwaitingStation = ref<number[]>([])
const focusTheNoteField = ref(false)

function leaveWhenTheOrderCannotBeBuiltHere(): void {
  if (order.changesAreRefused) {
    navigate('/review')
    return
  }
  if (order.draft.table === null) {
    navigate('/')
  }
}

onMounted(async () => {
  leaveWhenTheOrderCannotBeBuiltHere()
  await estimates.load()
})

watch(() => order.draft.table, leaveWhenTheOrderCannotBeBuiltHere)

onUnmounted(() => {
  closeOpenStep()
})

const openCategory = computed<CatalogCategoryView | null>(
  () =>
    catalog.catalog.categories.find(
      (category) => category.categoryId === tappedCategory.value,
    ) ?? null,
)

watch(openCategory, (category) => {
  if (category === null) {
    closeOpenStep()
  }
})

function forgetTheStationQuestion(): void {
  itemAwaitingStation.value = null
  linesAwaitingStation.value = []
  focusTheNoteField.value = false
}

function closeTheOpenCategory(): void {
  tappedCategory.value = null
  forgetTheStationQuestion()
}

function openTheCategory(category: CatalogCategoryView): void {
  tappedCategory.value = category.categoryId
  registerOpenStepCloser(closeTheOpenCategory)
}

const itemsInTheOpenCategory = computed(
  () =>
    catalog.groups.find(
      (group) => group.category.categoryId === openCategory.value?.categoryId,
    )?.items ?? [],
)

const itemBehindTheStationChoice = computed(() => {
  if (linesAwaitingStation.value.length === 0) {
    return itemAwaitingStation.value
  }
  const line = order.draft.lines[linesAwaitingStation.value[0]]
  return catalog.catalog.items.find((item) => item.id === line?.catalogItemId) ?? null
})

const { estimateForTheStationChoice } = useStationChoiceQuotes(
  itemBehindTheStationChoice,
  linesAwaitingStation,
)

function portionsIn(categoryId: string): number {
  return countCategoryPortions(order.draft, catalog.catalog.items, categoryId)
}

function labelFor(category: CatalogCategoryView): string {
  const portions = portionsIn(category.categoryId)
  if (portions < 1) {
    return category.name
  }
  return t('phone.catalog.labels.categoryWithCount', { count: portions, name: category.name })
}

function positionsFor(itemId: string): ItemPosition[] {
  const item = catalog.catalog.items.find((candidate) => candidate.id === itemId)
  return item === undefined ? [] : positionsForItem(order.draft, item, catalog.stationName)
}

const currentStationId = computed(() => {
  const first = linesAwaitingStation.value[0]
  return first === undefined ? null : order.draft.lines[first]?.stationId ?? null
})

function estimateRangeFor(itemId: string): EstimateRange | null {
  return estimateRangeForItem(estimates.items, itemId)
}

function place(item: CatalogItemView, note: string | null, stationId: string | null): void {
  order.addItem({
    catalogItemId: item.id,
    note: note,
    stationId: stationId,
    name: item.name,
  })
}

function addItem(item: CatalogItemView, note: string | null = null): void {
  if (needsStationChoice(item)) {
    itemAwaitingStation.value = item
    focusTheNoteField.value = false
    return
  }
  place(item, note, item.stationIds[0] ?? null)
}

function addItemWithANote(item: CatalogItemView, note: string): void {
  addItem(item, note)
}

function addItemWithANoteAtAStation(item: CatalogItemView): void {
  addItem(item)
  focusTheNoteField.value = true
}

function addLikeGroup(item: CatalogItemView, note: string | null, stationId: string | null): void {
  place(item, note, stationId)
}

function renameNote(indexes: number[], note: string): void {
  indexes.forEach((index) => order.noteLine(index, note))
}

function chooseStation(stationId: string, note: string | null): void {
  const movedLines = linesAwaitingStation.value
  if (movedLines.length > 0) {
    movedLines.forEach((index) => order.chooseStation(index, stationId))
    linesAwaitingStation.value = []
    return
  }
  const item = itemAwaitingStation.value
  if (item === null) {
    return
  }
  itemAwaitingStation.value = null
  place(item, note, stationId)
}
</script>

<template>
  <v-container class="catalog" data-test="catalog">
    <template v-if="openCategory === null">
      <div class="category-grid my-2" data-test="category-grid">
        <v-btn-wide
          v-for="category in catalog.catalog.categories"
          :key="category.categoryId"
          class="category-button"
          data-test="category-button"
          :style="paintOfCategoryColour(category.colourHex)"
          @click="openTheCategory(category)"
        >
          {{ labelFor(category) }}
        </v-btn-wide>
      </div>
      <DockedStrip class="order-tray">
        <BasketBar
          :item-count="order.itemCount"
          :total-cents="order.totalCents"
          :language="session.language"
          @review="navigate('/review')"
        />
      </DockedStrip>
    </template>
    <template v-else>
      <CategoryLabel
        class="open-category-name"
        data-test="open-category-name"
        :category="openCategory"
        :heading-level="2"
      />
      <ItemGrid
        :items="itemsInTheOpenCategory"
        :language="session.language"
        :positions-for="positionsFor"
        :estimate-range-for="estimateRangeFor"
        :station-name-for="catalog.stationName"
        class="my-2"
        @add="addItem"
        @add-with-a-note="addItemWithANote"
        @add-with-a-note-at-a-station="addItemWithANoteAtAStation"
        @add-like-group="addLikeGroup"
        @remove-one="order.dropLine"
        @rename-note="renameNote"
        @change-station="(indexes) => (linesAwaitingStation = indexes)"
      />
      <LineStationSheet
        v-if="itemBehindTheStationChoice !== null"
        :item="itemBehindTheStationChoice"
        :language="session.language"
        :station-name-for="catalog.stationName"
        :estimate-for="estimateForTheStationChoice"
        :current-station-id="currentStationId"
        :with-note="linesAwaitingStation.length === 0"
        :focus-the-note="focusTheNoteField"
        @choose="chooseStation"
        @cancel="forgetTheStationQuestion"
      />
      <DockedStrip class="back-strip">
        <div class="py-3">
          <v-btn-wide
            class="back-to-categories"
            data-test="back-to-categories"
            variant="outlined"
            @click="closeOpenStep()"
          >
            {{ t('phone.catalog.actions.backToCategories') }}
          </v-btn-wide>
        </div>
      </DockedStrip>
    </template>
  </v-container>
</template>

<style scoped>
.category-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 8px;
}

.category-button {
  min-width: 0;
  height: auto;
  min-height: 64px;
  padding-top: 12px;
  padding-bottom: 12px;
}

.category-button :deep(.v-btn__content) {
  white-space: normal;
}

.catalog {
  display: flex;
  flex-direction: column;
  min-height: calc(100vh - var(--v-layout-top, 0px));
  min-height: calc(100dvh - var(--v-layout-top, 0px));
  padding-bottom: 0;
}

.order-tray,
.back-strip {
  margin-top: auto;
}
</style>
