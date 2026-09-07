import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { request } from '../api/client'
import type {
  StationIdentity,
  StationItemStatusResponse,
  StationOrdersResponse,
  StationSlice,
} from '../core/apiTypes'
import { assertNever } from '../core/assertNever'
import {
  mergeSlices,
  splitSlices,
  stationFailureKey,
  type ProductionAdvance,
} from '../core/stationBoard'
import { useConnectionStore } from './connection'
import { useSessionStore } from './session'

export const useStationStore = defineStore('station', () => {
  const station = ref<StationIdentity | null>(null)
  const slices = ref<StationSlice[]>([])
  const hasLoaded = ref(false)
  const loadFailed = ref(false)
  const failureKey = ref<string | null>(null)
  const readyTableName = ref<string | null>(null)
  const isWorking = ref(false)

  const board = computed(() => splitSlices(slices.value))
  const hasNothingToPrepare = computed(
    () =>
      hasLoaded.value
      && !loadFailed.value
      && board.value.together.length === 0
      && board.value.single.length === 0,
  )

  function deviceToken(): string | null {
    return useSessionStore().deviceToken
  }

  async function load(): Promise<void> {
    if (deviceToken() === null) {
      return
    }
    const result = await request<StationOrdersResponse>('/api/station/orders', {
      token: deviceToken(),
    })
    if (result.kind !== 'ok') {
      loadFailed.value = true
      return
    }
    loadFailed.value = false
    station.value = result.data.station
    slices.value = result.data.slices
    hasLoaded.value = true
  }

  function dismissReadyNotice(): void {
    readyTableName.value = null
  }

  async function advance(orderItemIds: string[], status: ProductionAdvance): Promise<void> {
    failureKey.value = null
    readyTableName.value = null
    isWorking.value = true
    const result = await request<StationItemStatusResponse>('/api/station/items/status', {
      method: 'POST',
      body: { orderItemIds, status },
      token: deviceToken(),
    })
    isWorking.value = false
    if (result.kind !== 'ok') {
      failureKey.value = stationFailureKey(result)
      return
    }
    slices.value = mergeSlices(slices.value, result.data.slices)
    switch (status) {
      case 'finished':
        readyTableName.value = result.data.tableName ?? null
        return
      case 'inProduction':
        return
      default:
        assertNever(status)
    }
  }

  function listen(): () => void {
    const connection = useConnectionStore()
    const releases = [
      connection.registerRefetch(load),
      connection.onEvent<{ stationId: string }>('StationOrdersChanged', () => {
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

  return {
    station,
    slices,
    board,
    hasNothingToPrepare,
    loadFailed,
    failureKey,
    readyTableName,
    isWorking,
    load,
    advance,
    dismissReadyNotice,
    listen,
  }
})
