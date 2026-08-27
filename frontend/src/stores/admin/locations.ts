import { defineStore } from 'pinia'
import { ref } from 'vue'
import { listFrom, request } from '../../api/client'
import { adminErrorMessage, type AdminErrorMessage } from '../../core/adminErrorMessage'
import type { AppLanguage } from '../../core/apiTypes'

export interface AdminLocation {
  locationId: string
  name: string
  sortOrder: number
  slipLanguage: AppLanguage
  isActive: boolean
}

export const useAdminLocationsStore = defineStore('adminLocations', () => {
  const locations = ref<AdminLocation[]>([])
  const loadFailed = ref(false)
  const errorMessage = ref<AdminErrorMessage | null>(null)

  async function load(): Promise<void> {
    loadFailed.value = false
    const result = await request<unknown>('/api/admin/locations')
    if (result.kind !== 'ok') {
      loadFailed.value = true
      return
    }
    const rows = listFrom<AdminLocation>(result.data, 'locations')
    if (rows === null) {
      loadFailed.value = true
      return
    }
    locations.value = rows
  }

  async function save(
    location: Pick<AdminLocation, 'name' | 'sortOrder' | 'slipLanguage'> & { locationId?: string },
  ): Promise<void> {
    const path =
      location.locationId === undefined
        ? '/api/admin/locations'
        : `/api/admin/locations/${location.locationId}`
    await request(path, {
      method: location.locationId === undefined ? 'POST' : 'PUT',
      body: {
        name: location.name,
        sortOrder: location.sortOrder,
        slipLanguage: location.slipLanguage,
      },
    })
    await load()
  }

  async function setActive(id: string, isActive: boolean): Promise<void> {
    errorMessage.value = null
    const action = isActive ? 'activate' : 'deactivate'
    const result = await request(`/api/admin/locations/${id}/${action}`, { method: 'POST' })
    if (result.kind !== 'ok') {
      errorMessage.value = adminErrorMessage(result.kind === 'error' ? result.body : null)
      return
    }
    await load()
  }

  return { locations, loadFailed, errorMessage, load, save, setActive }
})
