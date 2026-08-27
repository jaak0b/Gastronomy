import { defineStore } from 'pinia'
import { ref } from 'vue'
import { listFrom, request } from '../../api/client'
import { adminErrorMessage, type AdminErrorMessage } from '../../core/adminErrorMessage'
import type { AppLanguage } from '../../core/apiTypes'

export interface AdminLocation {
  id: string
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
    location: Pick<AdminLocation, 'name' | 'sortOrder' | 'slipLanguage'> & { id?: string },
  ): Promise<void> {
    const path =
      location.id === undefined ? '/api/admin/locations' : `/api/admin/locations/${location.id}`
    await request(path, {
      method: location.id === undefined ? 'POST' : 'PUT',
      body: {
        name: location.name,
        sortOrder: location.sortOrder,
        slipLanguage: location.slipLanguage,
      },
    })
    await load()
  }

  async function deactivate(id: string): Promise<void> {
    errorMessage.value = null
    const result = await request(`/api/admin/locations/${id}/deactivate`, { method: 'POST' })
    if (result.kind !== 'ok') {
      errorMessage.value = adminErrorMessage(result.kind === 'error' ? result.body : null)
      return
    }
    await load()
  }

  async function regenerateAccessKey(id: string): Promise<string | null> {
    const result = await request<{ stationUrl: string }>(
      `/api/admin/locations/${id}/regenerate-access-key`,
      { method: 'POST' },
    )
    return result.kind === 'ok' ? result.data.stationUrl : null
  }

  return { locations, loadFailed, errorMessage, load, save, deactivate, regenerateAccessKey }
})
