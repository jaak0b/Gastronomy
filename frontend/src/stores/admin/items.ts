import { defineStore } from 'pinia'
import { ref } from 'vue'
import { listFrom, request, type ApiResult } from '../../api/client'
import { adminErrorMessage, type AdminErrorMessage } from '../../core/adminErrorMessage'
import { useConnectionStore } from '../connection'

export interface AdminItemAtFestival {
  priceCents: number
  isAvailable: boolean
  stationIds: string[]
}

export interface AdminItem {
  itemId: string
  name: string
  categoryId: string
  sortOrder: number
  isActive: boolean
  productionMinutes: number | null
  atTheFestival: AdminItemAtFestival | null
}

export interface AdminItemDraft {
  itemId?: string
  name: string
  categoryId: string
  sortOrder: number
  productionMinutes: number | null
}

export interface FestivalPlacement {
  priceCents: number
  stationIds: string[]
}

interface CreatedItem {
  itemId: string
}

export const useAdminItemsStore = defineStore('adminItems', () => {
  const items = ref<AdminItem[]>([])
  const loadFailed = ref(false)
  const errorMessage = ref<AdminErrorMessage | null>(null)
  const festivalInView = ref<string | null>(null)

  async function readInto(path: string): Promise<void> {
    loadFailed.value = false
    const result = await request<unknown>(path)
    if (result.kind !== 'ok') {
      loadFailed.value = true
      return
    }
    const rows = listFrom<AdminItem>(result.data, 'items')
    if (rows === null) {
      loadFailed.value = true
      return
    }
    items.value = rows
  }

  async function load(): Promise<void> {
    festivalInView.value = null
    await readInto('/api/admin/items')
  }

  async function loadAtTheFestival(festivalId: string): Promise<void> {
    festivalInView.value = festivalId
    await readInto(`/api/admin/items?festivalId=${festivalId}`)
  }

  async function reload(): Promise<void> {
    const festivalId = festivalInView.value
    if (festivalId === null) {
      await load()
      return
    }
    await loadAtTheFestival(festivalId)
  }

  async function reportAndReload(result: ApiResult<unknown>): Promise<boolean> {
    if (result.kind !== 'ok') {
      errorMessage.value = adminErrorMessage(result.kind === 'error' ? result.body : null)
      return false
    }
    await reload()
    return true
  }

  function bodyOf(item: AdminItemDraft): Record<string, unknown> {
    return {
      name: item.name,
      categoryId: item.categoryId,
      sortOrder: item.sortOrder,
      productionMinutes: item.productionMinutes,
    }
  }

  async function create(item: AdminItemDraft): Promise<string | null> {
    errorMessage.value = null
    const result = await request<CreatedItem>('/api/admin/items', {
      method: 'POST',
      body: bodyOf(item),
    })
    if (result.kind !== 'ok') {
      errorMessage.value = adminErrorMessage(result.kind === 'error' ? result.body : null)
      return null
    }
    await reload()
    return result.data.itemId
  }

  async function save(item: AdminItemDraft): Promise<boolean> {
    errorMessage.value = null
    if (item.itemId === undefined) {
      return (await create(item)) !== null
    }
    return reportAndReload(
      await request(`/api/admin/items/${item.itemId}`, { method: 'PUT', body: bodyOf(item) }),
    )
  }

  async function putAtTheFestival(
    festivalId: string,
    itemId: string,
    placement: FestivalPlacement,
  ): Promise<boolean> {
    errorMessage.value = null
    return reportAndReload(
      await request(`/api/admin/festivals/${festivalId}/items/${itemId}`, {
        method: 'PUT',
        body: { priceCents: placement.priceCents, stationIds: placement.stationIds },
      }),
    )
  }

  async function removeFromTheFestival(festivalId: string, itemId: string): Promise<boolean> {
    errorMessage.value = null
    return reportAndReload(
      await request(`/api/admin/festivals/${festivalId}/items/${itemId}`, { method: 'DELETE' }),
    )
  }

  async function setAvailability(
    festivalId: string,
    itemId: string,
    isAvailable: boolean,
  ): Promise<boolean> {
    errorMessage.value = null
    return reportAndReload(
      await request(`/api/admin/festivals/${festivalId}/items/${itemId}/availability`, {
        method: 'POST',
        body: { isAvailable },
      }),
    )
  }

  async function setActive(itemId: string, isActive: boolean): Promise<void> {
    errorMessage.value = null
    if (isActive) {
      await reportAndReload(
        await request(`/api/admin/items/${itemId}/activate`, { method: 'POST' }),
      )
      return
    }
    await reportAndReload(
      await request(`/api/admin/items/${itemId}/deactivate`, { method: 'POST' }),
    )
  }

  function listen(): () => void {
    const connection = useConnectionStore()
    const releases = [
      connection.registerRefetch(reload),
      connection.onEvent<unknown>('CatalogChanged', () => {
        void reload()
      }),
    ]
    return () => {
      for (const release of releases) {
        release()
      }
    }
  }

  function forgetError(): void {
    errorMessage.value = null
  }

  return {
    items,
    loadFailed,
    errorMessage,
    load,
    loadAtTheFestival,
    create,
    save,
    putAtTheFestival,
    removeFromTheFestival,
    setAvailability,
    setActive,
    forgetError,
    listen,
  }
})
