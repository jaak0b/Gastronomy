import { describe, expect, it } from 'vitest'
import { formatPrice, lineTotalCents, orderTotalCents } from '../../src/core/totals'
import type { BasketLineView } from '../../src/core/basket'

function basketLine(unitPriceCents: number, quantity: number): BasketLineView {
  return {
    catalogItemId: 'item-1',
    name: 'Bratwurst',
    unitPriceCents,
    quantity,
    note: null,
    productionLocationId: null,
    candidateLocationIds: ['location-1'],
    isSoldOut: false,
    isNoLongerOnTheMenu: false,
  }
}

describe('lineTotalCents', () => {
  it('charges three of an item at three times its price', () => {
    const total = lineTotalCents(basketLine(350, 3))

    expect(total).toBe(1050)
  })

  it('charges one of an item at its price', () => {
    const total = lineTotalCents(basketLine(350, 1))

    expect(total).toBe(350)
  })
})

describe('orderTotalCents', () => {
  it('adds up every line in the basket', () => {
    const total = orderTotalCents([basketLine(350, 2), basketLine(420, 1)])

    expect(total).toBe(1120)
  })

  it('is nothing when the basket is empty', () => {
    const total = orderTotalCents([])

    expect(total).toBe(0)
  })
})

describe('formatPrice', () => {
  it('writes ten euros fifty with a comma and a trailing euro sign in German', () => {
    const formatted = formatPrice(1050, 'de')

    expect(formatted).toBe('10,50 €')
  })

  it('writes ten euros fifty with a point and a leading euro sign in English', () => {
    const formatted = formatPrice(1050, 'en')

    expect(formatted).toBe('€10.50')
  })

  it('keeps both decimals on a whole euro amount in German', () => {
    const formatted = formatPrice(400, 'de')

    expect(formatted).toBe('4,00 €')
  })

  it('keeps both decimals on a whole euro amount in English', () => {
    const formatted = formatPrice(400, 'en')

    expect(formatted).toBe('€4.00')
  })

  it('groups a sum above a thousand euros with a point in German', () => {
    const formatted = formatPrice(123456, 'de')

    expect(formatted).toBe('1.234,56 €')
  })

  it('groups a sum above a thousand euros with a comma in English', () => {
    const formatted = formatPrice(123456, 'en')

    expect(formatted).toBe('€1,234.56')
  })

  it('writes nothing owed as zero in German', () => {
    const formatted = formatPrice(0, 'de')

    expect(formatted).toBe('0,00 €')
  })
})
