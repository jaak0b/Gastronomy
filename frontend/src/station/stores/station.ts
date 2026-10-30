import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { request, type ApiResult } from '../../shared/api/client'
import {
  StationFulfilledView,
  StationOrderQueueView,
  StationQueueView,
  StationSummaryView,
  type StationItemSelectionRequest,
} from '../../shared/api/generatedSchemas'
import { showsTheEmptyListText } from '../../shared/core/emptyList'
import { createLatestRequestGate } from '../../shared/core/latestRequestGate'
import { retainStillSelectableItemIds, stationFailureMessage, type StationMessage } from '../../shared/core/stationBoard'
import { useConnectionStore } from '../../shared/stores/connection'
import { useSessionStore } from '../../shared/stores/session'

export const useStationStore = defineStore('station', () => {
  const identity = ref<StationSummaryView | null>(null)
  const orders = ref<StationOrderQueueView[]>([])
  const asItComes = ref<StationOrderQueueView[]>([])
  const fulfilled = ref<StationOrderQueueView[]>([])
  const selectedItemIds = ref<string[]>([])
  const hasLoaded = ref(false)
  const loadFailed = ref(false)
  const loadFailureKey = ref<string | null>(null)
  const failure = ref<StationMessage | null>(null)
  const isWorking = ref(false)
  const isShowingFulfilled = ref(false)
  const fulfilledHasLoaded = ref(false)
  const fulfilledLoadFailed = ref(false)

  const boardGate = createLatestRequestGate()
  const fulfilledGate = createLatestRequestGate()
  let newestActionToken = 0

  const hasNothingDone = computed(() =>
    showsTheEmptyListText(
      { hasLoaded: fulfilledHasLoaded.value, loadFailed: fulfilledLoadFailed.value },
      fulfilled.value.length,
    ),
  )

  function deviceToken(): string | null {
    return useSessionStore().deviceToken
  }

  function applyQueue(data: StationQueueView): void {
    identity.value = data.station
    selectedItemIds.value = retainStillSelectableItemIds(selectedItemIds.value, orders.value, data.orders)
    orders.value = data.orders
    asItComes.value = data.asItComes
    hasLoaded.value = true
  }

  async function load(): Promise<void> {
    if (deviceToken() === null) {
      return
    }
    const token = boardGate.startRequest()
    const result = await request('/api/station/orders', {
      token: deviceToken(),
      schema: StationQueueView,
    })
    if (!boardGate.isNewestRequest(token)) {
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
    const token = fulfilledGate.startRequest()
    const result = await request('/api/station/orders/fulfilled', {
      token: deviceToken(),
      schema: StationFulfilledView,
    })
    if (!fulfilledGate.isNewestRequest(token)) {
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
    failure.value = null
    selectedItemIds.value = selectedItemIds.value.includes(orderItemId)
      ? selectedItemIds.value.filter((selected) => selected !== orderItemId)
      : [...selectedItemIds.value, orderItemId]
  }

  async function runAction(send: () => Promise<ApiResult<StationQueueView>>): Promise<boolean> {
    failure.value = null
    isWorking.value = true
    const token = boardGate.startRequest()
    newestActionToken = token
    const result = await send()
    if (newestActionToken === token) {
      isWorking.value = false
    }
    if (!boardGate.isNewestRequest(token)) {
      return false
    }
    if (result.kind !== 'ok') {
      failure.value = stationFailureMessage(result)
      return false
    }
    applyQueue(result.data)
    return true
  }

  async function fulfill(orderItemIds: string[]): Promise<void> {
    await runAction(() =>
      request('/api/station/items/fulfill', {
        method: 'POST',
        body: { orderItemIds } satisfies StationItemSelectionRequest,
        token: deviceToken(),
        schema: StationQueueView,
      }),
    )
  }

  async function acknowledgeCancellation(orderItemIds: string[]): Promise<void> {
    await runAction(() =>
      request('/api/station/items/acknowledge-cancellation', {
        method: 'POST',
        body: { orderItemIds } satisfies StationItemSelectionRequest,
        token: deviceToken(),
        schema: StationQueueView,
      }),
    )
  }

  async function unfulfill(orderItemId: string): Promise<void> {
    const isApplied = await runAction(() =>
      request('/api/station/items/unfulfill', {
        method: 'POST',
        body: { orderItemIds: [orderItemId] } satisfies StationItemSelectionRequest,
        token: deviceToken(),
        schema: StationQueueView,
      }),
    )
    if (isApplied && isShowingFulfilled.value) {
      await loadFulfilled()
    }
  }

  async function hide(stationOrderId: string): Promise<void> {
    await runAction(() =>
      request(`/api/station/orders/${stationOrderId}/hide`, {
        method: 'POST',
        token: deviceToken(),
        schema: StationQueueView,
      }),
    )
  }

  function listen(): () => void {
    return useConnectionStore().listenToTheLaptop(['ConfigurationChanged', 'OrdersChanged'], refresh)
  }

  return {
    identity,
    orders,
    asItComes,
    fulfilled,
    selectedItemIds,
    hasLoaded,
    loadFailed,
    loadFailureKey,
    failure,
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
    acknowledgeCancellation,
    unfulfill,
    hide,
    listen,
  }
})
