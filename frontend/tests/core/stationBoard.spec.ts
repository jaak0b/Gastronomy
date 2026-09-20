import { describe, expect, it } from 'vitest'
import {
  deliveryModeColour,
  deliveryModeKey,
  itemLineText,
  itemLines,
  linesByCount,
  openItemsIn,
  retainOpenItemIds,
  selectedOpenItemIds,
  selectedUnits,
  stationFailureKey,
  stationStats,
} from '../../src/core/stationBoard'
import type { StationOrder, StationOrderItem } from '../../src/core/apiTypes'

function stationOrderItem(
  orderItemId: string,
  itemName = 'Bratwurst',
  fulfilledAtUtc: string | null = null,
  note: string | null = null,
): StationOrderItem {
  return { orderItemId, itemName, note, fulfilledAtUtc }
}

function stationOrder(overrides: Partial<StationOrder> = {}): StationOrder {
  return {
    stationOrderId: 'station-order-1',
    globalOrderNumber: 137,
    stationOrderNumber: 12,
    tableName: 'Tisch 3',
    note: null,
    deliveryMode: 'together',
    createdAtUtc: '2026-09-05T18:00:00Z',
    isHiddenFromAsItComesQueue: false,
    itemCount: 1,
    fulfilledItemCount: 0,
    items: [stationOrderItem('a')],
    ...overrides,
  }
}

describe('the words a station tablet uses for a delivery mode', () => {
  it('names the mode that goes out together', () => {
    expect(deliveryModeKey('together')).toBe('delivery.together')
  })

  it('names the mode that goes out as it is ready', () => {
    expect(deliveryModeKey('asItComes')).toBe('delivery.asItComes')
  })

  it('marks a card that hands the order out together', () => {
    expect(deliveryModeColour('together')).toBe('together')
  })

  it('marks a card that hands each item out as it is ready', () => {
    expect(deliveryModeColour('asItComes')).toBe('individual')
  })
})

describe('picking the open items out of a station order', () => {
  const order = stationOrder({
    items: [
      stationOrderItem('a', 'Bratwurst'),
      stationOrderItem('b', 'Pommes', '2026-09-05T18:10:00Z'),
      stationOrderItem('c', 'Wasser'),
    ],
  })

  it('lists only the items nobody has handed out yet', () => {
    expect(openItemsIn(order).map((item) => item.orderItemId)).toEqual(['a', 'c'])
  })
})

describe('grouping what an employee selected', () => {
  const orders = [
    stationOrder({
      stationOrderId: 'station-order-1',
      items: [stationOrderItem('a', 'Bratwurst'), stationOrderItem('b', 'Pommes')],
    }),
    stationOrder({
      stationOrderId: 'station-order-2',
      items: [
        stationOrderItem('c', 'Wasser'),
        stationOrderItem('d', 'Bratwurst', '2026-09-05T18:20:00Z'),
      ],
    }),
  ]

  it('keeps only the selected open items and groups them by name and note', () => {
    expect(selectedUnits(orders, ['a', 'c'])).toEqual([
      { itemName: 'Bratwurst', note: null, units: 1 },
      { itemName: 'Wasser', note: null, units: 1 },
    ])
  })

  it('keeps two units of the same article apart when their notes differ', () => {
    const noted = [
      stationOrder({
        items: [
          stationOrderItem('a', 'Frankfurter', null, 'Mit Ketchup'),
          stationOrderItem('b', 'Frankfurter', null, 'Ohne Ketchup'),
        ],
      }),
    ]

    expect(selectedUnits(noted, ['a', 'b'])).toEqual([
      { itemName: 'Frankfurter', note: 'Mit Ketchup', units: 1 },
      { itemName: 'Frankfurter', note: 'Ohne Ketchup', units: 1 },
    ])
  })

  it('ignores a selected id that is done or gone', () => {
    expect(selectedUnits(orders, ['d', 'gone'])).toEqual([])
  })

  it('names the selected open items of one station order', () => {
    expect(selectedOpenItemIds(orders[1], ['c', 'd', 'gone'])).toEqual(['c'])
  })
})

