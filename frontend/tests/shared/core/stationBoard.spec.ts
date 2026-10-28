import { describe, expect, it } from 'vitest'
import {
  cancellationText,
  deliveryModeColourToken,
  deliveryModeKey,
  itemLineText,
  itemLines,
  itemsAwaitingAcknowledgementIn,
  itemsNotCancelledIn,
  itemsNotHandedOutIn,
  linesByCount,
  openItemsIn,
  retainStillSelectableItemIds,
  selectedItemIdsAwaitingAcknowledgement,
  selectedOpenItemIds,
  selectedUnits,
  stationFailureMessage,
  stationStats,
} from '../../../src/shared/core/stationBoard'
import type { ItemLine } from '../../../src/shared/core/stationBoard'
import { StationOrderQueueView, StationQueueItemView } from '../../../src/shared/api/generatedSchemas'

function articleAndUnits(lines: readonly ItemLine[]): Pick<ItemLine, 'itemName' | 'note' | 'units'>[] {
  return lines.map(({ itemName, note, units }) => ({ itemName, note, units }))
}

function line(itemName: string, note: string | null, units: number): ItemLine {
  return { key: `${itemName}|${note ?? ''}`, itemName, note, isCancelled: false, units }
}

function stationOrderItem(
  orderItemId: string,
  itemName = 'Bratwurst',
  handedOutAtUtc: string | null = null,
  note: string | null = null,
): StationQueueItemView {
  return {
    orderItemId,
    itemName,
    note,
    state: handedOutAtUtc === null ? 'open' : 'handedOut',
    stateSetAtUtc: handedOutAtUtc ?? '2026-09-05T18:00:00Z',
  }
}

