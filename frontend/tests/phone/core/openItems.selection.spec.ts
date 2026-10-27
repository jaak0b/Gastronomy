import { describe, expect, it } from 'vitest'
import {
  cashToReturnCents,
  selectedAmountCents,
  selectionStateOf,
  shareStateOf,
  selectableItemIdsIn,
  selectableItemIdsInOrder,
  withItemToggled,
  withWholeOrder,
  withWholeTable,
  withoutItemsThatAreGone,
} from '../../../src/phone/core/openItems'
import { itemWith, recordWith, reportWith } from './openItemsFixture'

const SETTLED_AT = '2026-09-05T18:40:00Z'

describe('the items of a table the waiter can still tick', () => {
  const report = reportWith([
    recordWith([itemWith('bier', null, null, 400), itemWith('wasser', '2026-09-05T18:12:00Z', SETTLED_AT, 200)]),
    recordWith([itemWith('bratwurst', '2026-09-05T18:05:00Z', null, 350)]),
  ])

  it('are the items of every order that are not settled yet', () => {
    expect(selectableItemIdsIn(report, 'settling')).toEqual(['bier', 'bratwurst'])
  })
})

describe('what the waiter has ticked', () => {
  const report = reportWith([
    recordWith([itemWith('bratwurst', null, null, 350), itemWith('bier', null, null, 400)]),
    recordWith([itemWith('wasser', null, null, 200), itemWith('cola', null, SETTLED_AT, 150)]),
  ])

  it('adds up to the amount the guests at the table are about to hand over', () => {
    expect(selectedAmountCents(report, ['bratwurst', 'wasser'])).toBe(550)
  })

  it('counts nothing while the waiter has ticked nothing', () => {
    expect(selectedAmountCents(report, [])).toBe(0)
  })

  it('counts nothing for an item that is already settled', () => {
    expect(selectedAmountCents(report, ['cola'])).toBe(0)
  })

  it('is emptied of the items somebody else settled in the meantime', () => {
    expect(withoutItemsThatAreGone(['bratwurst', 'cola'], report, 'settling')).toEqual(['bratwurst'])
  })
})

describe('ticking one item', () => {
  const report = reportWith([
    recordWith([
      itemWith('bratwurst', null, null, 350),
      itemWith('bier', null, null, 400),
      itemWith('cola', null, SETTLED_AT, 150),
    ]),
  ])

  it('adds it to the selection', () => {
    expect(withItemToggled([], report, 'bratwurst', 'settling')).toEqual(['bratwurst'])
  })

  it('takes it back out when the waiter taps it again', () => {
    expect(withItemToggled(['bratwurst', 'bier'], report, 'bratwurst', 'settling')).toEqual(['bier'])
  })

  it('takes a second item of the table', () => {
    expect(withItemToggled(['bratwurst'], report, 'bier', 'settling')).toEqual(['bratwurst', 'bier'])
  })

  it('ignores an item that is already settled', () => {
    expect(withItemToggled(['bratwurst'], report, 'cola', 'settling')).toEqual(['bratwurst'])
  })

  it('ignores an item the table no longer shows', () => {
    expect(withItemToggled(['bratwurst'], report, 'gone', 'settling')).toEqual(['bratwurst'])
  })
})

describe('taking a whole table at once', () => {
  const report = reportWith([
    recordWith([itemWith('bratwurst', null, null, 350), itemWith('cola', null, SETTLED_AT, 150)]),
    recordWith([itemWith('bier', null, null, 400)]),
  ])

  it('ticks every item the table still owes for', () => {
    expect(withWholeTable([], report, true, 'settling')).toEqual(['bratwurst', 'bier'])
  })

  it('unticks the table it had ticked', () => {
    expect(withWholeTable(['bratwurst', 'bier'], report, false, 'settling')).toEqual([])
  })
})

describe('how much of a group of items is ticked', () => {
  it('reports none when no item of the group is ticked', () => {
    expect(selectionStateOf(['a', 'b'], ['c'])).toBe('none')
  })

  it('reports some while one item of the group is left', () => {
    expect(selectionStateOf(['a', 'b'], ['a', 'c'])).toBe('some')
  })

  it('reports all once every item of the group is ticked', () => {
    expect(selectionStateOf(['a', 'b'], ['b', 'a'])).toBe('all')
  })
})

