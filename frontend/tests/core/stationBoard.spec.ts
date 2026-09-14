import { describe, expect, it } from 'vitest'
import {
  deliveryModeClass,
  deliveryModeKey,
  itemUnits,
  openItemsOf,
  retainOpenItemIds,
  selectedOpenItemIds,
  selectedUnits,
  stationFailureKey,
  stationStats,
} from '../../src/core/stationBoard'
import type { StationSlice, StationSliceItem } from '../../src/core/apiTypes'

function sliceItem(
  orderItemId: string,
  itemName = 'Bratwurst',
  fulfilledAtUtc: string | null = null,
): StationSliceItem {
  return { orderItemId, itemName, note: null, fulfilledAtUtc }
}

function slice(overrides: Partial<StationSlice> = {}): StationSlice {
  return {
    stationOrderId: 'slice-1',
    globalOrderNumber: 137,
    stationOrderNumber: 12,
    tableName: 'Tisch 3',
    note: null,
    deliveryMode: 'together',
    createdAtUtc: '2026-09-05T18:00:00Z',
    isHiddenFromAsItComesQueue: false,
    itemCount: 1,
    fulfilledItemCount: 0,
    items: [sliceItem('a')],
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
    expect(deliveryModeClass('together')).toBe('mode-together')
  })

  it('marks a card that hands each item out as it is ready', () => {
    expect(deliveryModeClass('asItComes')).toBe('mode-as-it-comes')
  })
})

describe('picking the open items out of a slice', () => {
  const order = slice({
    items: [
      sliceItem('a', 'Bratwurst'),
      sliceItem('b', 'Pommes', '2026-09-05T18:10:00Z'),
      sliceItem('c', 'Wasser'),
    ],
  })

  it('lists only the items nobody has handed out yet', () => {
    expect(openItemsOf(order).map((item) => item.orderItemId)).toEqual(['a', 'c'])
  })
})

describe('counting units of the same item', () => {
  it('adds up how many units of each name are in the list', () => {
    const items = [
      sliceItem('a', 'Bratwurst'),
      sliceItem('b', 'Wasser'),
      sliceItem('c', 'Bratwurst'),
      sliceItem('d', 'Wasser'),
      sliceItem('e', 'Bratwurst'),
    ]

    expect(itemUnits(items)).toEqual([
      { itemName: 'Bratwurst', units: 3 },
      { itemName: 'Wasser', units: 2 },
    ])
  })

  it('counts nothing when there is nothing', () => {
    expect(itemUnits([])).toEqual([])
  })
})

describe('grouping what an employee selected', () => {
  const orders = [
    slice({
      stationOrderId: 'slice-1',
      items: [sliceItem('a', 'Bratwurst'), sliceItem('b', 'Pommes')],
    }),
    slice({
      stationOrderId: 'slice-2',
      items: [
        sliceItem('c', 'Wasser'),
        sliceItem('d', 'Bratwurst', '2026-09-05T18:20:00Z'),
      ],
    }),
  ]

  it('keeps only the selected open items and groups them by name', () => {
    expect(selectedUnits(orders, ['a', 'c'])).toEqual([
      { itemName: 'Bratwurst', units: 1 },
      { itemName: 'Wasser', units: 1 },
    ])
  })

  it('ignores a selected id that is done or gone', () => {
    expect(selectedUnits(orders, ['d', 'gone'])).toEqual([])
  })

  it('names the selected open items of one slice', () => {
    expect(selectedOpenItemIds(orders[1], ['c', 'd', 'gone'])).toEqual(['c'])
  })
})

describe('the statistics above the queue', () => {
  it('counts the orders by delivery mode and the open units by item name', () => {
    const orders = [
      slice({
        stationOrderId: 'slice-1',
        items: [sliceItem('a', 'Bratwurst'), sliceItem('b', 'Pommes')],
      }),
      slice({
        stationOrderId: 'slice-2',
        deliveryMode: 'asItComes',
        items: [
          sliceItem('c', 'Bratwurst'),
          sliceItem('d', 'Wasser', '2026-09-05T18:20:00Z'),
        ],
      }),
      slice({
        stationOrderId: 'slice-3',
        deliveryMode: 'asItComes',
        isHiddenFromAsItComesQueue: true,
        items: [sliceItem('e', 'Wasser')],
      }),
    ]

    expect(stationStats(orders)).toEqual({
      togetherOrders: 1,
      asItComesOrders: 2,
      openUnits: [
        { itemName: 'Bratwurst', units: 2 },
        { itemName: 'Pommes', units: 1 },
        { itemName: 'Wasser', units: 1 },
      ],
    })
  })
})

describe('a selection after a reload', () => {
  it('drops the ids whose item is no longer open in the queue', () => {
    const orders = [
      slice({
        items: [sliceItem('a', 'Bratwurst'), sliceItem('b', 'Pommes', '2026-09-05T18:30:00Z')],
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
