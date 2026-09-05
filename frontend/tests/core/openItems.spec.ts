import { describe, expect, it } from 'vitest'
import type { OpenTable } from '../../src/core/apiTypes'
import {
  isPaymentNoticeWritten,
  isTheWholeTableSelected,
  itemIdsOfTable,
  noticeAfterSettling,
  selectedAmountCents,
  withItemToggled,
  withWholeTable,
  withoutItemsThatAreGone,
} from '../../src/core/openItems'

function tableWith(tableName: string, prices: number[]): OpenTable {
  return {
    tableName,
    openAmountCents: prices.reduce((total, price) => total + price, 0),
    givenAwayAmountCents: 0,
    givenAwayItems: [],
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
  it('adds it to the selection', () => {
    expect(withItemToggled([], 'item-1')).toEqual(['item-1'])
  })

  it('takes it back out when the waiter taps it again', () => {
    expect(withItemToggled(['item-1', 'item-2'], 'item-1')).toEqual(['item-2'])
  })
})

describe('taking a whole table at once', () => {
  it('ticks every item the table still owes for', () => {
    const table = tableWith('Tisch 12', [350, 400])

    expect(withWholeTable([], table, true).sort()).toEqual(itemIdsOfTable(table).sort())
  })

  it('leaves the items of other tables alone', () => {
    const twelve = tableWith('Tisch 12', [350])

    const selected = withWholeTable(['Tisch 3-0'], twelve, true)

    expect(selected).toContain('Tisch 3-0')
  })

  it('unticks the table again without touching the other tables', () => {
    const twelve = tableWith('Tisch 12', [350])

    const selected = withWholeTable(['Tisch 3-0', 'Tisch 12-0'], twelve, false)

    expect(selected).toEqual(['Tisch 3-0'])
  })

  it('reports the table as fully ticked once every item is ticked', () => {
    const table = tableWith('Tisch 12', [350, 400])

    expect(isTheWholeTableSelected(table, itemIdsOfTable(table))).toBe(true)
  })

  it('reports the table as not fully ticked while one item is left', () => {
    const table = tableWith('Tisch 12', [350, 400])

    expect(isTheWholeTableSelected(table, ['Tisch 12-0'])).toBe(false)
  })
})

describe('what the waiter is told after settling', () => {
  it('warns about every item somebody else had already taken, even when the rest went through', () => {
    const notice = noticeAfterSettling({
      settledOrderItemIds: ['item-1', 'item-2'],
      alreadySettledOrderItemIds: ['item-3', 'item-4', 'item-5'],
      otherPhonesWereTold: true,
    })

    expect(notice).toEqual({
      key: 'openItems.someWereAlreadySettled',
      parameters: { count: 3 },
      count: 3,
    })
  })

  it('warns about the clash when nothing at all was left to settle', () => {
    const notice = noticeAfterSettling({
      settledOrderItemIds: [],
      alreadySettledOrderItemIds: ['item-1'],
      otherPhonesWereTold: true,
    })

    expect(notice?.key).toBe('openItems.someWereAlreadySettled')
  })

  it('says the other phones were not told when the laptop could not reach them', () => {
    const notice = noticeAfterSettling({
      settledOrderItemIds: ['item-1'],
      alreadySettledOrderItemIds: [],
      otherPhonesWereTold: false,
    })

    expect(notice).toEqual({
      key: 'openItems.otherPhonesWereNotTold',
      parameters: {},
      count: null,
    })
  })

  it('says nothing when the whole selection was settled and every phone was told', () => {
    const notice = noticeAfterSettling({
      settledOrderItemIds: ['item-1'],
      alreadySettledOrderItemIds: [],
      otherPhonesWereTold: true,
    })

    expect(notice).toBeNull()
  })
})

describe('the reason a table pays nothing', () => {
  it('counts as written when the waiter typed something', () => {
    expect(isPaymentNoticeWritten('Essen fuer die Kapelle')).toBe(true)
  })

  it('does not count as written when the field holds only spaces', () => {
    expect(isPaymentNoticeWritten('   ')).toBe(false)
  })
})
