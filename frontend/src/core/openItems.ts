import type { OpenOrderItem, OpenTable, SettlementResponse } from './apiTypes'

export type SettleOutcome = 'accepted' | 'refused' | 'answerNeverCame'

export interface SettleNotice {
  key: string
  parameters: Record<string, string | number>
  count: number | null
}

export function itemIdsOfTable(table: OpenTable): string[] {
  return table.items.map((item) => item.orderItemId)
}

export function selectedItemsOfTable(
  table: OpenTable,
  selectedItemIds: readonly string[],
): OpenOrderItem[] {
  return table.items.filter((item) => selectedItemIds.includes(item.orderItemId))
}

export function selectedAmountCents(
  tables: readonly OpenTable[],
  selectedItemIds: readonly string[],
): number {
  return tables
    .flatMap((table) => selectedItemsOfTable(table, selectedItemIds))
    .reduce((total, item) => total + item.unitPriceCents, 0)
}

export function isTheWholeTableSelected(
  table: OpenTable,
  selectedItemIds: readonly string[],
): boolean {
  return (
    table.items.length > 0
    && table.items.every((item) => selectedItemIds.includes(item.orderItemId))
  )
}

export function tableHoldingTheSelection(
  tables: readonly OpenTable[],
  selectedItemIds: readonly string[],
): string | null {
  const holding = tables.find(
    (table) => selectedItemsOfTable(table, selectedItemIds).length > 0,
  )
  return holding === undefined ? null : holding.tableName
}

export function tableOfItem(tables: readonly OpenTable[], orderItemId: string): string | null {
  const owning = tables.find((table) => itemIdsOfTable(table).includes(orderItemId))
  return owning === undefined ? null : owning.tableName
}

export function isHeldBackByAnotherTable(
  tables: readonly OpenTable[],
  selectedItemIds: readonly string[],
  tableName: string,
): boolean {
  const holding = tableHoldingTheSelection(tables, selectedItemIds)
  return holding !== null && holding !== tableName
}

export function withItemToggled(
  selectedItemIds: readonly string[],
  tables: readonly OpenTable[],
  orderItemId: string,
): string[] {
  const tableName = tableOfItem(tables, orderItemId)
  if (tableName === null || isHeldBackByAnotherTable(tables, selectedItemIds, tableName)) {
    return [...selectedItemIds]
  }
  return selectedItemIds.includes(orderItemId)
    ? selectedItemIds.filter((id) => id !== orderItemId)
    : [...selectedItemIds, orderItemId]
}

export function withWholeTable(
  selectedItemIds: readonly string[],
  tables: readonly OpenTable[],
  table: OpenTable,
  isWanted: boolean,
): string[] {
  if (isHeldBackByAnotherTable(tables, selectedItemIds, table.tableName)) {
    return [...selectedItemIds]
  }
  const idsOfTable = itemIdsOfTable(table)
  const untouched = selectedItemIds.filter((id) => !idsOfTable.includes(id))
  return isWanted ? [...untouched, ...idsOfTable] : untouched
}

export function withoutItemsThatAreGone(
  selectedItemIds: readonly string[],
  tables: readonly OpenTable[],
): string[] {
  const stillOpen = tables.flatMap(itemIdsOfTable)
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

export function noticeAfterSettling(settlement: SettlementResponse): SettleNotice | null {
  const takenBySomebodyElse = settlement.alreadySettledOrderItemIds.length
  if (takenBySomebodyElse > 0) {
    return {
      key: 'openItems.someWereAlreadySettled',
      parameters: { count: takenBySomebodyElse },
      count: takenBySomebodyElse,
    }
  }
  if (!settlement.otherPhonesWereTold) {
    return { key: 'openItems.otherPhonesWereNotTold', parameters: {}, count: null }
  }
  return null
}
