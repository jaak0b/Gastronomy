import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { request, type ApiResult } from '../../shared/api/client'
import {
  openItemsResponseSchema,
  settlementResponseSchema,
  tableNamesResponseSchema,
  tableOrderReportSchema,
} from '../../shared/api/apiSchemas'
import type {
  OpenItemsSettleLine,
  OpenTable,
  SettlementResponse,
  TableOrderReport,
} from '../../shared/api/apiTypes'
import { assertNever } from '../../shared/core/assertNever'
import { createLatestRequestGate } from '../../shared/core/latestRequestGate'
import {
  noticeAfterSettling,
  openTableInReport,
  selectedAmountCents,
  selectedItems,
  withItemToggled,
  withWholeTable,
  withoutItemsThatAreGone,
  type SettleNotice,
  type SettleOutcome,
} from '../core/openItems'
import { SEND_TIMEOUT_MS } from '../core/sendTimeout'
import { splitSettlement } from '../core/settlementSplit'
import { useConnectionStore } from '../../shared/stores/connection'
import { useSessionStore } from '../../shared/stores/session'

export const useOpenItemsStore = defineStore('openItems', () => {
  const tables = ref<OpenTable[]>([])
  const knownTableNames = ref<string[]>([])
  const selectedItemIds = ref<string[]>([])
  const hasLoaded = ref(false)
  const loadFailed = ref(false)
  const itemsWithoutAnOrderCount = ref(0)
  const isSettling = ref(false)
  const notice = ref<SettleNotice | null>(null)
  const lookupName = ref<string | null>(null)
  const lookupReport = ref<TableOrderReport | null>(null)
  const lookupFailed = ref(false)

  const tablesGate = createLatestRequestGate()
  const tableNamesGate = createLatestRequestGate()
  const lookupGate = createLatestRequestGate()

  const isLookingUp = computed(() => lookupName.value !== null)

  const lookupTable = computed<OpenTable | null>(() =>
    lookupReport.value === null ? null : openTableInReport(lookupReport.value),
  )

  const activeTables = computed<OpenTable[]>(() => {
    if (lookupName.value === null) {
      return tables.value
    }
    return lookupTable.value === null ? [] : [lookupTable.value]
  })

  const selectedTotalCents = computed(() =>
    selectedAmountCents(activeTables.value, selectedItemIds.value),
  )

  function deviceToken(): string | null {
    return useSessionStore().deviceToken
  }

  async function load(): Promise<void> {
    if (deviceToken() === null) {
      return
    }
    const token = tablesGate.startRequest()
    const result = await request('/api/open-items', {
      token: deviceToken(),
      schema: openItemsResponseSchema,
    })
    if (!tablesGate.isNewestRequest(token)) {
      return
    }
    loadFailed.value = result.kind !== 'ok'
    if (result.kind === 'ok') {
      tables.value = result.data.tables
      itemsWithoutAnOrderCount.value = result.data.itemsWithoutAnOrderCount
      selectedItemIds.value = withoutItemsThatAreGone(selectedItemIds.value, activeTables.value)
      hasLoaded.value = true
    }
    await refreshLookup()
  }

  async function loadTableNames(): Promise<void> {
    if (deviceToken() === null) {
      return
    }
    const token = tableNamesGate.startRequest()
    const result = await request('/api/open-items/table-names', {
      token: deviceToken(),
      schema: tableNamesResponseSchema,
    })
    if (!tableNamesGate.isNewestRequest(token)) {
      return
    }
    if (result.kind !== 'ok') {
      return
    }
    knownTableNames.value = result.data.tableNames
  }

  function openLookup(tableName: string): void {
    if (lookupName.value === tableName) {
      return
    }
    lookupName.value = tableName
    lookupReport.value = null
    lookupFailed.value = false
    selectedItemIds.value = []
  }

  function closeLookup(): void {
    lookupName.value = null
    lookupReport.value = null
    lookupFailed.value = false
    selectedItemIds.value = []
    lookupGate.startRequest()
  }

  async function loadTableReport(tableName: string): Promise<void> {
    if (deviceToken() === null) {
      return
    }
    if (lookupName.value !== tableName) {
      return
    }
    const token = lookupGate.startRequest()
    const result = await request(`/api/open-items/table?tableName=${encodeURIComponent(tableName)}`, {
      token: deviceToken(),
      schema: tableOrderReportSchema,
    })
    if (!lookupGate.isNewestRequest(token)) {
      return
    }
    if (lookupName.value !== tableName) {
      return
    }
    lookupFailed.value = result.kind !== 'ok'
    if (result.kind !== 'ok') {
      lookupReport.value = null
      return
    }
    lookupReport.value = result.data
    selectedItemIds.value = withoutItemsThatAreGone(selectedItemIds.value, activeTables.value)
  }

  async function refreshLookup(): Promise<void> {
    const tableName = lookupName.value
    if (tableName === null) {
      return
    }
    await loadTableReport(tableName)
  }

  function listen(): () => void {
    const connection = useConnectionStore()
    const releases = [
      connection.registerRefetch(load),
      connection.onEvent('OrderItemsSettled', () => {
        void load()
      }),
      connection.onEvent('OrderStatusChanged', () => {
        void load()
      }),
      connection.onEvent('StationOrdersChanged', () => {
        void load()
      }),
      connection.onEvent('FestivalChanged', () => {
        void load()
      }),
    ]
    return () => {
      for (const release of releases) {
        release()
      }
    }
  }

  function dismissNotice(): void {
    notice.value = null
  }

  function toggleItem(orderItemId: string): void {
    dismissNotice()
    selectedItemIds.value = withItemToggled(selectedItemIds.value, activeTables.value, orderItemId)
  }

  function setWholeTable(table: OpenTable, isWanted: boolean): void {
    dismissNotice()
    selectedItemIds.value = withWholeTable(
      selectedItemIds.value,
      activeTables.value,
      table,
      isWanted,
    )
  }

  async function accept(
    result: ApiResult<SettlementResponse>,
    sentLines: readonly OpenItemsSettleLine[],
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
          key: result.body?.messageKey ?? 'openItems.settleFailed',
          parameters: {},
          count: null,
        }
        await load()
        return 'refused'
      case 'unreachable':
      case 'unreadableAnswer':
        notice.value = { key: 'openItems.settleAnswerNeverCame', parameters: {}, count: null }
        return 'answerNeverCame'
      default:
        return assertNever(result)
    }
  }

  async function settle(
    amountPaidCents: number,
    paymentNotice: string | null,
  ): Promise<SettleOutcome> {
    isSettling.value = true
    const items = selectedItems(activeTables.value, selectedItemIds.value)
    const split = splitSettlement(amountPaidCents, items, paymentNotice ?? '')
    const lines = items.map((item, index) => ({
      orderItemId: item.orderItemId,
      paidPriceCents: split[index].paidPriceCents,
      paymentNotice: split[index].paymentNotice,
    }))
    return await accept(
      await request('/api/open-items/settle', {
        method: 'POST',
        body: { lines },
        token: deviceToken(),
        timeoutMs: SEND_TIMEOUT_MS,
        schema: settlementResponseSchema,
      }),
      lines,
    )
  }

  return {
    tables,
    knownTableNames,
    selectedItemIds,
    selectedTotalCents,
    hasLoaded,
    loadFailed,
    itemsWithoutAnOrderCount,
    isSettling,
    notice,
    lookupName,
    lookupReport,
    lookupFailed,
    isLookingUp,
    lookupTable,
    load,
    loadTableNames,
    listen,
    dismissNotice,
    toggleItem,
    setWholeTable,
    settle,
    openLookup,
    closeLookup,
    loadTableReport,
    refreshLookup,
  }
})
