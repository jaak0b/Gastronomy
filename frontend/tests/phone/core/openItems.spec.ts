import { describe, expect, it } from 'vitest'
import { OpenTableView, TableOrderRecordItemView, TableOrderRecordView, TableOrderReportView } from '../../../src/shared/api/generatedSchemas'
import {

  canTheAmountBeSettled,
  isHeldBackByAnotherTable,
  isPaymentNoticeNeeded,
  isPaymentNoticeWritten,
  isTheWholeTableSelected,
  itemIdsAtTable,
  noticeAfterSettling,
  openTableInReport,
  positionStateOf,
  producedCountIn,
  productionStateOf,
  selectedAmountCents,
  tableHoldingTheSelection,
  tableForItem,
  withItemToggled,
  withWholeTable,
  withoutItemsThatAreGone,
} from '../../../src/phone/core/openItems'

function itemWith(
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

function recordWith(items: TableOrderRecordItemView[]): TableOrderRecordView {
  return {
    orderId: 'order-1',
    globalOrderNumber: 1,
    createdAtUtc: '2026-09-05T18:00:00Z',
    staffMemberName: 'Anna',
    items,
  }
}

function tableWith(tableName: string, prices: number[]): OpenTableView {
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

describe('what the waiter has ticked', () => {
  it('adds up to the amount the guests at the table are about to hand over', () => {
    const table = tableWith('Tisch 12', [350, 400, 200])

    const total = selectedAmountCents([table], ['Tisch 12-0', 'Tisch 12-2'])

    expect(total).toBe(550)
  })

  it('adds up across the tables that are open at the same time', () => {
    const twelve = tableWith('Tisch 12', [350])
    const three = tableWith('Tisch 3', [400])

    const total = selectedAmountCents([twelve, three], ['Tisch 12-0', 'Tisch 3-0'])

    expect(total).toBe(750)
  })

  it('counts nothing while the waiter has ticked nothing', () => {
    expect(selectedAmountCents([tableWith('Tisch 12', [350])], [])).toBe(0)
  })

  it('is emptied of the items somebody else settled in the meantime', () => {
    const table = tableWith('Tisch 12', [350])

    const remaining = withoutItemsThatAreGone(['Tisch 12-0', 'Tisch 3-0'], [table])

    expect(remaining).toEqual(['Tisch 12-0'])
  })
})

describe('ticking one item', () => {
  const tables = [tableWith('12', [350, 400]), tableWith('123', [500])]

  it('adds it to the selection', () => {
    expect(withItemToggled([], tables, '12-0')).toEqual(['12-0'])
  })

  it('takes it back out when the waiter taps it again', () => {
    expect(withItemToggled(['12-0', '12-1'], tables, '12-0')).toEqual(['12-1'])
  })

  it('takes a second item of the table that already holds the selection', () => {
    expect(withItemToggled(['12-0'], tables, '12-1')).toEqual(['12-0', '12-1'])
  })

  it('ignores an item of another table, so one settlement can never span two tables', () => {
    expect(withItemToggled(['12-0'], tables, '123-0')).toEqual(['12-0'])
  })

  it('starts on any table again once nothing is ticked', () => {
    expect(withItemToggled([], tables, '123-0')).toEqual(['123-0'])
  })

  it('ignores an item no table still shows, because it is no longer open', () => {
    expect(withItemToggled(['12-0'], tables, 'gone')).toEqual(['12-0'])
  })
})

describe('taking a whole table at once', () => {
  const twelve = tableWith('12', [350, 400])
  const hundredAndTwentyThree = tableWith('123', [500])
  const tables = [twelve, hundredAndTwentyThree]

  it('ticks every item the table still owes for', () => {
    expect(withWholeTable([], tables, twelve, true).sort()).toEqual(itemIdsAtTable(twelve).sort())
  })

  it('ignores the table while another table holds the selection', () => {
    expect(withWholeTable(['123-0'], tables, twelve, true)).toEqual(['123-0'])
  })

  it('unticks the table it had ticked', () => {
    expect(withWholeTable(['12-0', '12-1'], tables, twelve, false)).toEqual([])
  })

  it('reports the table as fully ticked once every item is ticked', () => {
    const table = tableWith('Tisch 12', [350, 400])

    expect(isTheWholeTableSelected(table, itemIdsAtTable(table))).toBe(true)
  })

  it('reports the table as not fully ticked while one item is left', () => {
    const table = tableWith('Tisch 12', [350, 400])

    expect(isTheWholeTableSelected(table, ['Tisch 12-0'])).toBe(false)
  })
})

describe('what the waiter is told after settling', () => {
  const thePhoneSent = [
    { orderItemId: 'item-1', paidPriceCents: 350, paymentNotice: null },
    { orderItemId: 'item-2', paidPriceCents: 350, paymentNotice: null },
  ]

  it('names the count and the amount to hand back when somebody else had taken the items', () => {
    const notice = noticeAfterSettling(
      {
        settledOrderItemIds: ['item-1'],
        reappliedOrderItemIds: [],
        alreadySettledByOthersOrderItemIds: ['item-2'],
      },
      thePhoneSent,
      'de',
    )

    expect(notice).toEqual({
      key: 'openItems.someWereAlreadySettled',
      parameters: { count: 1, amount: '3,50 €' },
      count: 1,
    })
  })

  it('adds up only the lines this phone sent for exactly the ids somebody else had taken', () => {
    const notice = noticeAfterSettling(
      {
        settledOrderItemIds: [],
        reappliedOrderItemIds: [],
        alreadySettledByOthersOrderItemIds: ['item-3'],
      },
      [
        { orderItemId: 'item-1', paidPriceCents: 350, paymentNotice: null },
        { orderItemId: 'item-3', paidPriceCents: 150, paymentNotice: 'Stammgast' },
        { orderItemId: 'item-4', paidPriceCents: 900, paymentNotice: null },
      ],
      'de',
    )

    expect(notice?.parameters).toEqual({ count: 1, amount: '1,50 €' })
  })

  it('formats the amount the way the reader counts money', () => {
    const notice = noticeAfterSettling(
      {
        settledOrderItemIds: [],
        reappliedOrderItemIds: [],
        alreadySettledByOthersOrderItemIds: ['item-1'],
      },
      [{ orderItemId: 'item-1', paidPriceCents: 350, paymentNotice: null }],
      'en',
    )

    expect(notice?.parameters).toEqual({ count: 1, amount: '€3.50' })
  })

  it('says nothing when the only clash is this phone finding its own earlier settlement', () => {
    const notice = noticeAfterSettling(
      {
        settledOrderItemIds: [],
        reappliedOrderItemIds: ['item-1', 'item-2'],
        alreadySettledByOthersOrderItemIds: [],
      },
      thePhoneSent,
      'de',
    )

    expect(notice).toBeNull()
  })

  it('warns about the clash when nothing at all was left to settle', () => {
    const notice = noticeAfterSettling(
      {
        settledOrderItemIds: [],
        reappliedOrderItemIds: [],
        alreadySettledByOthersOrderItemIds: ['item-1'],
      },
      thePhoneSent,
      'de',
    )

    expect(notice?.key).toBe('openItems.someWereAlreadySettled')
  })

  it('says nothing when the whole selection was settled', () => {
    const notice = noticeAfterSettling(
      {
        settledOrderItemIds: ['item-1'],
        reappliedOrderItemIds: [],
        alreadySettledByOthersOrderItemIds: [],
      },
      thePhoneSent,
      'de',
    )

    expect(notice).toBeNull()
  })
})

describe('the reason a table pays less than it owes', () => {
  it('counts as written when the waiter typed something', () => {
    expect(isPaymentNoticeWritten('Essen fuer die Kapelle')).toBe(true)
  })

  it('does not count as written when the field holds only spaces', () => {
    expect(isPaymentNoticeWritten('   ')).toBe(false)
  })

  it('is needed when the table hands over less than the selection costs', () => {
    expect(isPaymentNoticeNeeded(2000, 20000)).toBe(true)
  })

  it('is needed when the table hands over nothing at all', () => {
    expect(isPaymentNoticeNeeded(0, 700)).toBe(true)
  })

  it('is not needed when the table hands over the full price', () => {
    expect(isPaymentNoticeNeeded(700, 700)).toBe(false)
  })

  it('is not needed when the guest rounds up', () => {
    expect(isPaymentNoticeNeeded(1000, 700)).toBe(false)
  })
})

describe('whether the phone may send the amount the waiter typed', () => {
  it('refuses an amount the field could not be read as', () => {
    expect(canTheAmountBeSettled(null, 'Stammgast', 700)).toBe(false)
  })

  it('refuses a smaller amount while no reason is typed', () => {
    expect(canTheAmountBeSettled(200, '', 700)).toBe(false)
  })

  it('refuses a smaller amount whose reason is only spaces', () => {
    expect(canTheAmountBeSettled(200, '   ', 700)).toBe(false)
  })

  it('allows a smaller amount once a reason stands beside it', () => {
    expect(canTheAmountBeSettled(200, 'Stammgast', 700)).toBe(true)
  })

  it('allows the full price with no reason', () => {
    expect(canTheAmountBeSettled(700, '', 700)).toBe(true)
  })

  it('allows more than the full price with no reason', () => {
    expect(canTheAmountBeSettled(1000, '', 700)).toBe(true)
  })
})

describe('which table a selection belongs to', () => {
  it('names no table while nothing is ticked', () => {
    const tables = [tableWith('1', [300, 300]), tableWith('123', [500])]

    expect(tableHoldingTheSelection(tables, [])).toBeNull()
  })

  it('names the table whose item is ticked', () => {
    const tables = [tableWith('1', [300, 300]), tableWith('123', [500])]

    expect(tableHoldingTheSelection(tables, ['123-0'])).toBe('123')
  })

  it('still names that table when several of its items are ticked', () => {
    const tables = [tableWith('1', [300, 300]), tableWith('123', [500])]

    expect(tableHoldingTheSelection(tables, ['1-0', '1-1'])).toBe('1')
  })

  it('names no table when the ticked item is no longer open anywhere', () => {
    const tables = [tableWith('1', [300])]

    expect(tableHoldingTheSelection(tables, ['gone'])).toBeNull()
  })
})

describe('which table an item belongs to', () => {
  const tables = [tableWith('1', [300, 300]), tableWith('123', [500])]

  it('names the table the item is still open at', () => {
    expect(tableForItem(tables, '123-0')).toBe('123')
  })

  it('names no table for an item the list no longer holds', () => {
    expect(tableForItem(tables, 'gone')).toBeNull()
  })
})

describe('whether a table is held back by the table holding the selection', () => {
  const tables = [tableWith('1', [300, 300]), tableWith('123', [500])]

  it('holds every other table back while one table has something ticked', () => {
    expect(isHeldBackByAnotherTable(tables, ['1-0'], '123')).toBe(true)
  })

  it('leaves the table that holds the selection alone', () => {
    expect(isHeldBackByAnotherTable(tables, ['1-0'], '1')).toBe(false)
  })

  it('holds no table back while nothing is ticked', () => {
    expect(isHeldBackByAnotherTable(tables, [], '123')).toBe(false)
  })
})

describe('the open table inside what the laptop knows about one table', () => {
  const report: TableOrderReportView = {
    tableName: 'Tisch 12',
    openAmountCents: 750,
    orders: [
      recordWith([
        itemWith('item-open', null, null, 400),
        itemWith('item-settled', '2026-09-05T18:12:00Z', '2026-09-05T18:30:00Z', 200),
      ]),
      recordWith([itemWith('item-produced', '2026-09-05T18:05:00Z', null, 350)]),
    ],
  }

  it('keeps the table name and what the table still owes', () => {
    const table = openTableInReport(report)

    expect(table.tableName).toBe('Tisch 12')
    expect(table.openAmountCents).toBe(750)
  })

  it('lists the positions of every order that is not settled yet', () => {
    const table = openTableInReport(report)

    expect(table.items.map((item) => item.orderItemId)).toEqual(['item-open', 'item-produced'])
  })

  it('leaves a settled position out, so the waiter cannot tick it again', () => {
    const table = openTableInReport(report)

    expect(table.items.map((item) => item.orderItemId)).not.toContain('item-settled')
  })
})

describe('how far the positions of one order are produced', () => {
  it('says none while nothing has left the station', () => {
    expect(productionStateOf(recordWith([itemWith('item-1', null, null, 350)]))).toBe('none')
  })

  it('says some while part of the order has left the station', () => {
    const order = recordWith([
      itemWith('item-1', '2026-09-05T18:05:00Z', null, 350),
      itemWith('item-2', null, null, 350),
    ])

    expect(productionStateOf(order)).toBe('some')
  })

  it('says all once every position has left the station', () => {
    const order = recordWith([
      itemWith('item-1', '2026-09-05T18:05:00Z', null, 350),
      itemWith('item-2', '2026-09-05T18:06:00Z', null, 350),
    ])

    expect(productionStateOf(order)).toBe('all')
  })

  it('counts the positions that have left the station', () => {
    const order = recordWith([
      itemWith('item-1', '2026-09-05T18:05:00Z', null, 350),
      itemWith('item-2', null, null, 350),
    ])

    expect(producedCountIn(order)).toBe(1)
  })

  it('reads a position the station has handed out as produced', () => {
    expect(positionStateOf(itemWith('item-1', '2026-09-05T18:05:00Z', null, 350))).toBe('produced')
  })

  it('reads a position still at the station as not produced', () => {
    expect(positionStateOf(itemWith('item-1', null, null, 350))).toBe('notProduced')
  })
})
