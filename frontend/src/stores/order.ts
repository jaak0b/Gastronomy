import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { answerSaysTheDeviceIsNoLongerSetUp, request } from '../api/client'
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
  changesAreRefusedFor,
  paperIsTheOnlyWayLeft,
  progressAfterALoad,
  sendHasFailedIn,
  sendIsUnderWayIn,
  sendWasAcceptedIn,
  type SendProgress,
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
import { messageForSendFailure, type SendFailureMessage } from '../core/sendFailure'
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
  const anAttemptWentUnanswered = ref(progressWhenTheAppLoaded.anAttemptWentUnanswered)
  const acceptedOrderNumber = ref<number | null>(null)
  let arrivalNoticeTimer: ReturnType<typeof setTimeout> | null = null

  const catalogStore = useCatalogStore()

  function whatTheSendHasComeTo(): SendProgress {
    return {
      state: sendState.value,
      attempts: attemptsMade.value,
      settleOnSend: settledOnSend.value,
      anAttemptWentUnanswered: anAttemptWentUnanswered.value,
      failure: failure.value,
    }
  }

  const isSending = computed(() => sendIsUnderWayIn(sendState.value))
  const sendHasFailed = computed(() => sendHasFailedIn(sendState.value))
  const changesAreRefused = computed(() => changesAreRefusedFor(whatTheSendHasComeTo()))
  const onlyPaperIsLeft = computed(() => paperIsTheOnlyWayLeft(whatTheSendHasComeTo()))

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

  function nameOfTheStation(stationId: string | null): string {
    return stationId === null ? '' : catalogStore.stationName(stationId)
  }

  function addItem(line: Omit<DraftLine, 'stationName'>): void {
    if (changesAreRefused.value) {
      return
    }
    dismissDraftLoss()
    draft.value = addLine(draft.value, { ...line, stationName: nameOfTheStation(line.stationId) })
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
    change((current) => setLineStation(current, index, stationId, nameOfTheStation(stationId)))
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
    anAttemptWentUnanswered.value = false
    sendState.value = 'idle'
    startNextOrder()
  }

  function rememberWhatBecameOfTheSend(): void {
    saveSendProgress(whatTheSendHasComeTo())
  }

  function putTheSendBackTo(progress: SendProgress): void {
    sendState.value = progress.state
    attemptsMade.value = progress.attempts
    settledOnSend.value = progress.settleOnSend
    anAttemptWentUnanswered.value = progress.anAttemptWentUnanswered
    failure.value = progress.failure
    rememberWhatBecameOfTheSend()
  }

  async function send(settleOnSend: boolean): Promise<void> {
    const session = useSessionStore()
    const sendBeforeThisAttempt = whatTheSendHasComeTo()
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
        anAttemptWentUnanswered.value = false
        sendState.value = 'accepted'
        arrivalNoticeTimer = setTimeout(dismissConfirmation, ARRIVAL_NOTICE_MS)
        startNextOrder()
        return
      case 'unreachable':
        failure.value = messageForSendFailure(result)
        anAttemptWentUnanswered.value = true
        sendState.value = 'failed'
        rememberWhatBecameOfTheSend()
        return
      case 'error':
        if (answerSaysTheDeviceIsNoLongerSetUp(result)) {
          putTheSendBackTo(sendBeforeThisAttempt)
          return
        }
        failure.value = messageForSendFailure(result)
        attemptsMade.value = sendBeforeThisAttempt.attempts
        sendState.value = 'rejected'
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
    onlyPaperIsLeft,
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
