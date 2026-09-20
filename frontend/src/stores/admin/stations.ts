import { defineStore } from 'pinia'
import { ref } from 'vue'
import { request, requestAction, type ApiResult } from '../../shared/api/client'
import { adminStationsResponseSchema, createdStationSchema } from '../../shared/api/apiSchemas'
import { adminErrorMessage } from '../../core/adminErrorMessage'
import {
  adminFailed,
  adminOk,
  type AdminActionResult,
} from '../../core/adminActionResult'
import type { AdminStation } from '../../shared/api/apiTypes'
import { assertNever } from '../../shared/core/assertNever'
import { createLatestRequestGate } from '../../shared/core/latestRequestGate'
import { useConnectionStore } from '../../shared/stores/connection'
import { useAdminEnrolmentStore } from './enrolment'

export interface StationDraft {
  stationId?: string
  name: string
  sortOrder: number
}

export const useAdminStationsStore = defineStore('adminStations', () => {
  const stations = ref<AdminStation[]>([])
  const loadFailed = ref(false)
  const festivalInView = ref<string | null>(null)

  const stationsGate = createLatestRequestGate()

  async function loadStationsFrom(path: string): Promise<void> {
    loadFailed.value = false
    const token = stationsGate.startRequest()
    const result = await request(path, { schema: adminStationsResponseSchema })
    if (!stationsGate.isNewestRequest(token)) {
      return
    }
    if (result.kind !== 'ok') {
      loadFailed.value = true
      return
    }
    stations.value = result.data.stations
  }

  async function load(): Promise<void> {
    festivalInView.value = null
    await loadStationsFrom('/api/admin/stations')
  }

  async function loadAtTheFestival(festivalId: string): Promise<void> {
    festivalInView.value = festivalId
    await loadStationsFrom(`/api/admin/stations?festivalId=${festivalId}`)
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

  async function create(draft: StationDraft): Promise<AdminActionResult<string>> {
    const result = await request('/api/admin/stations', {
      method: 'POST',
      body: { name: draft.name, sortOrder: draft.sortOrder },
      schema: createdStationSchema,
    })
    if (result.kind !== 'ok') {
      return adminFailed(adminErrorMessage(result.kind === 'error' ? result.body : null))
    }
    await reload()
    return adminOk(result.data.stationId)
  }

  async function save(station: StationDraft): Promise<AdminActionResult<null>> {
    if (station.stationId === undefined) {
      const created = await create(station)
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
      await requestAction(`/api/admin/stations/${station.stationId}`, {
        method: 'PUT',
        body: { name: station.name, sortOrder: station.sortOrder },
      }),
    )
  }

  async function addToTheFestival(
    festivalId: string,
    stationId: string,
  ): Promise<AdminActionResult<null>> {
    return reportAndReload(
      await requestAction(`/api/admin/festivals/${festivalId}/stations/${stationId}`, {
        method: 'PUT',
      }),
    )
  }

  async function removeFromTheFestival(
    festivalId: string,
    stationId: string,
  ): Promise<AdminActionResult<null>> {
    return reportAndReload(
      await requestAction(`/api/admin/festivals/${festivalId}/stations/${stationId}`, {
        method: 'DELETE',
      }),
    )
  }

  async function setActive(id: string, isActive: boolean): Promise<AdminActionResult<null>> {
    if (isActive) {
      return reportAndReload(
        await requestAction(`/api/admin/stations/${id}/activate`, { method: 'POST' }),
      )
    }
    return reportAndReload(
      await requestAction(`/api/admin/stations/${id}/deactivate`, { method: 'POST' }),
    )
  }

  function listen(): () => void {
    const connection = useConnectionStore()
    const releases = [
      connection.registerRefetch(reload),
      useAdminEnrolmentStore().listen(() => {
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
    stations,
    loadFailed,
    load,
    loadAtTheFestival,
    create,
    save,
    addToTheFestival,
    removeFromTheFestival,
    setActive,
    listen,
  }
})
