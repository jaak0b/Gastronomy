import { defineStore } from 'pinia'
import { ref } from 'vue'
import { request, requestAction, type ApiResult } from '../../api/client'
import { adminItemsResponseSchema, createdItemSchema } from '../../core/apiSchemas'
import { adminErrorMessage } from '../../core/adminErrorMessage'
import {
  adminFailed,
  adminOk,
  type AdminActionResult,
} from '../../core/adminActionResult'
import type { AdminItem } from '../../core/apiTypes'
import { assertNever } from '../../core/assertNever'
import { createLatestRequestGate } from '../../core/latestRequestGate'
import { useConnectionStore } from '../connection'

export interface AdminItemDraft {
  itemId?: string
  name: string
  categoryId: string
  sortOrder: number
  productionMinutes: number | null
  isQueueIndependent: boolean
}

export interface FestivalPlacement {
  priceCents: number
  stationIds: string[]
}

export const useAdminItemsStore = defineStore('adminItems', () => {
  const items = ref<AdminItem[]>([])
  const loadFailed = ref(false)
  const festivalInView = ref<string | null>(null)

  const itemsGate = createLatestRequestGate()

  async function loadItemsFrom(path: string): Promise<void> {
    loadFailed.value = false
    const token = itemsGate.start()
    const result = await request(path, { schema: adminItemsResponseSchema })
    if (!itemsGate.isCurrent(token)) {
      return
    }
    if (result.kind !== 'ok') {
      loadFailed.value = true
      return
    }
    items.value = result.data.items
  }

  async function load(): Promise<void> {
    festivalInView.value = null
    await loadItemsFrom('/api/admin/items')
  }

  async function loadAtTheFestival(festivalId: string): Promise<void> {
    festivalInView.value = festivalId
    await loadItemsFrom(`/api/admin/items?festivalId=${festivalId}`)
  }

  async function reload(): Promise<void> {
    const festivalId = festivalInView.value
    if (festivalId === null) {
      await load()
      return
    }
    await loadAtTheFestival(festivalId)
  }

  async function reportAndReload(result: ApiResult<unknown>): Promise<AdminActionResult<null>> {
    if (result.kind !== 'ok') {
      return adminFailed(adminErrorMessage(result.kind === 'error' ? result.body : null))
    }
    await reload()
    return adminOk(null)
  }

  function buildItemRequestBody(item: AdminItemDraft): Record<string, unknown> {
    return {
      name: item.name.trim(),
      categoryId: item.categoryId,
      sortOrder: item.sortOrder,
      productionMinutes: item.productionMinutes,
      isQueueIndependent: item.isQueueIndependent,
    }
  }

  async function create(item: AdminItemDraft): Promise<AdminActionResult<string>> {
    const result = await request('/api/admin/items', {
      method: 'POST',
      body: buildItemRequestBody(item),
      schema: createdItemSchema,
    })
    if (result.kind !== 'ok') {
      return adminFailed(adminErrorMessage(result.kind === 'error' ? result.body : null))
    }
    await reload()
    return adminOk(result.data.itemId)
  }

  async function save(item: AdminItemDraft): Promise<AdminActionResult<null>> {
    if (item.itemId === undefined) {
      const created = await create(item)
      switch (created.kind) {
        case 'ok':
          return adminOk(null)
        case 'failed':
          return adminFailed(created.message)
        default:
          return assertNever(created)
      }
    }
    return reportAndReload(
      await requestAction(`/api/admin/items/${item.itemId}`, {
        method: 'PUT',
        body: buildItemRequestBody(item),
      }),
    )
  }

  async function putAtTheFestival(
    festivalId: string,
    itemId: string,
    placement: FestivalPlacement,
  ): Promise<AdminActionResult<null>> {
    return reportAndReload(
      await requestAction(`/api/admin/festivals/${festivalId}/items/${itemId}`, {
        method: 'PUT',
        body: { priceCents: placement.priceCents, stationIds: placement.stationIds },
      }),
    )
  }

  async function removeFromTheFestival(
    festivalId: string,
    itemId: string,
  ): Promise<AdminActionResult<null>> {
    return reportAndReload(
      await requestAction(`/api/admin/festivals/${festivalId}/items/${itemId}`, {
        method: 'DELETE',
      }),
    )
  }

  async function setAvailability(
    festivalId: string,
    itemId: string,
    isAvailable: boolean,
  ): Promise<AdminActionResult<null>> {
    return reportAndReload(
      await requestAction(`/api/admin/festivals/${festivalId}/items/${itemId}/availability`, {
        method: 'POST',
        body: { isAvailable },
      }),
    )
  }

  async function setActive(itemId: string, isActive: boolean): Promise<AdminActionResult<null>> {
    if (isActive) {
      return reportAndReload(
        await requestAction(`/api/admin/items/${itemId}/activate`, { method: 'POST' }),
      )
    }
    return reportAndReload(
      await requestAction(`/api/admin/items/${itemId}/deactivate`, { method: 'POST' }),
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

  return {
    items,
    loadFailed,
    load,
    loadAtTheFestival,
    create,
    save,
    putAtTheFestival,
    removeFromTheFestival,
    setAvailability,
    setActive,
    listen,
  }
})
