import type { TableOrderRecordItemView, TableOrderRecordView, TableOrderReportView } from '../../../src/shared/api/generatedSchemas'

export function itemWith(
  orderItemId: string,
  fulfilledAtUtc: string | null,
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
    orderedAtUtc: '2026-09-05T18:00:00Z',
    fulfilledAtUtc,
    settledAtUtc,
  }
}

export function recordWith(items: TableOrderRecordItemView[]): TableOrderRecordView {
  return {
    orderId: 'order-1',
    globalOrderNumber: 1,
    createdAtUtc: '2026-09-05T18:00:00Z',
    staffMemberName: 'Anna',
    items,
  }
}

export function reportWith(orders: TableOrderRecordView[]): TableOrderReportView {
  return {
    tableId: 'table-12',
    tableName: 'Tisch 12',
    openAmountCents: 0,
    orders,
  }
}
