import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { request } from '../../shared/api/client'
import { TablePlanAreaView, TablePlanView } from '../../shared/api/generatedSchemas'
import { createLatestRequestGate } from '../../shared/core/latestRequestGate'
import { closeStep, openStep, openStepValue } from '../../shared/router/router'
import { useConnectionStore } from '../../shared/stores/connection'
import { useSessionStore } from '../../shared/stores/session'

const AREA_STEP = 'area'

export const useTablePlanStore = defineStore('tablePlan', () => {
  const areas = ref<TablePlanAreaView[]>([])
  const hasLoaded = ref(false)
  const loadFailed = ref(false)
  const openedAreaId = computed(() => openStepValue(AREA_STEP))

  const loadGate = createLatestRequestGate()

  async function load(): Promise<void> {
    const session = useSessionStore()
    if (session.deviceToken === null) {
      return
    }
    const token = loadGate.startRequest()
    const result = await request('/api/table-plan', {
      token: session.deviceToken,
      schema: TablePlanView,
    })
    if (!loadGate.isNewestRequest(token)) {
      return
    }
    loadFailed.value = result.kind !== 'ok'
    if (result.kind !== 'ok') {
      return
    }
    areas.value = result.data.areas
    hasLoaded.value = true
  }

  function listen(): () => void {
    return useConnectionStore().listenToTheLaptop(['ConfigurationChanged', 'OrdersChanged'], load)
  }

  function openArea(areaId: string): void {
    openStep({ name: AREA_STEP, value: areaId, kind: 'level' })
  }

  function closeArea(): void {
    closeStep(AREA_STEP)
  }

  return { areas, hasLoaded, loadFailed, openedAreaId, load, listen, openArea, closeArea }
})
