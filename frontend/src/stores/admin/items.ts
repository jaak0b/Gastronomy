import { defineStore } from 'pinia'
import { ref } from 'vue'
import { request } from '../../api/client'

export interface AdminItem {
  id: string
  name: string
  categoryName: string
  priceCents: number
  sortOrder: number
  isAvailable: boolean
  locationIds: string[]
}

export type AdminItemDraft = Omit<AdminItem, 'id' | 'isAvailable'> & { id?: string }

export const useAdminItemsStore = defineStore('adminItems', () => {
  const items = ref<AdminItem[]>([])
  const errorKey = ref<string | null>(null)

  async function load(): Promise<void> {
    const result = await request<{ items: AdminItem[] }>('/api/admin/items')
    if (result.kind === 'ok') {
      items.value = result.data.items
    }
  }

  async function save(item: AdminItemDraft): Promise<boolean> {
    errorKey.value = null
    if (item.locationIds.length === 0) {
      errorKey.value = 'admin.items.needsLocation'
      return false
    }
    const path = item.id === undefined ? '/api/admin/items' : `/api/admin/items/${item.id}`
    const result = await request(path, {
      method: item.id === undefined ? 'POST' : 'PUT',
      body: {
        name: item.name,
        categoryName: item.categoryName,
        priceCents: item.priceCents,
        sortOrder: item.sortOrder,
        locationIds: item.locationIds,
      },
    })
    if (result.kind === 'error') {
      errorKey.value = result.body?.messageKey ?? 'admin.items.needsLocation'
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

  async function deactivate(id: string): Promise<void> {
    errorKey.value = null
    const result = await request(`/api/admin/items/${id}/deactivate`, { method: 'POST' })
    if (result.kind === 'error') {
      errorKey.value = result.body?.messageKey ?? 'admin.items.deactivateBlocked'
      return
    }
    await load()
  }

  return { items, errorKey, load, save, setAvailability, deactivate }
})
