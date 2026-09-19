import { describe, expect, it } from 'vitest'
import { splitSettlement } from '../../src/core/settlementSplit'

function linesOf(...unitPriceCents: number[]) {
  return unitPriceCents.map((unitPriceCents) => ({ unitPriceCents }))
}

describe('splitting what a table paid across its lines', () => {
  it('gives every line its own price when the full price was paid', () => {
    const split = splitSettlement(700, linesOf(350, 350), '')

    expect(split).toEqual([
      { paidPriceCents: 350, paymentNotice: null },
      { paidPriceCents: 350, paymentNotice: null },
    ])
  })

  it('splits a smaller amount in proportion to the prices', () => {
    const split = splitSettlement(500, linesOf(400, 600), '')

    expect(split.map((line) => line.paidPriceCents)).toEqual([200, 300])
  })

  it('hands the odd cents out one by one from the first line on', () => {
    const split = splitSettlement(1000, linesOf(350, 350, 350), '')

    expect(split.map((line) => line.paidPriceCents)).toEqual([334, 333, 333])
  })

  it('splits a hundred cents evenly over three free lines, the rest to the first', () => {
    const split = splitSettlement(100, linesOf(0, 0, 0), '')

    expect(split.map((line) => line.paidPriceCents)).toEqual([34, 33, 33])
  })

  it('allows a guest to round up and spreads the extra in proportion', () => {
    const split = splitSettlement(800, linesOf(350, 350), '')

    expect(split.map((line) => line.paidPriceCents)).toEqual([400, 400])
  })

  it('writes the trimmed reason on every line that falls short', () => {
    const split = splitSettlement(300, linesOf(350, 150), '  Stammgast  ')

    expect(split).toEqual([
      { paidPriceCents: 210, paymentNotice: 'Stammgast' },
      { paidPriceCents: 90, paymentNotice: 'Stammgast' },
    ])
  })

  it('leaves the notice off a line that was paid in full beside a short one', () => {
    const split = splitSettlement(399, linesOf(200, 200), 'Stammgast')

    expect(split).toEqual([
      { paidPriceCents: 200, paymentNotice: null },
      { paidPriceCents: 199, paymentNotice: 'Stammgast' },
    ])
  })

  it('writes no notice anywhere while the reason holds only spaces', () => {
    const split = splitSettlement(300, linesOf(350, 150), '   ')

    expect(split.map((line) => line.paymentNotice)).toEqual([null, null])
  })

  it('splits nothing over no lines', () => {
    expect(splitSettlement(500, [], 'Stammgast')).toEqual([])
  })
})
