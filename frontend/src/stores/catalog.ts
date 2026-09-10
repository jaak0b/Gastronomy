import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { listFrom, request } from '../api/client'
import type {
  Catalog,
  CatalogCategory,
  CatalogItem,
  CatalogStation,
  RunningFestival,
} from '../core/apiTypes'
import { findCatalogStation } from '../core/basket'
import { groupByCategory, type CategoryGroup } from '../core/grouping'
import { useConnectionStore } from './connection'
import { useOrderStore } from './order'
import { useSessionStore } from './session'

const EMPTY_CATALOG: Catalog = {
  festival: null,
  categories: [],
  items: [],
  stations: [],
}

function runningFestivalIn(data: unknown): RunningFestival | null {
  if (typeof data !== 'object' || data === null) {
    return null
  }
  const festival = (data as Record<string, unknown>).festival
  if (typeof festival !== 'object' || festival === null) {
    return null
  }
  const candidate = festival as Record<string, unknown>
  if (typeof candidate.festivalId !== 'string' || typeof candidate.name !== 'string') {
    return null
  }
  return { festivalId: candidate.festivalId, name: candidate.name }
}

export const useCatalogStore = defineStore('catalog', () => {
  const catalog = ref<Catalog>(EMPTY_CATALOG)
  const hasLoaded = ref(false)

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
      festival: runningFestivalIn(result.data),
      categories: listFrom<CatalogCategory>(result.data, 'categories') ?? [],
      items: listFrom<CatalogItem>(result.data, 'items') ?? [],
      stations: listFrom<CatalogStation>(result.data, 'stations') ?? [],
    }
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
  }

  return { catalog, hasLoaded, groups, stationName, load, listen }
})
