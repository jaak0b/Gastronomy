import { defineStore } from 'pinia'
import { ref } from 'vue'
import { listFrom, request, type ApiResult } from '../../api/client'
import { adminErrorMessage, type AdminErrorMessage } from '../../core/adminErrorMessage'
import { useConnectionStore } from '../connection'
import { useAdminEnrolmentStore } from './enrolment'
import { useAdminFestivalsStore } from './festivals'

export interface AdminStation {
  stationId: string
  name: string
  sortOrder: number
  isActive: boolean
  hasDevice: boolean
  isAtTheFestival: boolean
}

export const useAdminStationsStore = defineStore('adminStations', () => {
  const stations = ref<AdminStation[]>([])
  const loadFailed = ref(false)
  const errorMessage = ref<AdminErrorMessage | null>(null)

  function pickedFestivalId(): string | null {
    return useAdminFestivalsStore().pickedFestivalId
  }

  async function load(): Promise<void> {
    loadFailed.value = false
    const festivalId = pickedFestivalId()
    const path =
      festivalId === null ? '/api/admin/stations' : `/api/admin/stations?festivalId=${festivalId}`
    const result = await request<unknown>(path)
    if (result.kind !== 'ok') {
      loadFailed.value = true
      return
    }
    const rows = listFrom<AdminStation>(result.data, 'stations')
    if (rows === null) {
      loadFailed.value = true
      return
    }
    stations.value = rows
  }

  async function reportAndReload(result: ApiResult<unknown>): Promise<boolean> {
    if (result.kind !== 'ok') {
      errorMessage.value = adminErrorMessage(result.kind === 'error' ? result.body : null)
      return false
    }
    await load()
    return true
  }

  async function save(
    station: Pick<AdminStation, 'name' | 'sortOrder'> & { stationId?: string },
  ): Promise<boolean> {
    errorMessage.value = null
    const body = { name: station.name, sortOrder: station.sortOrder }
    if (station.stationId === undefined) {
      return reportAndReload(await request('/api/admin/stations', { method: 'POST', body }))
    }
    return reportAndReload(
      await request(`/api/admin/stations/${station.stationId}`, { method: 'PUT', body }),
    )
  }

  async function addToTheFestival(stationId: string): Promise<boolean> {
    errorMessage.value = null
    const festivalId = pickedFestivalId()
    if (festivalId === null) {
      return false
    }
    return reportAndReload(
      await request(`/api/admin/festivals/${festivalId}/stations/${stationId}`, {
        method: 'PUT',
      }),
    )
  }

  async function removeFromTheFestival(stationId: string): Promise<boolean> {
    errorMessage.value = null
    const festivalId = pickedFestivalId()
    if (festivalId === null) {
      return false
    }
    return reportAndReload(
      await request(`/api/admin/festivals/${festivalId}/stations/${stationId}`, {
        method: 'DELETE',
      }),
    )
  }

  async function setActive(id: string, isActive: boolean): Promise<void> {
    errorMessage.value = null
    if (isActive) {
      await reportAndReload(
        await request(`/api/admin/stations/${id}/activate`, { method: 'POST' }),
      )
      return
    }
    await reportAndReload(
      await request(`/api/admin/stations/${id}/deactivate`, { method: 'POST' }),
    )
  }

  function listen(): () => void {
    const connection = useConnectionStore()
    const releases = [
      connection.registerRefetch(load),
      useAdminEnrolmentStore().listen(() => {
        void load()
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
    stations,
    loadFailed,
    errorMessage,
    load,
    save,
    addToTheFestival,
    removeFromTheFestival,
    setActive,
    forgetError,
    listen,
  }
})
