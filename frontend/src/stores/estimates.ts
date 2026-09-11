import { defineStore } from 'pinia'
import { ref } from 'vue'
import { request } from '../api/client'
import type { EstimatesResponse, StationEstimate } from '../core/apiTypes'
import { useConnectionStore } from './connection'
import { useSessionStore } from './session'

export const useEstimatesStore = defineStore('estimates', () => {
  const stations = ref<StationEstimate[]>([])
  const loadFailed = ref(false)

  async function load(): Promise<void> {
    const session = useSessionStore()
    if (session.deviceToken === null) {
      return
    }
    const result = await request<EstimatesResponse>('/api/estimates', {
      token: session.deviceToken,
    })
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
    ]
    return () => {
      for (const release of releases) {
        release()
      }
    }
  }

  return { stations, loadFailed, load, listen }
})
