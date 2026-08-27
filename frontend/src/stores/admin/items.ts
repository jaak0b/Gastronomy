import { defineStore } from 'pinia'
import { ref } from 'vue'
import { listFrom, request } from '../../api/client'

export interface AdminItem {
  itemId: string
  name: string
  categoryName: string
  priceCents: number
  sortOrder: number
  isActive: boolean
  isAvailable: boolean
  stationIds: string[]
}

export type AdminItemDraft = Omit<AdminItem, 'itemId' | 'isActive' | 'isAvailable'> & {
  itemId?: string
}

export const useAdminItemsStore = defineStore('adminItems', () => {
  const items = ref<AdminItem[]>([])
  const loadFailed = ref(false)
  const errorKey = ref<string | null>(null)

  async function load(): Promise<void> {
    loadFailed.value = false
    const result = await request<unknown>('/api/admin/items')
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

  async function save(item: AdminItemDraft): Promise<boolean> {
    errorKey.value = null
    if (item.stationIds.length === 0) {
      errorKey.value = 'admin.items.needsStation'
      return false
    }
    const path =
      item.itemId === undefined ? '/api/admin/items' : `/api/admin/items/${item.itemId}`
    const result = await request(path, {
      method: item.itemId === undefined ? 'POST' : 'PUT',
      body: {
        name: item.name,
        categoryName: item.categoryName,
        priceCents: item.priceCents,
        sortOrder: item.sortOrder,
        stationIds: item.stationIds,
      },
    })
    if (result.kind === 'error') {
      errorKey.value = result.body?.messageKey ?? 'admin.items.needsStation'
      return false
    }
    await load()
    return true
  }

  async function setAvailability(id: string, isAvailable: boolean): Promise<void> {
    await request(`/api/admin/items/${id}/availability`, {
      method: 'POST',
      body: { isAvailable },
    })
    await load()
  }

  async function setActive(id: string, isActive: boolean): Promise<void> {
    errorKey.value = null
    const action = isActive ? 'activate' : 'deactivate'
    const result = await request(`/api/admin/items/${id}/${action}`, { method: 'POST' })
    if (result.kind === 'error') {
      errorKey.value = result.body?.messageKey ?? 'admin.items.deactivateBlocked'
      return
    }
    await load()
  }

  return { items, loadFailed, errorKey, load, save, setAvailability, setActive }
})
