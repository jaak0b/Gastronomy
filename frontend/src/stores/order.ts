import { defineStore } from 'pinia'
import { computed, ref, watch } from 'vue'
import { request } from '../api/client'
import type {
  DraftLine,
  OrderSubmitResponse,
  OrderSummary,
  TicketSummary,
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
  setTableLabel,
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

export const useOrderStore = defineStore('order', () => {
  const draft = ref(loadDraft())
  const orders = ref<OrderSummary[]>([])
  const sendState = ref<SendState>('idle')
  const failure = ref<SendFailureMessage | null>(null)
  const failedAttempts = ref(0)
  const acceptedOrderNumber = ref<number | null>(null)
  const acceptedTotalChangedTo = ref<number | null>(null)

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

  function chooseStation(index: number, locationId: string | null): void {
    draft.value = setLineStation(draft.value, index, locationId)
  }

  function setTable(tableLabel: string): void {
    draft.value = setTableLabel(draft.value, tableLabel)
  }

  function setNote(note: string | null): void {
    draft.value = setOrderNote(draft.value, note)
  }

  function dismissConfirmation(): void {
    sendState.value = 'idle'
    acceptedOrderNumber.value = null
    acceptedTotalChangedTo.value = null
  }

  function startNextOrder(): void {
    clearDraft()
    draft.value = loadDraft()
  }

  async function send(): Promise<void> {
    const session = useSessionStore()
    sendState.value = 'sending'
    draft.value = ensureClientOrderId(draft.value)
    const expectedTotalCents = totalCents.value
    const result = await request<OrderSubmitResponse>('/api/orders', {
      method: 'POST',
      body: buildSubmitRequest(draft.value, expectedTotalCents),
      token: session.deviceToken,
    })
    switch (result.kind) {
      case 'ok':
        acceptedOrderNumber.value = result.data.globalOrderNumber
        acceptedTotalChangedTo.value =
          result.data.totalCents === expectedTotalCents ? null : result.data.totalCents
        failure.value = null
        failedAttempts.value = 0
        sendState.value = 'accepted'
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
    ticketId: string,
    slipIsOnThePile: boolean,
  ): Promise<string | null> {
    const session = useSessionStore()
    const result = await request<TicketSummary>(
      `/api/orders/${orderId}/tickets/${ticketId}/resolve`,
      { method: 'POST', body: { slipIsOnThePile }, token: session.deviceToken },
    )
    switch (result.kind) {
      case 'ok':
        await loadMine()
        return null
      case 'error':
        return result.status === 409 ? 'ticket.unknown.answered' : 'review.sendFailed'
      case 'unreachable':
        return 'header.reconnecting'
    }
  }

  async function reprint(orderId: string, ticketId: string): Promise<void> {
    const session = useSessionStore()
    await request(`/api/orders/${orderId}/tickets/${ticketId}/reprint`, {
      method: 'POST',
      token: session.deviceToken,
    })
    await loadMine()
  }

  function applyTicketChange(payload: {
    orderId: string
    ticketId: string
    status: TicketSummary['status']
    failureReason: TicketSummary['failureReason']
    printerHasPaper: boolean | null
  }): void {
    const order = orderById(payload.orderId)
    if (order === null) {
      return
    }
    order.tickets = order.tickets.map((ticket) =>
      ticket.ticketId === payload.ticketId
        ? {
            ...ticket,
            status: payload.status,
            failureReason: payload.failureReason,
            printerHasPaper: payload.printerHasPaper,
          }
        : ticket,
    )
  }

  function listen(): void {
    const connection = useConnectionStore()
    connection.registerRefetch(loadMine)
    connection.onEvent('OrderAccepted', () => {
      void loadMine()
    })
    connection.onEvent<Parameters<typeof applyTicketChange>[0]>('TicketStatusChanged', (payload) => {
      applyTicketChange(payload)
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
    connection.onEvent('EventSessionStarted', () => {
      orders.value = []
    })
  }

  return {
    draft,
    orders,
    sendState,
    failure,
    failedAttempts,
    acceptedOrderNumber,
    acceptedTotalChangedTo,
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
    reprint,
    listen,
  }
})
