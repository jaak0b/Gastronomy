import { describe, expect, it } from 'vitest'
import { isHeldBackByAnotherTable, itemIdsAtTable, unsettledItemIdsInOrder, selectedAmountCents, selectionStateOf, shareStateOf, tableHoldingTheSelection, tableForItem, withItemToggled, withWholeOrder, withWholeTable, withoutItemsThatAreGone } from '../../../src/phone/core/openItems'
import { itemWith, recordWith, tableWith } from './openItemsFixture'

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
})

describe('taking a whole order at once', () => {
  const twelve = tableWith('12', [350, 400, 500])
  const hundredAndTwentyThree = tableWith('123', [500])
  const tables = [twelve, hundredAndTwentyThree]
  const order = recordWith([
    itemWith('12-0', null, null, 350),
    itemWith('12-1', null, null, 400),
    itemWith('12-paid', null, '2026-09-05T18:40:00Z', 200),
  ])

  it('offers only the items of the order that are not settled', () => {
    expect(unsettledItemIdsInOrder(order)).toEqual(['12-0', '12-1'])
  })

  it('offers nothing from an order whose items are all settled', () => {
    const paid = recordWith([itemWith('12-paid', null, '2026-09-05T18:40:00Z', 200)])

    expect(unsettledItemIdsInOrder(paid)).toEqual([])
  })

  it('ticks the open items of the order and keeps the other ticks of the table', () => {
    expect(withWholeOrder(['12-2'], tables, '12', order, true)).toEqual(['12-2', '12-0', '12-1'])
  })

  it('ticks the rest of an order that was partly ticked', () => {
    expect(withWholeOrder(['12-0', '12-2'], tables, '12', order, true)).toEqual([
      '12-2',
      '12-0',
      '12-1',
    ])
  })

  it('unticks exactly the items of the order', () => {
    expect(withWholeOrder(['12-0', '12-1', '12-2'], tables, '12', order, false)).toEqual(['12-2'])
  })

  it('ignores the order while another table holds the selection', () => {
    expect(withWholeOrder(['123-0'], tables, '12', order, true)).toEqual(['123-0'])
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
