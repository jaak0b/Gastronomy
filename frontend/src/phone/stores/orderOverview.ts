import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import type { TableOrderReportView } from '../../shared/api/generatedSchemas'
import { assertNever } from '../../shared/core/assertNever'
import { createLatestRequestGate } from '../../shared/core/latestRequestGate'
import { searchTheTable } from '../api/tableSearch'
import type { TableSearchFilter } from '../core/openItems'
import { useConnectionStore } from '../../shared/stores/connection'
import { useSessionStore } from '../../shared/stores/session'

export const EVERY_ORDER: TableSearchFilter = {
  orderStatus: null,
  paymentStatus: null,
  includeCancelled: true,
}

export const useOrderOverviewStore = defineStore('orderOverview', () => {
  const tableId = ref<string | null>(null)
  const filter = ref<TableSearchFilter>({ ...EVERY_ORDER })
  const report = ref<TableOrderReportView | null>(null)
  const loadFailureKey = ref<string | null>(null)

  const loadGate = createLatestRequestGate()

  const hasLoaded = computed(() => report.value !== null)
  const loadFailed = computed(() => loadFailureKey.value !== null)

  function openTable(openedTableId: string): void {
    loadGate.startRequest()
    tableId.value = openedTableId
    filter.value = { ...EVERY_ORDER }
    report.value = null
    loadFailureKey.value = null
  }

  function closeTable(): void {
    loadGate.startRequest()
    tableId.value = null
    filter.value = { ...EVERY_ORDER }
    report.value = null
    loadFailureKey.value = null
  }

  async function load(): Promise<void> {
    const shownTableId = tableId.value
    const token = useSessionStore().deviceToken
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
        return
      case 'refused':
        report.value = null
        loadFailureKey.value = answer.failureKey
        return
      case 'unanswered':
        report.value = null
        loadFailureKey.value = 'phone.openItems.errors.loadFailed'
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

  return {
    tableId,
    filter,
    report,
    hasLoaded,
    loadFailed,
    loadFailureKey,
    openTable,
    closeTable,
    load,
    changeFilter,
    listen,
  }
})
