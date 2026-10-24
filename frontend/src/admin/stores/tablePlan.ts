import { defineStore } from 'pinia'
import { ref } from 'vue'
import { request, requestAction } from '../../shared/api/client'
import {
  AdminTablePlanView,
  SavedAreaView,
  SavedTableView,
  type AdminAreaView,
  type SaveAreaRequest,
  type SaveStationShapeRequest,
  type SaveTableRequest,
} from '../../shared/api/generatedSchemas'
import { createLatestRequestGate } from '../../shared/core/latestRequestGate'
import { useConnectionStore } from '../../shared/stores/connection'
import { adminOk, type AdminActionResult } from '../core/adminActionResult'
import { adminFailureFrom, reloadOrFailureOf } from '../core/adminMutation'
import { sortOrderChangesForMoving, type AreaMove } from '../core/areaOrder'
import { loadAdminList } from '../core/listLoading'

export const useAdminTablePlanStore = defineStore('adminTablePlan', () => {
  const areas = ref<AdminAreaView[]>([])
  const loadFailed = ref(false)
  const festivalInView = ref<string | null>(null)

  const tablePlanGate = createLatestRequestGate()

  async function loadForFestival(festivalId: string): Promise<void> {
    if (festivalInView.value !== festivalId) {
      areas.value = []
    }
    festivalInView.value = festivalId
    await loadAdminList({
      path: `/api/admin/festivals/${festivalId}/table-plan`,
      schema: AdminTablePlanView,
      gate: tablePlanGate,
      itemsOf: (response) => response.areas,
      showItems: (loaded) => {
        areas.value = loaded
      },
      setLoadFailed: (failed) => {
        loadFailed.value = failed
      },
    })
  }

  async function reload(): Promise<void> {
    const festivalId = festivalInView.value
    if (festivalId !== null) {
      await loadForFestival(festivalId)
    }
  }

  async function createArea(
    festivalId: string,
    area: SaveAreaRequest,
  ): Promise<AdminActionResult<SavedAreaView>> {
    return await reloadOrFailureOf(
      await request(`/api/admin/festivals/${festivalId}/areas`, {
        method: 'POST',
        body: area,
        schema: SavedAreaView,
      }),
      reload,
    )
  }

  async function saveArea(
    festivalId: string,
    areaId: string,
    area: SaveAreaRequest,
  ): Promise<AdminActionResult<null>> {
    return await reloadOrFailureOf(
      await requestAction(`/api/admin/festivals/${festivalId}/areas/${areaId}`, {
        method: 'PUT',
        body: area,
      }),
      reload,
    )
  }

  async function moveArea(
    festivalId: string,
    areaId: string,
    move: AreaMove,
  ): Promise<AdminActionResult<null>> {
    for (const change of sortOrderChangesForMoving(areas.value, areaId, move)) {
      const body: SaveAreaRequest = { name: change.area.name, sortOrder: change.sortOrder }
      const saved = await requestAction(
        `/api/admin/festivals/${festivalId}/areas/${change.area.areaId}`,
        { method: 'PUT', body },
      )
      if (saved.kind !== 'ok') {
        await reload()
        return adminFailureFrom(saved)
      }
    }
    await reload()
    return adminOk(null)
  }

  async function hideArea(festivalId: string, areaId: string): Promise<AdminActionResult<null>> {
    return await reloadOrFailureOf(
      await requestAction(`/api/admin/festivals/${festivalId}/areas/${areaId}/hide`, {
        method: 'POST',
      }),
      reload,
    )
  }

  async function showArea(festivalId: string, areaId: string): Promise<AdminActionResult<null>> {
    return await reloadOrFailureOf(
      await requestAction(`/api/admin/festivals/${festivalId}/areas/${areaId}/show`, {
        method: 'POST',
      }),
      reload,
    )
  }

  async function deleteArea(festivalId: string, areaId: string): Promise<AdminActionResult<null>> {
    return await reloadOrFailureOf(
      await requestAction(`/api/admin/festivals/${festivalId}/areas/${areaId}`, {
        method: 'DELETE',
      }),
      reload,
    )
  }

  async function createTable(
    festivalId: string,
    table: SaveTableRequest,
  ): Promise<AdminActionResult<SavedTableView>> {
    return await reloadOrFailureOf(
      await request(`/api/admin/festivals/${festivalId}/tables`, {
        method: 'POST',
        body: table,
        schema: SavedTableView,
      }),
      reload,
    )
  }

  async function saveTable(
    festivalId: string,
    tableId: string,
    table: SaveTableRequest,
  ): Promise<AdminActionResult<null>> {
    return await reloadOrFailureOf(
      await requestAction(`/api/admin/festivals/${festivalId}/tables/${tableId}`, {
        method: 'PUT',
        body: table,
      }),
      reload,
    )
  }

  async function hideTable(festivalId: string, tableId: string): Promise<AdminActionResult<null>> {
    return await reloadOrFailureOf(
      await requestAction(`/api/admin/festivals/${festivalId}/tables/${tableId}/hide`, {
        method: 'POST',
      }),
      reload,
    )
  }

  async function showTable(festivalId: string, tableId: string): Promise<AdminActionResult<null>> {
    return await reloadOrFailureOf(
      await requestAction(`/api/admin/festivals/${festivalId}/tables/${tableId}/show`, {
        method: 'POST',
      }),
      reload,
    )
  }

  async function deleteTable(festivalId: string, tableId: string): Promise<AdminActionResult<null>> {
    return await reloadOrFailureOf(
      await requestAction(`/api/admin/festivals/${festivalId}/tables/${tableId}`, {
        method: 'DELETE',
      }),
      reload,
    )
  }

  async function placeStation(
    festivalId: string,
    stationId: string,
    shape: SaveStationShapeRequest,
  ): Promise<AdminActionResult<null>> {
    return await reloadOrFailureOf(
      await requestAction(`/api/admin/festivals/${festivalId}/stations/${stationId}/shape`, {
        method: 'PUT',
        body: shape,
      }),
      reload,
    )
  }

  async function takeStationOffThePlan(
    festivalId: string,
    stationId: string,
  ): Promise<AdminActionResult<null>> {
    return await reloadOrFailureOf(
      await requestAction(`/api/admin/festivals/${festivalId}/stations/${stationId}/shape`, {
        method: 'DELETE',
      }),
      reload,
    )
  }

  function listen(): () => void {
    return useConnectionStore().listenToTheLaptop(['ConfigurationChanged'], reload)
  }

  return {
    areas,
    loadFailed,
    loadForFestival,
    createArea,
    saveArea,
    moveArea,
    hideArea,
    showArea,
    deleteArea,
    createTable,
    saveTable,
    hideTable,
    showTable,
    deleteTable,
    placeStation,
    takeStationOffThePlan,
    listen,
  }
})
