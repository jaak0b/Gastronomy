import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { request, type ApiResult } from '../../shared/api/client'
import { SettlementView, TableOrderRecordView, TableOrderReportView } from '../../shared/api/generatedSchemas'
import { assertNever } from '../../shared/core/assertNever'
import { createLatestRequestGate } from '../../shared/core/latestRequestGate'
import {
  noticeAfterSettling,
  type SentSettleLine,
  selectedAmountCents,
  selectedItems,
  withItemToggled,
  withWholeOrder,
  withWholeTable,
  withoutItemsThatAreGone,
  type SettleNotice,
  type SettleOutcome,
  type SettlementPaymentMethod,
} from '../core/openItems'
import { SEND_TIMEOUT_MS } from '../core/sendTimeout'
import { splitSettlement } from '../core/settlementSplit'
import { useConnectionStore } from '../../shared/stores/connection'
import { useSessionStore } from '../../shared/stores/session'

const TABLE_NOT_FOUND = 404

export const useOpenItemsStore = defineStore('openItems', () => {
  const tableId = ref<string | null>(null)
  const report = ref<TableOrderReportView | null>(null)
  const loadFailureKey = ref<string | null>(null)
  const selectedItemIds = ref<string[]>([])
  const isSettling = ref(false)
  const notice = ref<SettleNotice | null>(null)
  const preselectedItemIds = ref<string[] | null>(null)

  const loadGate = createLatestRequestGate()

  const selectedTotalCents = computed(() =>
    report.value === null ? 0 : selectedAmountCents(report.value, selectedItemIds.value),
  )

  function deviceToken(): string | null {
    return useSessionStore().deviceToken
  }

  function openTable(openedTableId: string): void {
    loadGate.startRequest()
    tableId.value = openedTableId
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

  function closeTable(): void {
    loadGate.startRequest()
    tableId.value = null
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
    if (deviceToken() === null || shownTableId === null) {
      return
    }
    const token = loadGate.startRequest()
    const result = await request(`/api/open-items/tables/${encodeURIComponent(shownTableId)}`, {
      token: deviceToken(),
      schema: TableOrderReportView,
    })
    if (!loadGate.isNewestRequest(token)) {
      return
    }
    switch (result.kind) {
      case 'ok':
        report.value = result.data
        loadFailureKey.value = null
        selectedItemIds.value = withoutItemsThatAreGone(
          preselectedItemIds.value ?? selectedItemIds.value,
          result.data,
        )
        preselectedItemIds.value = null
        return
      case 'error':
        if (result.status === TABLE_NOT_FOUND) {
          showWhyTheLaptopRefusedTheTable('phone.openItems.errors.tableCouldNotBeLoaded')
          return
        }
        if (result.body !== null) {
          showWhyTheLaptopRefusedTheTable(result.body.messageKey)
          return
        }
        showThatTheLaptopCouldNotAnswer()
        return
      case 'unreachable':
      case 'unreadableAnswer':
        showThatTheLaptopCouldNotAnswer()
        return
      default:
        return assertNever(result)
    }
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
    selectedItemIds.value = withItemToggled(selectedItemIds.value, report.value, orderItemId)
  }

  function setWholeTable(isWanted: boolean): void {
    dismissNotice()
    if (report.value === null) {
      return
    }
    selectedItemIds.value = withWholeTable(selectedItemIds.value, report.value, isWanted)
  }

  function setWholeOrder(order: TableOrderRecordView, isWanted: boolean): void {
    dismissNotice()
    selectedItemIds.value = withWholeOrder(selectedItemIds.value, order, isWanted)
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
          parameters: {},
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
    const items = report.value === null ? [] : selectedItems(report.value, selectedItemIds.value)
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

  return {
    tableId,
    report,
    loadFailureKey,
    selectedItemIds,
    selectedTotalCents,
    isSettling,
    notice,
    openTable,
    openTableAndSelectItemsOnceLoaded,
    closeTable,
    load,
    listen,
    dismissNotice,
    toggleItem,
    setWholeTable,
    setWholeOrder,
    settle,
  }
})