describe('grouping the open items of one card', () => {
  it('keeps units with the same name and note together and separates different notes', () => {
    const items = [
      stationOrderItem('a', 'Frankfurter', null, 'Mit Ketchup'),
      stationOrderItem('b', 'Frankfurter', null, 'Mit Ketchup'),
      stationOrderItem('c', 'Frankfurter', null, 'Ohne Ketchup'),
      stationOrderItem('d', 'Bier'),
    ]

    expect(itemLines(items)).toEqual([
      { itemName: 'Bier', note: null, units: 1 },
      { itemName: 'Frankfurter', note: 'Mit Ketchup', units: 2 },
      { itemName: 'Frankfurter', note: 'Ohne Ketchup', units: 1 },
    ])
  })
})

describe('the open articles of the overview board', () => {
  it('puts the largest count first and breaks ties by name', () => {
    expect(
      linesByCount([
        { itemName: 'Wasser', note: null, units: 1 },
        { itemName: 'Frankfurter', note: null, units: 3 },
        { itemName: 'Bier', note: null, units: 3 },
        { itemName: 'Bratwurst', note: null, units: 2 },
      ]),
    ).toEqual([
      { itemName: 'Bier', note: null, units: 3 },
      { itemName: 'Frankfurter', note: null, units: 3 },
      { itemName: 'Bratwurst', note: null, units: 2 },
      { itemName: 'Wasser', note: null, units: 1 },
    ])
  })
})

describe('the words on one grouped line', () => {
  const words = (key: string, values?: Record<string, string | number>): string =>
    values === undefined ? `[${key}]` : `[${key} ${Object.values(values).join(' ')}]`

  it('names only the units when the article carries no note', () => {
    expect(itemLineText({ itemName: 'Frankfurter', note: null, units: 2 }, words)).toBe(
      '[station.itemUnits 2 Frankfurter]',
    )
  })

  it('keeps the note beside the units when the article carries one', () => {
    expect(itemLineText({ itemName: 'Frankfurter', note: 'Mit Ketchup', units: 2 }, words)).toBe(
      '[station.itemUnits 2 Frankfurter][station.unitSeparator][station.note Mit Ketchup]',
    )
  })
})

describe('the statistics above the queue', () => {
  it('counts the orders by delivery mode and keeps the open lines apart by note', () => {
    const orders = [
      stationOrder({
        stationOrderId: 'station-order-1',
        items: [
          stationOrderItem('a', 'Bratwurst'),
          stationOrderItem('b', 'Pommes'),
          stationOrderItem('c', 'Bratwurst', null, 'Ohne Senf'),
        ],
      }),
      stationOrder({
        stationOrderId: 'station-order-2',
        deliveryMode: 'asItComes',
        items: [
          stationOrderItem('d', 'Bratwurst'),
          stationOrderItem('e', 'Wasser', '2026-09-05T18:20:00Z'),
        ],
      }),
      stationOrder({
        stationOrderId: 'station-order-3',
        deliveryMode: 'asItComes',
        isHiddenFromAsItComesQueue: true,
        items: [stationOrderItem('f', 'Wasser')],
      }),
    ]

    expect(stationStats(orders)).toEqual({
      togetherOrders: 1,
      asItComesOrders: 2,
      openLines: [
        { itemName: 'Bratwurst', note: null, units: 2 },
        { itemName: 'Bratwurst', note: 'Ohne Senf', units: 1 },
        { itemName: 'Pommes', note: null, units: 1 },
        { itemName: 'Wasser', note: null, units: 1 },
      ],
    })
  })
})

describe('a selection after a reload', () => {
  it('drops the ids whose item is no longer open in the queue', () => {
    const orders = [
      stationOrder({
        items: [stationOrderItem('a', 'Bratwurst'), stationOrderItem('b', 'Pommes', '2026-09-05T18:30:00Z')],
      }),
    ]

    expect(retainOpenItemIds(['a', 'b', 'gone'], orders)).toEqual(['a'])
  })
})

describe('stationFailureKey, what the tablet says when a change did not go through', () => {
  it('asks to tap again when the laptop could not be reached', () => {
    expect(stationFailureKey({ kind: 'unreachable' })).toBe('station.actionNotReached')
  })

  it('shows the reason the laptop gave when it named one', () => {
    expect(
      stationFailureKey({
        kind: 'error',
        status: 409,
        body: {
          code: 'ItemNotFulfilled',
          messageKey: 'station.changeNotSaved',
          parameters: {},
          details: null,
        },
        raw: null,
      }),
    ).toBe('station.changeNotSaved')
  })

  it('falls back to a plain failure when the laptop named nothing', () => {
    expect(stationFailureKey({ kind: 'error', status: 500, body: null, raw: null })).toBe(
      'station.actionFailed',
    )
  })
})
