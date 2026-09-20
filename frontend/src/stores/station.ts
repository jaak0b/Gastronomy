import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { request } from '../api/client'
import { stationFulfilledResponseSchema, stationOrdersResponseSchema } from '../core/apiSchemas'
import type { StationIdentity, StationOrder, StationOrdersResponse } from '../core/apiTypes'
import { createLatestRequestGate } from '../core/latestRequestGate'
import { retainOpenItemIds, stationFailureKey } from '../core/stationBoard'
import { useConnectionStore } from './connection'
import { useSessionStore } from './session'

export const useStationStore = defineStore('station', () => {
  const station = ref<StationIdentity | null>(null)
  const orders = ref<StationOrder[]>([])
  const asItComes = ref<StationOrder[]>([])
  const fulfilled = ref<StationOrder[]>([])
  const selectedItemIds = ref<string[]>([])
  const loadFailed = ref(false)
  const loadFailureKey = ref<string | null>(null)
  const failureKey = ref<string | null>(null)
  const isWorking = ref(false)
  const isShowingFulfilled = ref(false)
  const fulfilledHasLoaded = ref(false)
  const fulfilledLoadFailed = ref(false)

  const boardGate = createLatestRequestGate()
  const fulfilledGate = createLatestRequestGate()
  let newestActionToken = 0

  const hasNothingDone = computed(
    () =>
      fulfilledHasLoaded.value && !fulfilledLoadFailed.value && fulfilled.value.length === 0,
  )

  function deviceToken(): string | null {
    return useSessionStore().deviceToken
  }

  function applyQueue(data: StationOrdersResponse): void {
    station.value = data.station
    orders.value = data.orders
    asItComes.value = data.asItComes
    selectedItemIds.value = retainOpenItemIds(selectedItemIds.value, data.orders)
  }

  async function load(): Promise<void> {
    if (deviceToken() === null) {
      return
    }
    const token = boardGate.start()
    const result = await request('/api/station/orders', {
      token: deviceToken(),
      schema: stationOrdersResponseSchema,
    })
    if (!boardGate.isCurrent(token)) {
      return
    }
    if (result.kind !== 'ok') {
      loadFailed.value = true
      loadFailureKey.value =
        result.kind === 'error' && result.body !== null ? result.body.messageKey : null
      return
    }
    loadFailed.value = false
    loadFailureKey.value = null
    applyQueue(result.data)
  }

  async function loadFulfilled(): Promise<void> {
    if (deviceToken() === null) {
      return
    }
    const token = fulfilledGate.start()
    const result = await request('/api/station/orders/fulfilled', {
      token: deviceToken(),
      schema: stationFulfilledResponseSchema,
    })
    if (!fulfilledGate.isCurrent(token)) {
      return
    }
    if (result.kind !== 'ok') {
      fulfilledLoadFailed.value = true
      return
    }
    fulfilledLoadFailed.value = false
    fulfilled.value = result.data.stationOrders
    fulfilledHasLoaded.value = true
  }

  async function refresh(): Promise<void> {
    await load()
    if (isShowingFulfilled.value) {
      await loadFulfilled()
    }
  }

  async function openFulfilled(): Promise<void> {
    isShowingFulfilled.value = true
    await loadFulfilled()
  }

  function closeFulfilled(): void {
    isShowingFulfilled.value = false
  }

  function toggleItemSelection(orderItemId: string): void {
    failureKey.value = null
    selectedItemIds.value = selectedItemIds.value.includes(orderItemId)
      ? selectedItemIds.value.filter((selected) => selected !== orderItemId)
      : [...selectedItemIds.value, orderItemId]
  }

  async function fulfill(orderItemIds: string[]): Promise<void> {
    failureKey.value = null
    isWorking.value = true
    const token = boardGate.start()
    newestActionToken = token
    const result = await request('/api/station/items/fulfill', {
      method: 'POST',
      body: { orderItemIds },
      token: deviceToken(),
      schema: stationOrdersResponseSchema,
    })
    if (newestActionToken === token) {
      isWorking.value = false
    }
    if (!boardGate.isCurrent(token)) {
      return
    }
    if (result.kind !== 'ok') {
      failureKey.value = stationFailureKey(result)
      return
    }
    applyQueue(result.data)
  }

  async function unfulfill(orderItemId: string): Promise<void> {
    failureKey.value = null
    isWorking.value = true
    const token = boardGate.start()
    newestActionToken = token
    const result = await request('/api/station/items/unfulfill', {
      method: 'POST',
      body: { orderItemIds: [orderItemId] },
      token: deviceToken(),
      schema: stationOrdersResponseSchema,
    })
    if (newestActionToken === token) {
      isWorking.value = false
    }
    if (!boardGate.isCurrent(token)) {
      return
    }
    if (result.kind !== 'ok') {
      failureKey.value = stationFailureKey(result)
      return
    }
    applyQueue(result.data)
    if (isShowingFulfilled.value) {
      await loadFulfilled()
    }
  }

  async function hide(stationOrderId: string): Promise<void> {
    failureKey.value = null
    isWorking.value = true
    const token = boardGate.start()
    newestActionToken = token
    const result = await request(`/api/station/orders/${stationOrderId}/hide`, {
      method: 'POST',
      token: deviceToken(),
      schema: stationOrdersResponseSchema,
    })
    if (newestActionToken === token) {
      isWorking.value = false
    }
    if (!boardGate.isCurrent(token)) {
      return
    }
    if (result.kind !== 'ok') {
      failureKey.value = stationFailureKey(result)
      return
    }
    applyQueue(result.data)
  }

  function listen(): () => void {
    const connection = useConnectionStore()
    const releases = [
      connection.registerRefetch(refresh),
      connection.onEvent<{ stationId: string }>('StationOrdersChanged', () => {
        void refresh()
      }),
      connection.onEvent<unknown>('StationsChanged', () => {
        void refresh()
      }),
      connection.onEvent<unknown>('FestivalChanged', () => {
        void refresh()
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
    orders,
    asItComes,
    fulfilled,
    selectedItemIds,
    loadFailed,
    loadFailureKey,
    failureKey,
    isWorking,
    isShowingFulfilled,
    fulfilledHasLoaded,
    fulfilledLoadFailed,
    hasNothingDone,
    load,
    loadFulfilled,
    openFulfilled,
    closeFulfilled,
    toggleItemSelection,
    fulfill,
    unfulfill,
    hide,
    listen,
  }
})
