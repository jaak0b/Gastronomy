import type {
  AdminFestivalView,
  AdminItemView,
  AdminStationView,
  OpenOrderItemView,
  OpenTableView,
  StationOrderQueueView,
  StationQueueItemView,
} from '../../src/shared/api/generatedSchemas'

export function anAdminItem(overrides: Partial<AdminItemView> = {}): AdminItemView {
  return {
    itemId: 'item-bratwurst',
    name: 'Bratwurst',
    categoryId: 'category-speisen',
    sortOrder: 1,
    isActive: true,
    productionMinutes: null,
    isQueueIndependent: false,
    atTheFestival: null,
    ingredients: [],
    ...overrides,
  }
}

export function aFestival(overrides: Partial<AdminFestivalView> = {}): AdminFestivalView {
  return {
    festivalId: 'fest-1',
    name: 'Sommerfest',
    startsAtUtc: '2026-07-18T10:00:00Z',
    endsAtUtc: '2026-07-19T02:00:00Z',
    isHidden: false,
    isRunning: true,
    stationCount: 1,
    menuItemCount: 1,
    orderCount: 0,
    ...overrides,
  }
}

export function anAdminStation(overrides: Partial<AdminStationView> = {}): AdminStationView {
  return {
    stationId: 'station-kueche',
    name: 'Küche',
    sortOrder: 1,
    isActive: true,
    hasDevice: true,
    isAtAnyFestival: true,
    ...overrides,
  }
}

export function anOpenItem(overrides: Partial<OpenOrderItemView> = {}): OpenOrderItemView {
  return {
    orderItemId: 'item-1',
    orderId: 'order-1',
    globalOrderNumber: 1,
    itemName: 'Bratwurst',
    note: null,
    unitPriceCents: 350,
    orderedAtUtc: '2026-07-18T18:00:00Z',
    ...overrides,
  }
}

export function anOpenItemsTable(overrides: Partial<OpenTableView> = {}): OpenTableView {
  return {
    tableName: 'Tisch 4',
    openAmountCents: 350,
    items: [anOpenItem()],
    ...overrides,
  }
}

export function aQueuedItem(overrides: Partial<StationQueueItemView> = {}): StationQueueItemView {
  return {
    orderItemId: 'order-item-1',
    itemName: 'Bratwurst',
    note: null,
    fulfilledAtUtc: null,
    ...overrides,
  }
}

export function aStationOrder(
  overrides: Partial<StationOrderQueueView> = {},
): StationOrderQueueView {
  const items = overrides.items ?? [aQueuedItem()]
  return {
    stationOrderId: 'station-order-1',
    globalOrderNumber: 1,
    stationOrderNumber: 1,
    tableName: 'Tisch 4',
    staffMemberName: 'Anna',
    deliveryMode: 'together',
    createdAtUtc: '2026-07-18T18:00:00Z',
    isHiddenFromAsItComesQueue: false,
    itemCount: items.length,
    fulfilledItemCount: items.filter((item) => item.fulfilledAtUtc !== null).length,
    ...overrides,
    items,
  }
}
