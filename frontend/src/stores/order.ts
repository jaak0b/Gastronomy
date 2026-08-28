import { defineStore } from 'pinia'
import { computed, ref, watch } from 'vue'
import { request } from '../api/client'
import type {
  DraftLine,
  OrderSubmitResponse,
  OrderSummary,
  StationOrderSummary,
} from '../core/apiTypes'
import {
  addLine,
  clearDraft,
  loadDraft,
  removeLine,
  setLineNote,
  setLineQuantity,
  setLineStation,
  setOrderNote,
  setTableName,
} from '../core/draftCart'
import { buildSubmitRequest, ensureClientOrderId } from '../core/submission'
import { buildBasketView, basketItemCount, refreshLineSnapshots } from '../core/basket'
import { orderTotalCents } from '../core/totals'
import { messageForSendFailure, type SendFailureMessage } from '../core/sendFailure'
import { presentOrderState } from '../core/orderStateMachine'
import { useCatalogStore } from './catalog'
import { useConnectionStore } from './connection'
import { useSessionStore } from './session'

export type SendState = 'idle' | 'sending' | 'failed' | 'accepted'

export const ARRIVAL_NOTICE_MS = 8000

export const useOrderStore = defineStore('order', () => {
  const draft = ref(loadDraft())
  const orders = ref<OrderSummary[]>([])
  const sendState = ref<SendState>('idle')
  const failure = ref<SendFailureMessage | null>(null)
  const failedAttempts = ref(0)
  const acceptedOrderNumber = ref<number | null>(null)
  let arrivalNoticeTimer: ReturnType<typeof setTimeout> | null = null

  const catalogStore = useCatalogStore()

  watch(
    () => catalogStore.catalog,
    (pushed) => {
      draft.value = refreshLineSnapshots(draft.value, pushed)
    },
  )

  const basketLines = computed(() => buildBasketView(draft.value, catalogStore.catalog))
  const itemCount = computed(() => basketItemCount(draft.value))
  const totalCents = computed(() => orderTotalCents(basketLines.value))
  const attentionCount = computed(
    () => orders.value.filter((order) => presentOrderState(order) === 'NeedsAttention').length,
  )

  function addItem(line: DraftLine): void {
    draft.value = addLine(draft.value, line)
  }

  function changeQuantity(index: number, quantity: number): void {
    draft.value = setLineQuantity(draft.value, index, quantity)
  }

  function dropLine(index: number): void {
    draft.value = removeLine(draft.value, index)
  }

  function noteLine(index: number, note: string | null): void {
    draft.value = setLineNote(draft.value, index, note)
  }

  function chooseStation(index: number, stationId: string | null): void {
    draft.value = setLineStation(draft.value, index, stationId)
  }

  function setTable(tableName: string): void {
    draft.value = setTableName(draft.value, tableName)
  }

  function setNote(note: string | null): void {
    draft.value = setOrderNote(draft.value, note)
  }

  function dismissConfirmation(): void {
    if (arrivalNoticeTimer !== null) {
      clearTimeout(arrivalNoticeTimer)
      arrivalNoticeTimer = null
    }
    sendState.value = 'idle'
    acceptedOrderNumber.value = null
  }

  function startNextOrder(): void {
    clearDraft()
    draft.value = loadDraft()
  }

  async function send(): Promise<void> {
    const session = useSessionStore()
    sendState.value = 'sending'
    draft.value = ensureClientOrderId(draft.value)
    const result = await request<OrderSubmitResponse>('/api/orders', {
      method: 'POST',
      body: buildSubmitRequest(draft.value),
      token: session.deviceToken,
    })
    switch (result.kind) {
      case 'ok':
        acceptedOrderNumber.value = result.data.globalOrderNumber
        failure.value = null
        failedAttempts.value = 0
        sendState.value = 'accepted'
        arrivalNoticeTimer = setTimeout(dismissConfirmation, ARRIVAL_NOTICE_MS)
        startNextOrder()
        await loadMine()
        return
      case 'unreachable':
      case 'error':
        failedAttempts.value += 1
        failure.value = messageForSendFailure(result, failedAttempts.value)
        sendState.value = 'failed'
        return
    }
  }

  async function loadMine(): Promise<void> {
    const session = useSessionStore()
    if (session.deviceToken === null) {
      return
    }
    const result = await request<{ orders: OrderSummary[] }>('/api/orders/mine', {
      token: session.deviceToken,
    })
    if (result.kind === 'ok') {
      orders.value = result.data.orders
    }
  }

  function orderById(orderId: string): OrderSummary | null {
    return orders.value.find((order) => order.orderId === orderId) ?? null
  }

  async function answerUnknown(
    orderId: string,
    stationOrderId: string,
    slipIsOnThePile: boolean,
  ): Promise<string | null> {
    const session = useSessionStore()
    const result = await request<StationOrderSummary>(
      `/api/orders/${orderId}/station-orders/${stationOrderId}/resolve`,
      { method: 'POST', body: { slipIsOnThePile }, token: session.deviceToken },
    )
    switch (result.kind) {
      case 'ok':
        await loadMine()
        return null
      case 'error':
        return result.status === 409 ? 'printJob.unknown.answered' : 'review.sendFailed'
      case 'unreachable':
        return 'header.reconnecting'
    }
  }

  async function printAnotherCopy(orderId: string, stationOrderId: string): Promise<void> {
    const session = useSessionStore()
    await request(`/api/orders/${orderId}/station-orders/${stationOrderId}/print-another-copy`, {
      method: 'POST',
      token: session.deviceToken,
    })
    await loadMine()
  }

  function applyPrintJobChange(payload: {
    orderId: string
    stationOrderId: string
    status: StationOrderSummary['status']
    failureReason: StationOrderSummary['failureReason']
    printerHasPaper: boolean | null
  }): void {
    const order = orderById(payload.orderId)
    if (order === null) {
      return
    }
    order.stationOrders = order.stationOrders.map((stationOrder) =>
      stationOrder.stationOrderId === payload.stationOrderId
        ? {
            ...stationOrder,
            status: payload.status,
            failureReason: payload.failureReason,
            printerHasPaper: payload.printerHasPaper,
          }
        : stationOrder,
    )
  }

  function listen(): void {
    const connection = useConnectionStore()
    connection.registerRefetch(loadMine)
    connection.onEvent('OrderAccepted', () => {
      void loadMine()
    })
    connection.onEvent<Parameters<typeof applyPrintJobChange>[0]>('PrintJobStatusChanged', (payload) => {
      applyPrintJobChange(payload)
    })
    connection.onEvent<{ orderId: string; status: OrderSummary['status'] }>(
      'OrderStatusChanged',
      (payload) => {
        const order = orderById(payload.orderId)
        if (order !== null) {
          order.status = payload.status
        }
      },
    )
  }

  return {
    draft,
    orders,
    sendState,
    failure,
    failedAttempts,
    acceptedOrderNumber,
    basketLines,
    itemCount,
    totalCents,
    attentionCount,
    addItem,
    changeQuantity,
    dropLine,
    noteLine,
    chooseStation,
    setTable,
    setNote,
    dismissConfirmation,
    send,
    loadMine,
    orderById,
    answerUnknown,
    printAnotherCopy,
    listen,
  }
})
