import { defineStore } from 'pinia'
import { ref } from 'vue'
import { request, requestAction } from '../../shared/api/client'
import {
  AdminTablePlanView,
  SavedAreaView,
  SavedTableView,
  type AdminAreaView,
  type MoveDirection,
  type SaveAreaRequest,
  type SaveStationShapeRequest,
  type SaveTableRequest,
} from '../../shared/api/generatedSchemas'
import { createLatestRequestGate } from '../../shared/core/latestRequestGate'
import { useConnectionStore } from '../../shared/stores/connection'
import type { AdminActionResult } from '../core/adminActionResult'
import { reloadOrFailureOf } from '../core/adminMutation'
import { loadAdminList } from '../core/listLoading'
import { oneAtATime } from '../core/oneAtATime'

export const useAdminTablePlanStore = defineStore('adminTablePlan', () => {
  const areas = ref<AdminAreaView[]>([])
  const loadFailed = ref(false)
  const festivalInView = ref<string | null>(null)
  const loadedFestivalId = ref<string | null>(null)

  const tablePlanGate = createLatestRequestGate()
  const areaMoves = oneAtATime()

  async function loadForFestival(festivalId: string): Promise<void> {
    if (festivalInView.value !== festivalId) {
      areas.value = []
      loadedFestivalId.value = null
    }
    festivalInView.value = festivalId
    await loadAdminList({
      path: `/api/admin/festivals/${festivalId}/table-plan`,
      schema: AdminTablePlanView,
      gate: tablePlanGate,
      itemsOf: (response) => response.areas,
      showItems: (loaded) => {
        areas.value = loaded
        loadedFestivalId.value = festivalId
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
    direction: MoveDirection,
  ): Promise<AdminActionResult<null>> {
    return await areaMoves.run(async () =>
      reloadOrFailureOf(
        await requestAction(`/api/admin/festivals/${festivalId}/areas/${areaId}/move`, {
          method: 'POST',
          body: { direction },
        }),
        reload,
      ),
    )
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
    return useConnectionStore().listenToTheLaptop(['ConfigurationChanged', 'OrdersChanged'], reload)
  }

  return {
    areas,
    loadFailed,
    loadedFestivalId,
    loadForFestival,
    createArea,
    saveArea,
    moveArea,
    hideArea,
    showArea,
    createTable,
    saveTable,
    hideTable,
    showTable,
    placeStation,
    takeStationOffThePlan,
    listen,
  }
})
