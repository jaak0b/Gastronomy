import { defineStore } from 'pinia'
import { ref } from 'vue'
import { listFrom, request, type ApiResult } from '../../api/client'
import { adminErrorMessage, type AdminErrorMessage } from '../../core/adminErrorMessage'
import { useAdminFestivalsStore } from './festivals'

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

export interface MenuPlacement {
  priceCents: number
  stationIds: string[]
}

export const useAdminItemsStore = defineStore('adminItems', () => {
  const items = ref<AdminItem[]>([])
  const loadFailed = ref(false)
  const errorMessage = ref<AdminErrorMessage | null>(null)

  function pickedFestivalId(): string | null {
    return useAdminFestivalsStore().pickedFestivalId
  }

  async function load(): Promise<void> {
    loadFailed.value = false
    const festivalId = pickedFestivalId()
    const path =
      festivalId === null ? '/api/admin/items' : `/api/admin/items?festivalId=${festivalId}`
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

  async function reportAndReload(result: ApiResult<unknown>): Promise<boolean> {
    if (result.kind !== 'ok') {
      errorMessage.value = adminErrorMessage(result.kind === 'error' ? result.body : null)
      return false
    }
    await load()
    return true
  }

  async function save(item: AdminItemDraft): Promise<boolean> {
    errorMessage.value = null
    const body = {
      name: item.name,
      categoryId: item.categoryId,
      sortOrder: item.sortOrder,
      productionMinutes: item.productionMinutes,
    }
    if (item.itemId === undefined) {
      return reportAndReload(await request('/api/admin/items', { method: 'POST', body }))
    }
    return reportAndReload(
      await request(`/api/admin/items/${item.itemId}`, { method: 'PUT', body }),
    )
  }

  async function putOnTheMenu(itemId: string, placement: MenuPlacement): Promise<boolean> {
    errorMessage.value = null
    const festivalId = pickedFestivalId()
    if (festivalId === null) {
      return false
    }
    return reportAndReload(
      await request(`/api/admin/festivals/${festivalId}/items/${itemId}`, {
        method: 'PUT',
        body: { priceCents: placement.priceCents, stationIds: placement.stationIds },
      }),
    )
  }

  async function takeOffTheMenu(itemId: string): Promise<boolean> {
    errorMessage.value = null
    const festivalId = pickedFestivalId()
    if (festivalId === null) {
      return false
    }
    return reportAndReload(
      await request(`/api/admin/festivals/${festivalId}/items/${itemId}`, { method: 'DELETE' }),
    )
  }

  async function setAvailability(itemId: string, isAvailable: boolean): Promise<boolean> {
    errorMessage.value = null
    const festivalId = pickedFestivalId()
    if (festivalId === null) {
      return false
    }
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

  function forgetError(): void {
    errorMessage.value = null
  }

  return {
    items,
    loadFailed,
    errorMessage,
    load,
    save,
    putOnTheMenu,
    takeOffTheMenu,
    setAvailability,
    setActive,
    forgetError,
  }
})
