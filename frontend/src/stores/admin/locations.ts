import { defineStore } from 'pinia'
import { ref } from 'vue'
import { request } from '../../api/client'
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
  const errorKey = ref<string | null>(null)
  const errorParameters = ref<Record<string, string | number>>({})

  async function load(): Promise<void> {
    const result = await request<{ locations: AdminLocation[] }>('/api/admin/locations')
    if (result.kind === 'ok') {
      locations.value = result.data.locations
    }
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
    errorKey.value = null
    const result = await request(`/api/admin/locations/${id}/deactivate`, { method: 'POST' })
    if (result.kind === 'error') {
      errorKey.value = result.body?.messageKey ?? 'admin.locations.openTickets'
      errorParameters.value = result.body?.parameters ?? {}
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

  return { locations, errorKey, errorParameters, load, save, deactivate, regenerateAccessKey }
})
