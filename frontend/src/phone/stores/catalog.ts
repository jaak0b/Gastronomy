import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { request } from '../../shared/api/client'
import { CatalogCategoryView, CatalogItemView, CatalogView } from '../../shared/api/generatedSchemas'
import { findCatalogStation } from '../core/basket'
import { groupByCategorySortingItemsByName, type CategoryGroup } from '../../shared/core/grouping'
import { createLatestRequestGate } from '../../shared/core/latestRequestGate'
import { useConnectionStore } from '../../shared/stores/connection'
import { useOrderStore } from './order'
import { useSessionStore } from '../../shared/stores/session'

const EMPTY_CATALOG: CatalogView = {
  festival: null,
  categories: [],
  items: [],
  stations: [],
}

export const useCatalogStore = defineStore('catalog', () => {
  const catalog = ref<CatalogView>(EMPTY_CATALOG)
  const hasLoaded = ref(false)

  const loadGate = createLatestRequestGate()

  const groups = computed<CategoryGroup<CatalogCategoryView, CatalogItemView>[]>(() =>
    groupByCategorySortingItemsByName(
      catalog.value.categories,
      catalog.value.items,
      (category) => category.categoryId,
      (item) => item.categoryId,
      (item) => item.name,
    ),
  )

  function stationName(stationId: string): string {
    return findCatalogStation(catalog.value, stationId)?.name ?? ''
  }

  async function load(): Promise<void> {
    const session = useSessionStore()
    if (session.deviceToken === null) {
      return
    }
    const token = loadGate.startRequest()
    const result = await request('/api/catalog', {
      token: session.deviceToken,
      schema: CatalogView,
    })
    if (!loadGate.isNewestRequest(token)) {
      return
    }
    if (result.kind !== 'ok') {
      return
    }
    catalog.value = result.data
    hasLoaded.value = true
    useOrderStore().dropTheDraftIfTheFestivalChanged()
  }

  function listen(): void {
    const connection = useConnectionStore()
    connection.registerRefetch(load)
    connection.onEvent<unknown>('CatalogChanged', () => {
      void load()
    })
    connection.onEvent<unknown>('StationsChanged', () => {
      void load()
    })
    connection.onEvent<unknown>('FestivalChanged', () => {
      void load()
    })
  }

  return { catalog, hasLoaded, groups, stationName, load, listen }
})
