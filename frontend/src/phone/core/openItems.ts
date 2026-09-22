import { OpenOrderItemView, OpenTableView, SettleLineRequest, SettlementView, TableOrderRecordItemView, TableOrderRecordView, TableOrderReportView } from '../../shared/api/generatedSchemas'
import type { AppLanguage } from '../../shared/core/deviceLanguage'
import { formatPrice } from './totals'

export type SettleOutcome = 'accepted' | 'refused' | 'answerNeverCame'

export type ProductionState = 'none' | 'some' | 'all'

export type PositionState = 'produced' | 'notProduced' | 'unknown'

export const TABLE_LOOKUP_DEBOUNCE_MS = 300

export interface SettleNotice {
  key: string
  parameters: Record<string, string | number>
  count: number | null
}

export function openTableInReport(report: TableOrderReportView): OpenTableView {
  return {
    tableName: report.tableName,
    openAmountCents: report.openAmountCents,
    items: report.orders
      .flatMap((order) => order.items)
      .filter((item) => item.settledAtUtc === null),
  }
}

export function producedCountIn(order: TableOrderRecordView): number {
  return order.items.filter((item) => item.fulfilledAtUtc !== null).length
}

export function productionStateOf(order: TableOrderRecordView): ProductionState {
  const produced = producedCountIn(order)
  if (produced === 0) {
    return 'none'
  }
  return produced === order.items.length ? 'all' : 'some'
}

export function positionStateOf(item: TableOrderRecordItemView): PositionState {
  return item.fulfilledAtUtc === null ? 'notProduced' : 'produced'
}

export function itemIdsAtTable(table: OpenTableView): string[] {
  return table.items.map((item) => item.orderItemId)
}

export function selectedItemsAtTable(
  table: OpenTableView,
  selectedItemIds: readonly string[],
): OpenOrderItemView[] {
  return table.items.filter((item) => selectedItemIds.includes(item.orderItemId))
}

export function selectedItems(
  tables: readonly OpenTableView[],
  selectedItemIds: readonly string[],
): OpenOrderItemView[] {
  return tables.flatMap((table) => selectedItemsAtTable(table, selectedItemIds))
}

export function selectedAmountCents(
  tables: readonly OpenTableView[],
  selectedItemIds: readonly string[],
): number {
  return selectedItems(tables, selectedItemIds).reduce(
    (total, item) => total + item.unitPriceCents,
    0,
  )
}

export function isTheWholeTableSelected(
  table: OpenTableView,
  selectedItemIds: readonly string[],
): boolean {
  return (
    table.items.length > 0
    && table.items.every((item) => selectedItemIds.includes(item.orderItemId))
  )
}

export function tableHoldingTheSelection(
  tables: readonly OpenTableView[],
  selectedItemIds: readonly string[],
): string | null {
  const holding = tables.find(
    (table) => selectedItemsAtTable(table, selectedItemIds).length > 0,
  )
  return holding === undefined ? null : holding.tableName
}

export function tableForItem(tables: readonly OpenTableView[], orderItemId: string): string | null {
  const owning = tables.find((table) => itemIdsAtTable(table).includes(orderItemId))
  return owning === undefined ? null : owning.tableName
}

export function isHeldBackByAnotherTable(
  tables: readonly OpenTableView[],
  selectedItemIds: readonly string[],
  tableName: string,
): boolean {
  const holding = tableHoldingTheSelection(tables, selectedItemIds)
  return holding !== null && holding !== tableName
}

export function withItemToggled(
  selectedItemIds: readonly string[],
  tables: readonly OpenTableView[],
  orderItemId: string,
): string[] {
  const tableName = tableForItem(tables, orderItemId)
  if (tableName === null || isHeldBackByAnotherTable(tables, selectedItemIds, tableName)) {
    return [...selectedItemIds]
  }
  return selectedItemIds.includes(orderItemId)
    ? selectedItemIds.filter((id) => id !== orderItemId)
    : [...selectedItemIds, orderItemId]
}

export function withWholeTable(
  selectedItemIds: readonly string[],
  tables: readonly OpenTableView[],
  table: OpenTableView,
  isWanted: boolean,
): string[] {
  if (isHeldBackByAnotherTable(tables, selectedItemIds, table.tableName)) {
    return [...selectedItemIds]
  }
  const tableItemIds = itemIdsAtTable(table)
  const untouched = selectedItemIds.filter((id) => !tableItemIds.includes(id))
  return isWanted ? [...untouched, ...tableItemIds] : untouched
}

export function withoutItemsThatAreGone(
  selectedItemIds: readonly string[],
  tables: readonly OpenTableView[],
): string[] {
  const stillOpen = tables.flatMap(itemIdsAtTable)
  return selectedItemIds.filter((id) => stillOpen.includes(id))
}

export function isPaymentNoticeWritten(reason: string): boolean {
  return reason.trim().length > 0
}

export function isPaymentNoticeNeeded(
  amountPaidCents: number,
  selectedTotalCents: number,
): boolean {
  return amountPaidCents < selectedTotalCents
}

export function canTheAmountBeSettled(
  amountPaidCents: number | null,
  reason: string,
  selectedTotalCents: number,
): boolean {
  if (amountPaidCents === null) {
    return false
  }
  return (
    !isPaymentNoticeNeeded(amountPaidCents, selectedTotalCents) || isPaymentNoticeWritten(reason)
  )
}

export type SentSettleLine = SettleLineRequest & { paidPriceCents: number }

export function noticeAfterSettling(
  settlement: SettlementView,
  sentLines: readonly SentSettleLine[],
  language: AppLanguage,
): SettleNotice | null {
  const takenBySomebodyElse = settlement.alreadySettledByOthersOrderItemIds
  if (takenBySomebodyElse.length > 0) {
    const toHandBack = sentLines
      .filter((line) => takenBySomebodyElse.includes(line.orderItemId))
      .reduce((total, line) => total + line.paidPriceCents, 0)
    return {
      key: 'openItems.someWereAlreadySettled',
      parameters: {
        count: takenBySomebodyElse.length,
        amount: formatPrice(toHandBack, language),
      },
      count: takenBySomebodyElse.length,
    }
  }
  return null
}
