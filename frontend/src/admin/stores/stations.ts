import { defineStore } from 'pinia'
import { requestAction } from '../../shared/api/client'
import {
  AdminStationListView,
  AdminStationView,
  type SaveStationRequest,
} from '../../shared/api/generatedSchemas'
import type { AdminActionResult } from '../core/adminActionResult'
import { reloadOrFailureOf } from '../core/adminMutation'
import { useAdminLoadStateStore } from './adminLoadState'
import { combineReleases } from '../../shared/core/combineReleases'
import { useAdminEnrolmentStore } from './enrolment'
import { defineFestivalScopedList } from './festivalScopedList'

export interface StationDraft {
  stationId?: string
  name: string
  sortOrder: number
}

export const useAdminStationsStore = defineStore('adminStations', () => {
  const {
    entries: stations,
    hasLoaded,
    loadedFestivalId,
    loadFailed,
    load,
    loadAtTheFestival,
    reload,
    create,
    save,
    setActive,
  } = defineFestivalScopedList({
    path: '/api/admin/stations',
    listSchema: AdminStationListView,
    entrySchema: AdminStationView,
    entriesOf: (response) => response.stations,
    idOf: (station) => station.stationId,
    draftIdOf: (station: StationDraft) => station.stationId,
    requestBodyOf: (station): SaveStationRequest => ({ name: station.name, sortOrder: station.sortOrder }),
  })

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

  function listen(): () => void {
    return combineReleases(
      useAdminLoadStateStore().listenToTheLaptop(
        ['ConfigurationChanged', 'OrdersChanged'],
        reload,
        loadFailed,
      ),
      useAdminEnrolmentStore().listen(() => {
        void reload()
      }),
    )
  }

  return {
    stations,
    hasLoaded,
    loadedFestivalId,
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
