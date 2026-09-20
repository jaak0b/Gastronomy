import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { request, type ApiResult } from '../shared/api/client'
import {
  openItemsResponseSchema,
  settlementResponseSchema,
  tableNamesResponseSchema,
} from '../shared/api/apiSchemas'
import type {
  OpenItemsSettleLine,
  OpenTable,
  SettlementResponse,
} from '../shared/api/apiTypes'
import { assertNever } from '../shared/core/assertNever'
import { createLatestRequestGate } from '../shared/core/latestRequestGate'
import {
  noticeAfterSettling,
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
import { useConnectionStore } from '../shared/stores/connection'
import { useSessionStore } from '../shared/stores/session'

export const useOpenItemsStore = defineStore('openItems', () => {
  const tables = ref<OpenTable[]>([])
  const knownTableNames = ref<string[]>([])
  const selectedItemIds = ref<string[]>([])
  const hasLoaded = ref(false)
  const loadFailed = ref(false)
  const itemsWithoutAnOrderCount = ref(0)
  const isSettling = ref(false)
  const notice = ref<SettleNotice | null>(null)

  const tablesGate = createLatestRequestGate()
  const tableNamesGate = createLatestRequestGate()

  const selectedTotalCents = computed(() =>
    selectedAmountCents(tables.value, selectedItemIds.value),
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
    if (result.kind !== 'ok') {
      return
    }
    tables.value = result.data.tables
    itemsWithoutAnOrderCount.value = result.data.itemsWithoutAnOrderCount
    selectedItemIds.value = withoutItemsThatAreGone(selectedItemIds.value, result.data.tables)
    hasLoaded.value = true
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
    selectedItemIds.value = withItemToggled(selectedItemIds.value, tables.value, orderItemId)
  }

  function setWholeTable(table: OpenTable, isWanted: boolean): void {
    dismissNotice()
    selectedItemIds.value = withWholeTable(selectedItemIds.value, tables.value, table, isWanted)
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
    const items = selectedItems(tables.value, selectedItemIds.value)
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
    load,
    loadTableNames,
    listen,
    dismissNotice,
    toggleItem,
    setWholeTable,
    settle,
  }
})
