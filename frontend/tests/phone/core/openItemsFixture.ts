import { OpenTableView, TableOrderRecordItemView, TableOrderRecordView } from '../../../src/shared/api/generatedSchemas'

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

export function tableWith(tableName: string, prices: number[]): OpenTableView {
  return {
    tableName,
    openAmountCents: prices.reduce((total, price) => total + price, 0),
    items: prices.map((unitPriceCents, position) => ({
      orderItemId: `${tableName}-${position}`,
      orderId: 'order-1',
      globalOrderNumber: 1,
      itemName: 'Bratwurst',
      note: null,
      unitPriceCents,
      orderedAtUtc: '2026-09-05T18:00:00Z',
    })),
  }
}
