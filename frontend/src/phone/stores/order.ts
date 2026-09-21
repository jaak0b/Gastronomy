import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { answerIsABusinessRefusal, answerSaysTheDeviceIsNoLongerSetUp, request } from '../../shared/api/client'
import type { DraftLine, DraftOrder } from '../core/draftCart'
import type { ConfirmedSettlement } from '../core/submission'
import { DeliveryMode, PlaceOrderRequest, PlacedOrderView } from '../../shared/api/generatedSchemas'
import {

  addLine,
  clearDraft,
  draftIsForAnotherFestival,
  removeLine,
  restoreDraft,
  restoreSendProgress,
  saveSendProgress,
  setDeliveryMode,
  setLineNote,
  setLineStation,
  setTableName,
  stampFestival,
} from '../core/draftCart'
import {

  changesAreRefusedFor,
  writingItDownIsTheOnlyWayLeft,
  progressAfterALoad,
  sendHasFailedIn,
  sendIsUnderWayIn,
  sendWasAcceptedIn,
  type SendProgress,
  type SendState,
} from '../core/sendProgress'
import { assertNever } from '../../shared/core/assertNever'
import { buildSubmitRequest, withClientOrderIdIfMissing } from '../core/submission'
import { buildStationDeliveryModes, buildStationOrders, deliveryModeChosenOrDefault } from '../core/stationOrders'
import {

  buildBasketView,
  basketItemCount,
  lineCannotBeOrdered,
  withoutLinesThatCannotBeOrdered,
} from '../core/basket'
import { orderTotalCents } from '../core/totals'
import { messageForSendFailure, type SendFailureMessage } from '../core/sendFailure'
import { SEND_TIMEOUT_MS } from '../core/sendTimeout'
import { useCatalogStore } from './catalog'
import { useSessionStore } from '../../shared/stores/session'

