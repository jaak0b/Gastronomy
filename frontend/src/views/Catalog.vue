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

const selectedCategory = ref<string | null>(null)
const search = ref('')
const itemAwaitingStation = ref<CatalogItem | null>(null)

const activeCategory = computed(
  () => selectedCategory.value ?? catalog.categories[0]?.name ?? null,
)

const items = computed(() => {
  const typed = search.value.trim().toLowerCase()
  if (typed.length > 0) {
    return catalog.catalog.items.filter((item) => item.name.toLowerCase().includes(typed))
  }
  return activeCategory.value === null ? [] : catalog.itemsInCategory(activeCategory.value)
})

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
    productionLocationId: item.locationIds[0] ?? null,
    name: item.name,
    unitPriceCents: item.priceCents,
  })
}

function chooseStation(locationId: string): void {
  const item = itemAwaitingStation.value
  if (item === null) {
    return
  }
  order.addItem({
    catalogItemId: item.id,
    quantity: 1,
    note: null,
    productionLocationId: locationId,
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
    <CategoryStrip
      :categories="catalog.categories"
      :selected="activeCategory"
      @select="(name) => (selectedCategory = name)"
    />
    <ItemGrid
      :items="items"
      :language="session.language"
      :quantity-for="quantityFor"
      @add="addItem"
      @remove="removeItem"
    />
    <LineStationSheet
      v-if="itemAwaitingStation !== null"
      :item="itemAwaitingStation"
      :location-name-for="catalog.locationName"
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
