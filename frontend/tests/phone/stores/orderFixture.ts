import { answer, inTurn, noConnection, stubLaptop, type StubbedLaptop } from '../../support/laptop'
import { CatalogView } from '../../../src/shared/api/generatedSchemas'

export function menuWithWaterAtTheBar(): CatalogView {
  return {
    festival: null,
    categories: [],
    items: [
      {
        id: 'item-wasser',
        name: 'Wasser',
        categoryId: 'category-getraenke',
        priceCents: 800,
        sortOrder: 1,
        isAvailable: true,
        stationIds: ['station-bar'],
        productionMinutes: null,
        isQueueIndependent: false,
      },
    ],
    stations: [{ id: 'station-bar', name: 'Bar', sortOrder: 1 }],
  }
}

export function answerWith(totalCents: number): StubbedLaptop {
  return stubLaptop().answersEverythingElse(
    answer({
      orderId: 'order-1',
      globalOrderNumber: 1,
      status: 'open',
      totalCents,
      createdAtUtc: '2026-09-05T18:00:00Z',
      stationOrders: [],
    }),
  )
}

const PLACED_ORDER_WITH_TWO_STATIONS = {
  orderId: 'order-9',
  globalOrderNumber: 141,
  status: 'open',
  totalCents: 1600,
  createdAtUtc: '2026-09-05T18:20:00Z',
  stationOrders: [
    {
      stationOrderId: 'station-order-1',
      stationId: 'station-bar',
      stationName: 'Bar',
      stationOrderNumber: 7,
      deliveryMode: 'together',
      itemIds: ['new-1'],
    },
    {
      stationOrderId: 'station-order-2',
      stationId: 'station-kitchen',
      stationName: 'Küche',
      stationOrderNumber: 3,
      deliveryMode: 'together',
      itemIds: ['new-2'],
    },
  ],
}

function openItemOf(orderItemId: string) {
  return {
    orderItemId,
    orderId: 'order-9',
    globalOrderNumber: 141,
    itemName: 'Wasser',
    note: null,
    unitPriceCents: 800,
    orderedAtUtc: '2026-09-05T18:20:00Z',
    fulfilledAtUtc: null,
    settledAtUtc: null,
  }
}

const TABLE_THREE_REPORT = {
  tableName: 'Tisch 3',
  openAmountCents: 1600,
  orders: [
    {
      orderId: 'order-9',
      globalOrderNumber: 141,
      createdAtUtc: '2026-09-05T18:20:00Z',
      staffMemberName: 'Anna',
      items: [openItemOf('new-1'), openItemOf('new-2')],
    },
  ],
}

export function theLaptopPlacesTheOrderAfter(failedAttempts: number): StubbedLaptop {
  const failures = Array.from({ length: failedAttempts }, () => noConnection())
  return stubLaptop()
    .answersEverythingElse(inTurn(...failures, answer(PLACED_ORDER_WITH_TWO_STATIONS)))
    .answers('GET', '/api/open-items/table', answer(TABLE_THREE_REPORT))
}
