import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { request, type ApiResult } from '../../shared/api/client'
import {
  CancellationView,
  type CancelItemsRequest,
  SettlementView,
  type TableOrderRecordView,
  type TableOrderReportView,
} from '../../shared/api/generatedSchemas'
import { assertNever } from '../../shared/core/assertNever'
import { createLatestRequestGate } from '../../shared/core/latestRequestGate'
import {
  cashToReturnCents,
  defaultFilterFor,
  type TableSearchFilter,
  noticeAfterSettling,
  type SentSettleLine,
  selectedAmountCents,
  selectedItems,
  withItemToggled,
  withWholeOrder,
  withWholeTable,
  withoutItemsThatAreGone,
  type SelectionPurpose,
  type SettleNotice,
  type SettleOutcome,
  type SettlementPaymentMethod,
} from '../core/openItems'
import { SEND_TIMEOUT_MS } from '../core/sendTimeout'
import { splitSettlement } from '../core/settlementSplit'
import { searchTheTable } from '../api/tableSearch'
import { useConnectionStore } from '../../shared/stores/connection'
import { useSessionStore } from '../../shared/stores/session'

export const useOpenItemsStore = defineStore('openItems', () => {
  const tableId = ref<string | null>(null)
  const report = ref<TableOrderReportView | null>(null)
  const loadFailureKey = ref<string | null>(null)
  const selectedItemIds = ref<string[]>([])
  const isSettling = ref(false)
  const isCancelling = ref(false)
  const purpose = ref<SelectionPurpose>('settling')
  const filter = ref<TableSearchFilter>(defaultFilterFor('settling'))
  const notice = ref<SettleNotice | null>(null)
  const preselectedItemIds = ref<string[] | null>(null)

  const loadGate = createLatestRequestGate()

  const hasLoaded = computed(() => report.value !== null)
  const loadFailed = computed(() => loadFailureKey.value !== null)

  const selectedTotalCents = computed(() =>
    report.value === null ? 0 : selectedAmountCents(report.value, selectedItemIds.value),
  )

  const itemsToCancel = computed(() =>
    report.value === null ? [] : selectedItems(report.value, selectedItemIds.value, 'cancelling'),
  )

  const cashToReturnForTheItemsToCancel = computed(() => cashToReturnCents(itemsToCancel.value))

  function deviceToken(): string | null {
    return useSessionStore().deviceToken
  }

  function openTable(openedTableId: string): void {
    loadGate.startRequest()
    tableId.value = openedTableId
    purpose.value = 'settling'
    filter.value = defaultFilterFor('settling')
    report.value = null
    loadFailureKey.value = null
    selectedItemIds.value = []
    preselectedItemIds.value = null
    notice.value = null
  }

  function openTableAndSelectItemsOnceLoaded(openedTableId: string, itemIds: readonly string[]): void {
    openTable(openedTableId)
    preselectedItemIds.value = [...itemIds]
  }

  function openTableToCancelItems(openedTableId: string): void {
    openTable(openedTableId)
    purpose.value = 'cancelling'
    filter.value = defaultFilterFor('cancelling')
  }

  function closeTable(): void {
    loadGate.startRequest()
    tableId.value = null
    filter.value = defaultFilterFor(purpose.value)
    report.value = null
    loadFailureKey.value = null
    selectedItemIds.value = []
    preselectedItemIds.value = null
    notice.value = null
  }

  function showWhyTheLaptopRefusedTheTable(failureKey: string): void {
    report.value = null
    selectedItemIds.value = []
    preselectedItemIds.value = null
    loadFailureKey.value = failureKey
  }

  function showThatTheLaptopCouldNotAnswer(): void {
    report.value = null
    loadFailureKey.value = 'phone.openItems.errors.loadFailed'
  }

  async function load(): Promise<void> {
    const shownTableId = tableId.value
    const token = deviceToken()
    if (token === null || shownTableId === null) {
      return
    }
    const gateNumber = loadGate.startRequest()
    const answer = await searchTheTable(shownTableId, filter.value, token)
    if (!loadGate.isNewestRequest(gateNumber)) {
      return
    }
    switch (answer.kind) {
      case 'found':
        report.value = answer.report
        loadFailureKey.value = null
        selectedItemIds.value = withoutItemsThatAreGone(
          preselectedItemIds.value ?? selectedItemIds.value,
          answer.report,
          purpose.value,
        )
        preselectedItemIds.value = null
        return
      case 'refused':
        showWhyTheLaptopRefusedTheTable(answer.failureKey)
        return
      case 'unanswered':
        showThatTheLaptopCouldNotAnswer()
        return
      default:
        return assertNever(answer)
    }
  }

  async function changeFilter(changed: TableSearchFilter): Promise<void> {
    filter.value = changed
    await load()
  }

  function listen(): () => void {
    return useConnectionStore().listenToTheLaptop(['ConfigurationChanged', 'OrdersChanged'], load)
  }

  function dismissNotice(): void {
    notice.value = null
  }

  function toggleItem(orderItemId: string): void {
    dismissNotice()
    if (report.value === null) {
      return
    }
    selectedItemIds.value = withItemToggled(selectedItemIds.value, report.value, orderItemId, purpose.value)
  }

  function setWholeTable(isWanted: boolean): void {
    dismissNotice()
    if (report.value === null) {
      return
    }
    selectedItemIds.value = withWholeTable(selectedItemIds.value, report.value, isWanted, purpose.value)
  }

  function setWholeOrder(order: TableOrderRecordView, isWanted: boolean): void {
    dismissNotice()
    selectedItemIds.value = withWholeOrder(selectedItemIds.value, order, isWanted, purpose.value)
  }

  async function accept(
    result: ApiResult<SettlementView>,
    sentLines: readonly SentSettleLine[],
  ): Promise<SettleOutcome> {
    isSettling.value = false
    switch (result.kind) {
      case 'ok':
        notice.value = noticeAfterSettling(result.data, sentLines, useSessionStore().language)
        selectedItemIds.value = []
        await load()
        return 'accepted'
      case 'error':
        notice.value = {
          key: result.body?.messageKey ?? 'phone.openItems.errors.settleFailed',
          parameters: result.body?.parameters ?? {},
          count: null,
        }
        await load()
        return 'refused'
      case 'unreachable':
      case 'unreadableAnswer':
        notice.value = { key: 'phone.openItems.errors.settleAnswerNeverCame', parameters: {}, count: null }
        return 'answerNeverCame'
      default:
        return assertNever(result)
    }
  }

  async function settle(
    amountPaidCents: number,
    paymentNotice: string | null,
    paymentMethod: SettlementPaymentMethod,
  ): Promise<SettleOutcome> {
    isSettling.value = true
    const items = report.value === null ? [] : selectedItems(report.value, selectedItemIds.value, 'settling')
    const split = splitSettlement(amountPaidCents, items, paymentNotice ?? '')
    const lines = items.map((item, index) => ({
      orderItemId: item.orderItemId,
      paidPriceCents: split[index].paidPriceCents,
      paymentNotice: split[index].paymentNotice,
    }))
    return await accept(
      await request('/api/open-items/settle', {
        method: 'POST',
        body: { lines, paymentMethod },
        token: deviceToken(),
        timeoutMs: SEND_TIMEOUT_MS,
        schema: SettlementView,
      }),
      lines,
    )
  }

  async function cancelSelectedItems(): Promise<boolean> {
    isCancelling.value = true
    const cancellation: CancelItemsRequest = {
      orderItemIds: itemsToCancel.value.map((item) => item.orderItemId),
    }
    const result = await request('/api/open-items/cancel', {
      method: 'POST',
      body: cancellation,
      token: deviceToken(),
      timeoutMs: SEND_TIMEOUT_MS,
      schema: CancellationView,
    })
    isCancelling.value = false
    switch (result.kind) {
      case 'ok':
        notice.value = null
        selectedItemIds.value = []
        await load()
        return true
      case 'error':
        notice.value = {
          key: result.body?.messageKey ?? 'phone.openItems.errors.cancelAnswerNeverCame',
          parameters: result.body?.parameters ?? {},
          count: null,
        }
        return false
      case 'unreachable':
      case 'unreadableAnswer':
        notice.value = { key: 'phone.openItems.errors.cancelAnswerNeverCame', parameters: {}, count: null }
        return false
      default:
        return assertNever(result)
    }
  }

  return {
    tableId,
    purpose,
    filter,
    report,
    hasLoaded,
    loadFailed,
    loadFailureKey,
    selectedItemIds,
    selectedTotalCents,
    itemsToCancel,
    cashToReturnForTheItemsToCancel,
    isSettling,
    isCancelling,
    notice,
    openTable,
    openTableAndSelectItemsOnceLoaded,
    openTableToCancelItems,
    closeTable,
    load,
    changeFilter,
    listen,
    dismissNotice,
    toggleItem,
    setWholeTable,
    setWholeOrder,
    settle,
    cancelSelectedItems,
  }
})
