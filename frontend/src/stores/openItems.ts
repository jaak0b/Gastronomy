import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { request, type ApiResult } from '../api/client'
import type {
  OpenItemsResponse,
  OpenItemsSettleLine,
  OpenTable,
  SettlementResponse,
  TableNamesResponse,
} from '../core/apiTypes'
import { assertNever } from '../core/assertNever'
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
import { useConnectionStore } from './connection'
import { useSessionStore } from './session'

export const useOpenItemsStore = defineStore('openItems', () => {
  const tables = ref<OpenTable[]>([])
  const knownTableNames = ref<string[]>([])
  const selectedItemIds = ref<string[]>([])
  const hasLoaded = ref(false)
  const loadFailed = ref(false)
  const itemsWithoutAnOrderCount = ref(0)
  const isSettling = ref(false)
  const notice = ref<SettleNotice | null>(null)

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
    const result = await request<OpenItemsResponse>('/api/open-items', { token: deviceToken() })
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
    const result = await request<TableNamesResponse>('/api/open-items/table-names', {
      token: deviceToken(),
    })
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
      await request<SettlementResponse>('/api/open-items/settle', {
        method: 'POST',
        body: { lines },
        token: deviceToken(),
        timeoutMs: SEND_TIMEOUT_MS,
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
