<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type { CatalogItem } from '../core/apiTypes'
import { needsStationChoice } from '../core/routingPreview'
import { useCatalogStore } from '../stores/catalog'
import { useOrderStore } from '../stores/order'
import { useSessionStore } from '../stores/session'
import { usePrinterStatusStore } from '../stores/printerStatus'
import { navigate } from '../router'
import CategoryStrip from '../components/catalog/CategoryStrip.vue'
import ItemGrid from '../components/catalog/ItemGrid.vue'
import LineStationSheet from '../components/catalog/LineStationSheet.vue'
import BasketBar from '../components/catalog/BasketBar.vue'

const { t } = useI18n()
const catalog = useCatalogStore()
const order = useOrderStore()
const session = useSessionStore()
const printerStatus = usePrinterStatusStore()

const search = ref('')
const itemAwaitingStation = ref<CatalogItem | null>(null)
const headingElements: Record<string, HTMLElement> = {}

const isSearching = computed(() => search.value.trim().length > 0)

const searchResults = computed(() => {
  const typed = search.value.trim().toLowerCase()
  return catalog.catalog.items.filter((item) => item.name.toLowerCase().includes(typed))
})

const groups = computed(() =>
  catalog.categories.map((category) => ({
    name: category.name,
    items: catalog.itemsInCategory(category.name),
  })),
)

function rememberHeading(name: string, element: unknown): void {
  if (element instanceof HTMLElement) {
    headingElements[name] = element
  }
}

function jumpTo(name: string): void {
  headingElements[name]?.scrollIntoView({ behavior: 'smooth', block: 'start' })
}

function quantityFor(itemId: string): number {
  return order.draft.lines
    .filter((line) => line.catalogItemId === itemId)
    .reduce((count, line) => count + line.quantity, 0)
}

function addItem(item: CatalogItem): void {
  if (needsStationChoice(item)) {
    itemAwaitingStation.value = item
    return
  }
  order.addItem({
    catalogItemId: item.id,
    quantity: 1,
    note: null,
    stationId: item.stationIds[0] ?? null,
    name: item.name,
    unitPriceCents: item.priceCents,
  })
}

function chooseStation(stationId: string): void {
  const item = itemAwaitingStation.value
  if (item === null) {
    return
  }
  order.addItem({
    catalogItemId: item.id,
    quantity: 1,
    note: null,
    stationId: stationId,
    name: item.name,
    unitPriceCents: item.priceCents,
  })
  itemAwaitingStation.value = null
}

function removeItem(item: CatalogItem): void {
  const index = order.draft.lines.findIndex((line) => line.catalogItemId === item.id)
  if (index === -1) {
    return
  }
  order.changeQuantity(index, order.draft.lines[index].quantity - 1)
}
</script>

<template>
  <v-container class="catalog">
    <h1 class="text-h5 mb-2">{{ t('catalog.title') }}</h1>
    <v-alert
      v-for="(warning, index) in printerStatus.catalogWarnings"
      :key="index"
      class="station-warning mb-2"
      type="warning"
      variant="tonal"
    >
      {{ t(warning.key, { name: warning.name }) }}
    </v-alert>
    <v-text-field
      v-model="search"
      class="catalog-search"
      type="search"
      :placeholder="t('catalog.searchPlaceholder')"
      hide-details
    />
    <CategoryStrip :categories="catalog.categories" @select="jumpTo" />
    <ItemGrid
      v-if="isSearching"
      :items="searchResults"
      :language="session.language"
      :quantity-for="quantityFor"
      @add="addItem"
      @remove="removeItem"
    />
    <template v-else>
      <section v-for="group in groups" :key="group.name" class="category-section">
        <h2
          :ref="(element) => rememberHeading(group.name, element)"
          class="category-heading text-subtitle-1 font-weight-bold py-2"
          :data-category="group.name"
        >
          {{ group.name }}
        </h2>
        <ItemGrid
          :items="group.items"
          :language="session.language"
          :quantity-for="quantityFor"
          @add="addItem"
          @remove="removeItem"
        />
      </section>
    </template>
    <LineStationSheet
      v-if="itemAwaitingStation !== null"
      :item="itemAwaitingStation"
      :station-name-for="catalog.stationName"
      @choose="chooseStation"
    />
    <BasketBar
      :item-count="order.itemCount"
      :total-cents="order.totalCents"
      :language="session.language"
      @review="navigate('/review')"
    />
  </v-container>
</template>

<style scoped>
.category-heading {
  position: sticky;
  top: 0;
  z-index: 1;
  background-color: rgb(var(--v-theme-background));
}
</style>
