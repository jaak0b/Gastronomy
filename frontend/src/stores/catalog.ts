import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { request } from '../api/client'
import type { Catalog, CatalogItem } from '../core/apiTypes'
import { groupByCategory, type CategoryGroup } from '../core/grouping'
import { useConnectionStore } from './connection'
import { useSessionStore } from './session'

const EMPTY_CATALOG: Catalog = {
  version: '',
  categories: [],
  items: [],
  stations: [],
}

export const useCatalogStore = defineStore('catalog', () => {
  const catalog = ref<Catalog>(EMPTY_CATALOG)

  const groups = computed<CategoryGroup<CatalogItem>[]>(() =>
    groupByCategory(
      catalog.value.items,
      (item) => item.categoryName,
      (item) => item.name,
    ),
  )

  function stationName(stationId: string): string {
    return catalog.value.stations.find((station) => station.id === stationId)?.name ?? ''
  }

  async function load(): Promise<void> {
    const session = useSessionStore()
    if (session.deviceToken === null) {
      return
    }
    const result = await request<Catalog>('/api/catalog', { token: session.deviceToken })
    if (result.kind === 'ok') {
      catalog.value = result.data
    }
  }

  function listen(): void {
    const connection = useConnectionStore()
    connection.registerRefetch(load)
    connection.onEvent<{ version: string }>('CatalogChanged', () => {
      void load()
    })
  }

  return { catalog, groups, stationName, load, listen }
})
