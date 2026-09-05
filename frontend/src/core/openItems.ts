import type { OpenOrderItem, OpenTable, SettlementResponse } from './apiTypes'

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

export function withItemToggled(
  selectedItemIds: readonly string[],
  orderItemId: string,
): string[] {
  return selectedItemIds.includes(orderItemId)
    ? selectedItemIds.filter((id) => id !== orderItemId)
    : [...selectedItemIds, orderItemId]
}

export function withWholeTable(
  selectedItemIds: readonly string[],
  table: OpenTable,
  isWanted: boolean,
): string[] {
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
