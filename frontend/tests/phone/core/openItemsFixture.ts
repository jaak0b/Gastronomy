import type { TableOrderRecordItemView, TableOrderRecordView, TableOrderReportView } from '../../../src/shared/api/generatedSchemas'

export function itemWith(
  orderItemId: string,
  handedOutAtUtc: string | null,
  settledAtUtc: string | null,
  unitPriceCents: number,
): TableOrderRecordItemView {
  return {
    orderItemId,
    orderId: 'order-1',
    globalOrderNumber: 1,
    itemName: 'Bratwurst',
    note: null,
    unitPriceCents,
    state: handedOutAtUtc === null ? 'open' : 'handedOut',
    stateSetAtUtc: handedOutAtUtc ?? '2026-09-05T18:00:00Z',
    wasHandedOutWhenCancelled: false,
    settledAtUtc,
    chargedPriceCents: settledAtUtc === null ? null : unitPriceCents,
  }
}

export function recordWith(items: TableOrderRecordItemView[]): TableOrderRecordView {
  return {
    orderId: 'order-1',
    globalOrderNumber: 1,
    createdAtUtc: '2026-09-05T18:00:00Z',
    staffMemberName: 'Anna',
    notCancelledItemCount: items.length,
    handedOutItemCount: items.filter((item) => item.state === 'handedOut').length,
    items,
  }
}

export function reportWith(orders: TableOrderRecordView[]): TableOrderReportView {
  return {
    tableId: 'table-12',
    tableName: 'Tisch 12',
    orders,
  }
}