function stationOrder(overrides: Partial<StationOrderQueueView> = {}): StationOrderQueueView {
  return {
    stationOrderId: 'station-order-1',
    globalOrderNumber: 137,
    stationOrderNumber: 12,
    tableName: 'Tisch 3',
    staffMemberName: 'Anna',
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
    expect(deliveryModeKey('together')).toBe('common.delivery.together')
  })

  it('names the mode that goes out as it is ready', () => {
    expect(deliveryModeKey('asItComes')).toBe('common.delivery.asItComes')
  })

  it('marks a card that hands the order out together', () => {
    expect(deliveryModeColourToken('together')).toBe('together')
  })

  it('marks a card that hands each item out as it is ready', () => {
    expect(deliveryModeColourToken('asItComes')).toBe('individual')
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
    expect(articleAndUnits(selectedUnits(orders, ['a', 'c']))).toEqual([
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

    expect(articleAndUnits(selectedUnits(noted, ['a', 'b']))).toEqual([
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
  it('keeps the lines in the order the laptop sent the items in, not in the order of their names', () => {
    const items = [
      stationOrderItem('a', 'Wasser'),
      stationOrderItem('b', 'Frankfurter', null, 'Ohne Ketchup'),
      stationOrderItem('c', 'Bier'),
      stationOrderItem('d', 'Frankfurter', null, 'Ohne Ketchup'),
      stationOrderItem('e', 'Frankfurter', null, 'Mit Ketchup'),
    ]

    expect(articleAndUnits(itemLines(items))).toEqual([
      { itemName: 'Wasser', note: null, units: 1 },
      { itemName: 'Frankfurter', note: 'Ohne Ketchup', units: 2 },
      { itemName: 'Bier', note: null, units: 1 },
      { itemName: 'Frankfurter', note: 'Mit Ketchup', units: 1 },
    ])
  })

  it('gives two lines of the same article with different notes keys of their own', () => {
    const items = [
      stationOrderItem('a', 'Frankfurter', null, 'Mit Ketchup'),
      stationOrderItem('b', 'Frankfurter', null, 'Ohne Ketchup'),
    ]

    const [withKetchup, withoutKetchup] = itemLines(items)

    expect(withKetchup.key).not.toBe(withoutKetchup.key)
  })
})

describe('the open articles of the overview board', () => {
  it('puts the largest count first and breaks ties by name', () => {
    expect(
      linesByCount([
        line('Wasser', null, 1),
        line('Frankfurter', null, 3),
        line('Bier', null, 3),
        line('Bratwurst', null, 2),
      ]),
    ).toEqual([
      line('Bier', null, 3),
      line('Frankfurter', null, 3),
      line('Bratwurst', null, 2),
      line('Wasser', null, 1),
    ])
  })

  it('orders names of equal count with capitals ignored and umlauts after Z', () => {
    expect(
      linesByCount([
        line('Zwiebel', null, 1),
        line('Äpfel', null, 1),
        line('apfel', null, 1),
        line('Birne', null, 1),
      ]).map((entry) => entry.itemName),
    ).toEqual(['apfel', 'Birne', 'Zwiebel', 'Äpfel'])
  })
})

describe('the words on one grouped line', () => {
  const words = (key: string, values?: Record<string, string | number>): string =>
    values === undefined ? `[${key}]` : `[${key} ${Object.values(values).join(' ')}]`

  it('names only the units when the article carries no note', () => {
    expect(itemLineText(line('Frankfurter', null, 2), words)).toBe(
      '[common.labels.countTimesItem 2 Frankfurter]',
    )
  })

  it('keeps the note beside the units when the article carries one', () => {
    expect(itemLineText(line('Frankfurter', 'Mit Ketchup', 2), words)).toBe(
      '[common.labels.countTimesItem 2 Frankfurter][station.board.labels.unitSeparator][common.labels.note Mit Ketchup]',
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

    const stats = stationStats(orders)

    expect(stats.togetherOrders).toBe(1)
    expect(stats.asItComesOrders).toBe(2)
    expect(articleAndUnits(stats.openLines)).toEqual([
      { itemName: 'Bratwurst', note: null, units: 2 },
      { itemName: 'Bratwurst', note: 'Ohne Senf', units: 1 },
      { itemName: 'Pommes', note: null, units: 1 },
      { itemName: 'Wasser', note: null, units: 1 },
    ])
  })
})

describe('a selection after a reload', () => {
  it('drops the ids whose item is no longer open in the queue', () => {
    const before = [stationOrder({ items: [stationOrderItem('a', 'Bratwurst'), stationOrderItem('b', 'Pommes')] })]
    const after = [
      stationOrder({
        items: [stationOrderItem('a', 'Bratwurst'), stationOrderItem('b', 'Pommes', '2026-09-05T18:30:00Z')],
      }),
    ]

    expect(retainStillSelectableItemIds(['a', 'b', 'gone'], before, after)).toEqual(['a'])
  })
})

describe('stationFailureMessage, what the tablet says when a change did not go through', () => {
  it('asks to tap again when the laptop could not be reached', () => {
    expect(stationFailureMessage({ kind: 'unreachable' })).toEqual({
      key: 'station.board.errors.actionNotReached',
      parameters: {},
    })
  })

  it('shows the reason the laptop gave when it named one, with the item it names', () => {
    expect(
      stationFailureMessage({
        kind: 'error',
        status: 409,
        body: {
          code: 'ItemWasCancelled',
          messageKey: 'errors.station.itemWasCancelled',
          parameters: { name: 'Pommes' },
          details: null,
        },
        raw: null,
      }),
    ).toEqual({ key: 'errors.station.itemWasCancelled', parameters: { name: 'Pommes' } })
  })

  it('falls back to a plain failure when the laptop named nothing', () => {
    expect(stationFailureMessage({ kind: 'error', status: 500, body: null, raw: null })).toEqual({
      key: 'common.errors.actionFailed',
      parameters: {},
    })
  })
})

describe('a station order with an item a waiter cancelled while it was open', () => {
  const cancelledPommes: StationQueueItemView = { ...stationOrderItem('c', 'Pommes'), state: 'cancelled' }
  const order = stationOrder({
    items: [stationOrderItem('a', 'Pommes'), stationOrderItem('b', 'Bier', '2026-09-05T18:30:00Z'), cancelledPommes],
  })

  it('leaves the cancelled item out of the open items', () => {
    expect(openItemsIn(order).map((item) => item.orderItemId)).toEqual(['a'])
  })

  it('keeps the cancelled item on the card beside the open one', () => {
    expect(itemsNotHandedOutIn(order).map((item) => item.orderItemId)).toEqual(['a', 'c'])
  })

  it('leaves the cancelled item out of what the order counts', () => {
    expect(itemsNotCancelledIn(order).map((item) => item.orderItemId)).toEqual(['a', 'b'])
  })

  it('never merges the cancelled pommes into the line of the open pommes', () => {
    expect(itemLines(order.items).map(({ itemName, isCancelled, units }) => ({ itemName, isCancelled, units }))).toEqual([
      { itemName: 'Pommes', isCancelled: false, units: 1 },
      { itemName: 'Bier', isCancelled: false, units: 1 },
      { itemName: 'Pommes', isCancelled: true, units: 1 },
    ])
  })

  it('leaves the cancelled item out of the overview of open lines', () => {
    expect(articleAndUnits(stationStats([order]).openLines)).toEqual([
      { itemName: 'Pommes', note: null, units: 1 },
    ])
  })

  it('drops a selected open item once it is cancelled, so the selection never turns into a confirmation', () => {
    const before = stationOrder({ items: [stationOrderItem('a', 'Pommes'), stationOrderItem('c', 'Pommes')] })

    expect(retainStillSelectableItemIds(['a', 'c'], [before], [order])).toEqual(['a'])
  })

  it('keeps a selected cancelled item selected while it still waits for its confirmation', () => {
    expect(retainStillSelectableItemIds(['c'], [order], [order])).toEqual(['c'])
  })

  it('drops a selected cancelled item once its cancellation is confirmed and it left the card', () => {
    const after = stationOrder({ items: [stationOrderItem('a', 'Pommes')] })

    expect(retainStillSelectableItemIds(['a', 'c'], [order], [after])).toEqual(['a'])
  })

  it('names the cancelled item as the one that waits for the confirmation', () => {
    expect(itemsAwaitingAcknowledgementIn(order).map((item) => item.orderItemId)).toEqual(['c'])
  })

  it('picks only the selected cancelled items for the confirmation', () => {
    expect(selectedItemIdsAwaitingAcknowledgement(order, ['a', 'c', 'elsewhere'])).toEqual(['c'])
  })
})

describe('the state line of a cancelled item', () => {
  const t = (key: string, values?: Record<string, string | number>) => `${key} ${values?.time ?? ''}`
  const at = (state: StationQueueItemView['state']): StationQueueItemView => ({
    ...stationOrderItem('a', 'Pommes'),
    state,
    stateSetAtUtc: '2026-09-05T16:42:00Z',
  })

  it.each([
    ['cancelled', 'station.board.labels.cancelledAt 18:42'],
    ['cancellationAcknowledged', 'station.board.labels.cancellationAcknowledgedAt 18:42'],
  ] as const)('reads %s as the time of that step', (state, expected) => {
    expect(cancellationText(at(state), t, 'de')).toBe(expected)
  })

  it.each(['open', 'handedOut'] as const)('gives an item that is %s no state line', (state) => {
    expect(cancellationText(at(state), t, 'de')).toBeNull()
  })
})
