import { defineStore } from 'pinia'
import { ref } from 'vue'
import { request } from '../api/client'
import { estimatesSchema } from '../core/apiSchemas'
import type { StationEstimate } from '../core/apiTypes'
import { createLatestRequestGate } from '../core/latestRequestGate'
import { useConnectionStore } from './connection'
import { useSessionStore } from './session'

export const useEstimatesStore = defineStore('estimates', () => {
  const stations = ref<StationEstimate[]>([])
  const loadFailed = ref(false)

  const loadGate = createLatestRequestGate()

  async function load(): Promise<void> {
    const session = useSessionStore()
    if (session.deviceToken === null) {
      return
    }
    const token = loadGate.start()
    const result = await request('/api/estimates', {
      token: session.deviceToken,
      schema: estimatesSchema,
    })
    if (!loadGate.isCurrent(token)) {
      return
    }
    if (result.kind !== 'ok') {
      loadFailed.value = true
      return
    }
    loadFailed.value = false
    stations.value = result.data.stations
  }

  function listen(): () => void {
    const connection = useConnectionStore()
    const releases = [
      connection.registerRefetch(load),
      connection.onEvent<{ stationId: string }>('StationOrdersChanged', () => {
        void load()
      }),
      connection.onEvent<unknown>('OrderStatusChanged', () => {
        void load()
      }),
      connection.onEvent<unknown>('CatalogChanged', () => {
        void load()
      }),
      connection.onEvent<unknown>('StationsChanged', () => {
        void load()
      }),
      connection.onEvent<unknown>('FestivalChanged', () => {
        void load()
      }),
    ]
    return () => {
      for (const release of releases) {
        release()
      }
    }
  }

  return { stations, loadFailed, load, listen }
})