describe('how much of a whole is covered by a count', () => {
  it('reports none for a count of zero', () => {
    expect(shareStateOf(0, 3)).toBe('none')
  })

  it('reports some for a count below the total', () => {
    expect(shareStateOf(2, 3)).toBe('some')
  })

  it('reports all when the count reaches the total', () => {
    expect(shareStateOf(3, 3)).toBe('all')
  })

  it('reports all for a whole with nothing in it, since nothing is left to do', () => {
    expect(shareStateOf(0, 0)).toBe('all')
  })
})

describe('taking a whole order at once', () => {
  const order = recordWith([
    itemWith('12-0', null, null, 350),
    itemWith('12-1', null, null, 400),
    itemWith('12-paid', null, SETTLED_AT, 200),
  ])

  it('offers only the items of the order that are not settled', () => {
    expect(selectableItemIdsInOrder(order, 'settling')).toEqual(['12-0', '12-1'])
  })

  it('offers nothing from an order whose items are all settled', () => {
    const paid = recordWith([itemWith('12-paid', null, SETTLED_AT, 200)])

    expect(selectableItemIdsInOrder(paid, 'settling')).toEqual([])
  })

  it('ticks the open items of the order and keeps the other ticks of the table', () => {
    expect(withWholeOrder(['12-2'], order, true, 'settling')).toEqual(['12-2', '12-0', '12-1'])
  })

  it('ticks the rest of an order that was partly ticked', () => {
    expect(withWholeOrder(['12-0', '12-2'], order, true, 'settling')).toEqual(['12-2', '12-0', '12-1'])
  })

  it('unticks exactly the items of the order', () => {
    expect(withWholeOrder(['12-0', '12-1', '12-2'], order, false, 'settling')).toEqual(['12-2'])
  })
})

describe('an item a waiter cancelled', () => {
  const cancelledBier = { ...itemWith('bier', null, null, 400), state: 'cancelled' as const }
  const cancelledPaidCola = { ...itemWith('cola', '2026-09-05T18:12:00Z', SETTLED_AT, 150), state: 'cancelled' as const }
  const order = recordWith([itemWith('bratwurst', null, null, 350), cancelledBier, cancelledPaidCola])
  const report = reportWith([order])

  it('is never one the waiter can tick', () => {
    expect(selectableItemIdsIn(report, 'settling')).toEqual(['bratwurst'])
    expect(selectableItemIdsInOrder(order, 'settling')).toEqual(['bratwurst'])
  })

  it('stays out of the selection when tapped', () => {
    expect(withItemToggled([], report, 'bier', 'settling')).toEqual([])
  })

  it('stays out when the whole table is ticked', () => {
    expect(withWholeTable([], report, true, 'settling')).toEqual(['bratwurst'])
  })

  it('counts nothing toward the amount', () => {
    expect(selectedAmountCents(report, ['bratwurst', 'bier'])).toBe(350)
  })
})

describe('the items a waiter can tick to cancel them', () => {
  const cancelledBier = { ...itemWith('bier', null, null, 400), state: 'cancelled' as const }
  const order = recordWith([
    itemWith('bratwurst', null, null, 350),
    itemWith('wasser', '2026-09-05T18:12:00Z', SETTLED_AT, 200),
    cancelledBier,
  ])
  const report = reportWith([order])

  it('are every item that is not cancelled, paid ones included', () => {
    expect(selectableItemIdsIn(report, 'cancelling')).toEqual(['bratwurst', 'wasser'])
    expect(selectableItemIdsInOrder(order, 'cancelling')).toEqual(['bratwurst', 'wasser'])
  })

  it('take a paid item into the selection when tapped', () => {
    expect(withItemToggled([], report, 'wasser', 'cancelling')).toEqual(['wasser'])
  })

  it('leave a cancelled item out when the whole table is ticked', () => {
    expect(withWholeTable([], report, true, 'cancelling')).toEqual(['bratwurst', 'wasser'])
  })

  it('lose an item somebody else cancelled in the meantime', () => {
    expect(withoutItemsThatAreGone(['bratwurst', 'bier'], report, 'cancelling')).toEqual(['bratwurst'])
  })
})

describe('the cash a guest gets back for cancelled items', () => {
  it('adds up what was charged for the paid items', () => {
    const items = [
      { ...itemWith('wasser', null, SETTLED_AT, 200), chargedPriceCents: 200 },
      { ...itemWith('bier', null, SETTLED_AT, 400), chargedPriceCents: 300 },
    ]

    expect(cashToReturnCents(items)).toBe(500)
  })

  it('counts nothing for an item that is not paid yet', () => {
    expect(cashToReturnCents([itemWith('bratwurst', null, null, 350)])).toBe(0)
  })
})