export const ARRIVAL_NOTICE_MS = 8000

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
    return withClientOrderIdIfMissing(draftRestoredFromStorage())
  }

  function runningFestivalId(): string | null {
    return catalogStore.catalog.festival?.festivalId ?? null
  }

  function underTheRunningFestival(current: DraftOrder): DraftOrder {
    const running = runningFestivalId()
    if (current.festivalId !== null || running === null) {
      return current
    }
    return stampFestival(current, running)
  }

  const draft = ref(draftFromStorage())
  const progressWhenTheAppLoaded = progressAfterALoad(restoreSendProgress())
  const sendState = ref<SendState>(progressWhenTheAppLoaded.state)
  const failure = ref<SendFailureMessage | null>(progressWhenTheAppLoaded.failure)
  const attemptsMade = ref(progressWhenTheAppLoaded.attempts)
  const unresolvedAttempt = ref<PlaceOrderRequest | null>(
    progressWhenTheAppLoaded.unresolvedAttempt,
  )
  const acceptedOrderNumber = ref<number | null>(null)
  let arrivalNoticeTimer: ReturnType<typeof setTimeout> | null = null

  const catalogStore = useCatalogStore()

  function whatTheSendHasComeTo(): SendProgress {
    return {
      state: sendState.value,
      attempts: attemptsMade.value,
      failure: failure.value,
      unresolvedAttempt: unresolvedAttempt.value,
    }
  }

  const isSending = computed(() => sendIsUnderWayIn(sendState.value))
  const sendHasFailed = computed(() => sendHasFailedIn(sendState.value))
  const changesAreRefused = computed(() => changesAreRefusedFor(whatTheSendHasComeTo()))
  const onlyWritingItDownIsLeft = computed(() => writingItDownIsTheOnlyWayLeft(whatTheSendHasComeTo()))

  function theRefusalNoLongerFitsTheOrder(): void {
    if (failure.value === null) {
      return
    }
    failure.value = null
    unresolvedAttempt.value = null
    sendState.value = 'idle'
    rememberWhatBecameOfTheSend()
  }

  function change(makeTheChange: (current: DraftOrder) => DraftOrder): void {
    if (changesAreRefused.value) {
      return
    }
    draft.value = underTheRunningFestival(makeTheChange(draft.value))
    theRefusalNoLongerFitsTheOrder()
  }

  const basketLines = computed(() => buildBasketView(draft.value, catalogStore.catalog))
  const stationOrders = computed(() => buildStationOrders(basketLines.value))
  const itemCount = computed(() => basketItemCount(draft.value))
  const totalCents = computed(() => orderTotalCents(basketLines.value))
  const hasLinesThatCannotBeOrdered = computed(() =>
    basketLines.value.some(lineCannotBeOrdered),
  )

  function dismissDraftLoss(): void {
    draftWasLost.value = false
  }

  function stationName(stationId: string | null): string {
    return stationId === null ? '' : catalogStore.stationName(stationId)
  }

  function addItem(line: Omit<DraftLine, 'stationName'>): void {
    if (changesAreRefused.value) {
      return
    }
    dismissDraftLoss()
    draft.value = underTheRunningFestival(
      addLine(draft.value, { ...line, stationName: stationName(line.stationId) }),
    )
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
    change((current) => setLineStation(current, index, stationId, stationName(stationId)))
  }

  function setTable(tableName: string): void {
    change((current) => setTableName(current, tableName))
  }

  function deliveryModeAt(stationId: string): DeliveryMode {
    return deliveryModeChosenOrDefault(draft.value.deliveryModes, stationId)
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
    draft.value = underTheRunningFestival(draftFromStorage())
  }

  function dropTheDraftIfTheFestivalChanged(): void {
    if (changesAreRefused.value) {
      return
    }
    if (!draftIsForAnotherFestival(draft.value, runningFestivalId())) {
      return
    }
    startNextOrder()
  }

  function startNextOrderAfterWritingItDown(): void {
    failure.value = null
    attemptsMade.value = 0
    unresolvedAttempt.value = null
    sendState.value = 'idle'
    startNextOrder()
  }

  function rememberWhatBecameOfTheSend(): void {
    saveSendProgress(whatTheSendHasComeTo())
  }

  function putTheSendBackTo(progress: SendProgress): void {
    sendState.value = progress.state
    attemptsMade.value = progress.attempts
    failure.value = progress.failure
    unresolvedAttempt.value = progress.unresolvedAttempt
    rememberWhatBecameOfTheSend()
  }

  async function send(settlement: ConfirmedSettlement | null): Promise<void> {
    if (unresolvedAttempt.value !== null) {
      await sendAgain()
      return
    }
    const submitRequest = buildSubmitRequest(
      draft.value,
      catalogStore.catalog,
      settlement,
      buildStationDeliveryModes(stationOrders.value, draft.value.deliveryModes),
    )
    const sendBeforeThisAttempt = whatTheSendHasComeTo()
    unresolvedAttempt.value = submitRequest
    await postTheAttempt(submitRequest, sendBeforeThisAttempt)
  }

  async function postTheAttempt(
    attempt: PlaceOrderRequest,
    sendBeforeThisAttempt: SendProgress = whatTheSendHasComeTo(),
  ): Promise<void> {
    const session = useSessionStore()
    attemptsMade.value += 1
    failure.value = null
    sendState.value = 'sending'
    rememberWhatBecameOfTheSend()
    const result = await request('/api/orders', {
      method: 'POST',
      body: attempt,
      token: session.deviceToken,
      timeoutMs: SEND_TIMEOUT_MS,
      schema: PlacedOrderView,
    })
    switch (result.kind) {
      case 'ok':
        acceptedOrderNumber.value = result.data.globalOrderNumber
        failure.value = null
        attemptsMade.value = 0
        unresolvedAttempt.value = null
        sendState.value = 'accepted'
        arrivalNoticeTimer = setTimeout(dismissConfirmation, ARRIVAL_NOTICE_MS)
        startNextOrder()
        return
      case 'unreachable':
      case 'unreadableAnswer':
        failure.value = messageForSendFailure(result)
        sendState.value = 'failed'
        rememberWhatBecameOfTheSend()
        return
      case 'error':
        if (answerSaysTheDeviceIsNoLongerSetUp(result)) {
          putTheSendBackTo(sendBeforeThisAttempt)
          return
        }
        if (answerIsABusinessRefusal(result)) {
          unresolvedAttempt.value = null
          void catalogStore.load()
        } else if (sendBeforeThisAttempt.unresolvedAttempt === null) {
          unresolvedAttempt.value = null
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
    const attempt = unresolvedAttempt.value
    if (attempt === null) {
      return
    }
    await postTheAttempt(attempt)
  }

  return {
    draft,
    draftWasLost,
    sendState,
    failure,
    attemptsMade,
    acceptedOrderNumber,
    basketLines,
    stationOrders,
    itemCount,
    totalCents,
    hasLinesThatCannotBeOrdered,
    isSending,
    sendHasFailed,
    changesAreRefused,
    onlyWritingItDownIsLeft,
    addItem,
    dropLine,
    dropLinesThatCannotBeOrdered,
    noteLine,
    chooseStation,
    setTable,
    deliveryModeAt,
    chooseDeliveryMode,
    dismissConfirmation,
    dismissDraftLoss,
    dropTheDraftIfTheFestivalChanged,
    startNextOrderAfterWritingItDown,
    send,
    sendAgain,
  }
})
