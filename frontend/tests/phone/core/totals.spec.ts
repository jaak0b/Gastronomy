import { describe, expect, it } from 'vitest'
import { collapsedTotalCents, orderTotalCents } from '../../../src/phone/core/totals'
import type { BasketLineView } from '../../../src/phone/core/basket'

function basketLine(unitPriceCents: number | null): BasketLineView {
  return {
    catalogItemId: 'item-1',
    name: 'Bratwurst',
    unitPriceCents,
    note: null,
    stationId: null,
    candidateStationIds: ['station-1'],
    isSoldOut: false,
    isNoLongerOnTheMenu: false,
  }
}

describe('collapsedTotalCents', () => {
  it('charges three of an item at three times its price', () => {
    const total = collapsedTotalCents({ line: basketLine(350), quantity: 3 })

    expect(total).toBe(1050)
  })

  it('charges one of an item at its price', () => {
    const total = collapsedTotalCents({ line: basketLine(350), quantity: 1 })

    expect(total).toBe(350)
  })
})

describe('a line whose item has left the menu', () => {
  it('has no price of its own to put beside it', () => {
    const total = collapsedTotalCents({ line: basketLine(null), quantity: 2 })

    expect(total).toBeNull()
  })

  it('adds nothing to the total, so the total is what the laptop would record', () => {
    const total = orderTotalCents([basketLine(350), basketLine(null)])

    expect(total).toBe(350)
  })
})

describe('orderTotalCents', () => {
  it('adds up every position in the basket', () => {
    const total = orderTotalCents([basketLine(350), basketLine(350), basketLine(420)])

    expect(total).toBe(1120)
  })

  it('is nothing when the basket is empty', () => {
    const total = orderTotalCents([])

    expect(total).toBe(0)
  })
})
