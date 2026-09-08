import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { request } from '../api/client'
import type {
  DeliveryMode,
  DraftLine,
  DraftOrder,
  OrderSubmitResponse,
} from '../core/apiTypes'
import {
  addLine,
  clearDraft,
  removeLine,
  restoreDraft,
  restoreSendProgress,
  saveSendProgress,
  setDeliveryMode,
  setLineNote,
  setLineStation,
  setOrderNote,
  setTableName,
} from '../core/draftCart'
import {
  changesAreRefusedIn,
  paperIsTheOnlyWayLeft,
  progressAfterALoad,
  sendHasFailedIn,
  sendIsUnderWayIn,
  sendWasAcceptedIn,
  type SendState,
} from '../core/sendProgress'
import { assertNever } from '../core/assertNever'
import { buildSubmitRequest, ensureClientOrderId } from '../core/submission'
import { chosenDeliveryMode, deliveryModesOf, orderSlices } from '../core/orderSlices'
import {
  buildBasketView,
  basketItemCount,
  lineCannotBeOrdered,
  withoutLinesThatCannotBeOrdered,
} from '../core/basket'
import { orderTotalCents } from '../core/totals'
import {
  messageForSendFailure,
  theLaptopNamedAReason,
  type SendFailureMessage,
} from '../core/sendFailure'
import { useCatalogStore } from './catalog'
import { useSessionStore } from './session'

export const ARRIVAL_NOTICE_MS = 8000

export const SEND_TIMEOUT_MS = 10000

export const useOrderStore = defineStore('order', () => {
  const draftWasLost = ref(false)

  function draftRestoredFromStorage(): DraftOrder {
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

  function draftFromStorage(): DraftOrder {
    return ensureClientOrderId(draftRestoredFromStorage())
  }

  const draft = ref(draftFromStorage())
  const progressWhenTheAppLoaded = progressAfterALoad(restoreSendProgress())
  const sendState = ref<SendState>(progressWhenTheAppLoaded.state)
  const failure = ref<SendFailureMessage | null>(progressWhenTheAppLoaded.failure)
  const attemptsMade = ref(progressWhenTheAppLoaded.attempts)
  const settledOnSend = ref(progressWhenTheAppLoaded.settleOnSend)
  const acceptedOrderNumber = ref<number | null>(null)
  let arrivalNoticeTimer: ReturnType<typeof setTimeout> | null = null

  const catalogStore = useCatalogStore()

  const isSending = computed(() => sendIsUnderWayIn(sendState.value))
  const sendHasFailed = computed(() => sendHasFailedIn(sendState.value))
  const changesAreRefused = computed(() => changesAreRefusedIn(sendState.value))
  const sendingFailedTwice = computed(() =>
    paperIsTheOnlyWayLeft(sendState.value, attemptsMade.value),
  )

  function theRefusalNoLongerFitsTheOrder(): void {
    if (failure.value === null) {
      return
    }
    failure.value = null
    sendState.value = 'idle'
    rememberWhatBecameOfTheSend()
  }

  function change(makeTheChange: (current: DraftOrder) => DraftOrder): void {
    if (changesAreRefused.value) {
      return
    }
    draft.value = makeTheChange(draft.value)
    theRefusalNoLongerFitsTheOrder()
  }

  const basketLines = computed(() => buildBasketView(draft.value, catalogStore.catalog))
  const slices = computed(() => orderSlices(basketLines.value))
  const itemCount = computed(() => basketItemCount(draft.value))
  const totalCents = computed(() => orderTotalCents(basketLines.value))
  const hasLinesThatCannotBeOrdered = computed(() =>
    basketLines.value.some(lineCannotBeOrdered),
  )

  function dismissDraftLoss(): void {
    draftWasLost.value = false
  }

  function addItem(line: DraftLine): void {
    if (changesAreRefused.value) {
      return
    }
    dismissDraftLoss()
    draft.value = addLine(draft.value, line)
    theRefusalNoLongerFitsTheOrder()
  }

  function dropLine(index: number): void {
    change((current) => removeLine(current, index))
  }

  function dropLinesThatCannotBeOrdered(): void {
    change((current) => withoutLinesThatCannotBeOrdered(current, catalogStore.catalog))
  }

  function noteLine(index: number, note: string | null): void {
    change((current) => setLineNote(current, index, note))
  }

  function chooseStation(index: number, stationId: string | null): void {
    change((current) => setLineStation(current, index, stationId))
  }

  function setTable(tableName: string): void {
    change((current) => setTableName(current, tableName))
  }

  function setNote(note: string | null): void {
    change((current) => setOrderNote(current, note))
  }

  function deliveryModeAt(stationId: string): DeliveryMode {
    return chosenDeliveryMode(draft.value.deliveryModes, stationId)
  }

  function chooseDeliveryMode(stationId: string, deliveryMode: DeliveryMode): void {
    change((current) => setDeliveryMode(current, stationId, deliveryMode))
  }

  function dismissConfirmation(): void {
    if (!sendWasAcceptedIn(sendState.value)) {
      return
    }
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

  function startNextOrderAfterWritingItDown(): void {
    failure.value = null
    attemptsMade.value = 0
    sendState.value = 'idle'
    startNextOrder()
  }

  function rememberWhatBecameOfTheSend(): void {
    saveSendProgress({
      state: sendState.value,
      attempts: attemptsMade.value,
      settleOnSend: settledOnSend.value,
      failure: failure.value,
    })
  }

  async function send(settleOnSend: boolean): Promise<void> {
    const session = useSessionStore()
    const attemptsBeforeThisOne = attemptsMade.value
    settledOnSend.value = settleOnSend
    attemptsMade.value += 1
    failure.value = null
    sendState.value = 'sending'
    rememberWhatBecameOfTheSend()
    const result = await request<OrderSubmitResponse>('/api/orders', {
      method: 'POST',
      body: buildSubmitRequest(
        draft.value,
        catalogStore.catalog,
        settleOnSend,
        deliveryModesOf(slices.value, draft.value.deliveryModes),
      ),
      token: session.deviceToken,
      timeoutMs: SEND_TIMEOUT_MS,
    })
    switch (result.kind) {
      case 'ok':
        acceptedOrderNumber.value = result.data.globalOrderNumber
        failure.value = null
        attemptsMade.value = 0
        sendState.value = 'accepted'
        arrivalNoticeTimer = setTimeout(dismissConfirmation, ARRIVAL_NOTICE_MS)
        startNextOrder()
        return
      case 'unreachable':
        failure.value = messageForSendFailure(result)
        sendState.value = 'failed'
        rememberWhatBecameOfTheSend()
        return
      case 'error':
        failure.value = messageForSendFailure(result)
        if (theLaptopNamedAReason(result.body)) {
          attemptsMade.value = attemptsBeforeThisOne
          sendState.value = 'rejected'
        } else {
          sendState.value = 'failed'
        }
        rememberWhatBecameOfTheSend()
        return
      default:
        return assertNever(result)
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
    attemptsMade,
    acceptedOrderNumber,
    settledOnSend,
    basketLines,
    slices,
    itemCount,
    totalCents,
    hasLinesThatCannotBeOrdered,
    isSending,
    sendHasFailed,
    changesAreRefused,
    sendingFailedTwice,
    addItem,
    dropLine,
    dropLinesThatCannotBeOrdered,
    noteLine,
    chooseStation,
    setTable,
    setNote,
    deliveryModeAt,
    chooseDeliveryMode,
    dismissConfirmation,
    dismissDraftLoss,
    startNextOrderAfterWritingItDown,
    send,
    sendAgain,
  }
})
