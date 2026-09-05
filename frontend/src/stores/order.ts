import { defineStore } from 'pinia'
import { computed, ref, watch } from 'vue'
import { request } from '../api/client'
import type { DraftLine, DraftOrder, OrderSubmitResponse } from '../core/apiTypes'
import {
  addLine,
  clearDraft,
  removeLine,
  restoreDraft,
  setLineNote,
  setLineStation,
  setOrderNote,
  setTableName,
} from '../core/draftCart'
import { assertNever } from '../core/assertNever'
import { buildSubmitRequest, ensureClientOrderId } from '../core/submission'
import {
  buildBasketView,
  basketItemCount,
  refreshLineSnapshots,
  withoutLinesNoLongerOnTheMenu,
} from '../core/basket'
import { orderTotalCents } from '../core/totals'
import { messageForSendFailure, type SendFailureMessage } from '../core/sendFailure'
import { useCatalogStore } from './catalog'
import { useSessionStore } from './session'

export type SendState = 'idle' | 'sending' | 'failed' | 'accepted'

export const ARRIVAL_NOTICE_MS = 8000

export const useOrderStore = defineStore('order', () => {
  const draftWasLost = ref(false)

  function draftFromStorage(): DraftOrder {
    const restoration = restoreDraft()
    switch (restoration.outcome) {
      case 'nothingStored':
      case 'restored':
        return restoration.draft
      case 'unreadableDraftDiscarded':
        draftWasLost.value = true
        return restoration.draft
      default:
        return assertNever(restoration)
    }
  }

  const draft = ref(draftFromStorage())
  const sendState = ref<SendState>('idle')
  const failure = ref<SendFailureMessage | null>(null)
  const failedAttempts = ref(0)
  const acceptedOrderNumber = ref<number | null>(null)
  const settledOnSend = ref(false)
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
  const hasLinesNoLongerOnTheMenu = computed(() =>
    basketLines.value.some((line) => line.isNoLongerOnTheMenu),
  )

  function dismissDraftLoss(): void {
    draftWasLost.value = false
  }

  function addItem(line: DraftLine): void {
    dismissDraftLoss()
    draft.value = addLine(draft.value, line)
  }

  function dropLine(index: number): void {
    draft.value = removeLine(draft.value, index)
  }

  function dropLinesNoLongerOnTheMenu(): void {
    draft.value = withoutLinesNoLongerOnTheMenu(draft.value, catalogStore.catalog)
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
    draft.value = draftFromStorage()
  }

  async function send(settleOnSend: boolean): Promise<void> {
    const session = useSessionStore()
    sendState.value = 'sending'
    settledOnSend.value = settleOnSend
    draft.value = ensureClientOrderId(draft.value)
    const result = await request<OrderSubmitResponse>('/api/orders', {
      method: 'POST',
      body: buildSubmitRequest(draft.value, settleOnSend),
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
        return
      case 'unreachable':
      case 'error':
        failedAttempts.value += 1
        failure.value = messageForSendFailure(result, failedAttempts.value)
        sendState.value = 'failed'
        return
    }
  }

  async function sendAgain(): Promise<void> {
    await send(settledOnSend.value)
  }

  return {
    draft,
    draftWasLost,
    sendState,
    failure,
    failedAttempts,
    acceptedOrderNumber,
    settledOnSend,
    basketLines,
    itemCount,
    totalCents,
    hasLinesNoLongerOnTheMenu,
    addItem,
    dropLine,
    dropLinesNoLongerOnTheMenu,
    noteLine,
    chooseStation,
    setTable,
    setNote,
    dismissConfirmation,
    dismissDraftLoss,
    send,
    sendAgain,
  }
})
