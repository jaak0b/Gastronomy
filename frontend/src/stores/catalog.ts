import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { listFrom, request } from '../api/client'
import type { Catalog, CatalogCategory, CatalogItem, CatalogStation } from '../core/apiTypes'
import { findCatalogStation } from '../core/catalog'
import { groupByCategory, type CategoryGroup } from '../core/grouping'
import { useConnectionStore } from './connection'
import { useSessionStore } from './session'

const EMPTY_CATALOG: Catalog = {
  categories: [],
  items: [],
  stations: [],
}

export const useCatalogStore = defineStore('catalog', () => {
  const catalog = ref<Catalog>(EMPTY_CATALOG)

  const groups = computed<CategoryGroup<CatalogCategory, CatalogItem>[]>(() =>
    groupByCategory(
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
    const result = await request<unknown>('/api/catalog', { token: session.deviceToken })
    if (result.kind !== 'ok') {
      return
    }
    catalog.value = {
      categories: listFrom<CatalogCategory>(result.data, 'categories') ?? [],
      items: listFrom<CatalogItem>(result.data, 'items') ?? [],
      stations: listFrom<CatalogStation>(result.data, 'stations') ?? [],
    }
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
  }

  return { catalog, groups, stationName, load, listen }
})
