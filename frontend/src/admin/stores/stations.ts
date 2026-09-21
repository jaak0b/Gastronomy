import { defineStore } from 'pinia'
import { ref } from 'vue'
import { request, requestAction } from '../../shared/api/client'
import { AdminStationListView, AdminStationView } from '../../shared/api/generatedSchemas'
import { adminFailed, adminOk, type AdminActionResult } from '../core/adminActionResult'
import { adminFailureFrom, reloadOrFailureOf } from '../core/adminMutation'
import { loadAdminList } from '../core/adminList'
import { createPendingCreatedEntities } from '../core/pendingCreatedEntities'
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
  const stations = ref<AdminStationView[]>([])
  const loadFailed = ref(false)
  const festivalInView = ref<string | null>(null)

  const stationsGate = createLatestRequestGate()
  const pendingCreatedStations = createPendingCreatedEntities<AdminStationView>(
    (station) => station.stationId,
  )

  async function loadStationsFrom(path: string): Promise<void> {
    await loadAdminList({
      path,
      schema: AdminStationListView,
      gate: stationsGate,
      itemsOf: (response) => response.stations,
      showItems: (loaded) => {
        stations.value = pendingCreatedStations.mergeInto(loaded)
      },
      setLoadFailed: (failed) => {
        loadFailed.value = failed
      },
    })
  }

  async function load(): Promise<void> {
    if (festivalInView.value !== null) {
      pendingCreatedStations.clear()
    }
    festivalInView.value = null
    await loadStationsFrom('/api/admin/stations')
  }

  async function loadAtTheFestival(festivalId: string): Promise<void> {
    if (festivalInView.value !== festivalId) {
      pendingCreatedStations.clear()
    }
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

  async function create(draft: StationDraft): Promise<AdminActionResult<AdminStationView>> {
    const scopeAtStart = festivalInView.value
    const result = await request('/api/admin/stations', {
      method: 'POST',
      body: { name: draft.name, sortOrder: draft.sortOrder },
      schema: AdminStationView,
    })
    if (result.kind !== 'ok') {
      return adminFailureFrom(result)
    }
    if (scopeAtStart === festivalInView.value) {
      pendingCreatedStations.remember(result.data)
      stations.value = pendingCreatedStations.mergeInto(stations.value)
    }
    return adminOk(result.data)
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
    return await reloadOrFailureOf(
      await requestAction(`/api/admin/stations/${station.stationId}`, {
        method: 'PUT',
        body: { name: station.name, sortOrder: station.sortOrder },
      }),
      reload,
    )
  }

  async function addToTheFestival(
    festivalId: string,
    stationId: string,
  ): Promise<AdminActionResult<null>> {
    return await reloadOrFailureOf(
      await requestAction(`/api/admin/festivals/${festivalId}/stations/${stationId}`, {
        method: 'PUT',
      }),
      reload,
    )
  }

  async function removeFromTheFestival(
    festivalId: string,
    stationId: string,
  ): Promise<AdminActionResult<null>> {
    return await reloadOrFailureOf(
      await requestAction(`/api/admin/festivals/${festivalId}/stations/${stationId}`, {
        method: 'DELETE',
      }),
      reload,
    )
  }

  async function setActive(id: string, isActive: boolean): Promise<AdminActionResult<null>> {
    if (isActive) {
      return await reloadOrFailureOf(
        await requestAction(`/api/admin/stations/${id}/activate`, { method: 'POST' }),
        reload,
      )
    }
    return await reloadOrFailureOf(
      await requestAction(`/api/admin/stations/${id}/deactivate`, { method: 'POST' }),
      reload,
    )
  }

  function listen(): () => void {
    const connection = useConnectionStore()
    const releases = [
      connection.registerRefetch(reload),
      connection.onEvent<unknown>('StationsChanged', () => {
        void reload()
      }),
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
