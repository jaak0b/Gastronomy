import { describe, expect, it } from 'vitest'
import { isShownAsPaid, searchOptionsFor } from '../../../src/phone/core/openItems'
import { itemWith } from './openItemsFixture'

describe('what the settle and cancel screens ask the laptop for', () => {
  it('asks for the items that are neither paid nor cancelled when taking payment', () => {
    expect(searchOptionsFor('settling', false)).toEqual({ paymentStatus: 'notPaid', includeCancelled: false })
  })

  it('asks for the items that are not cancelled when cancelling', () => {
    expect(searchOptionsFor('cancelling', false)).toEqual({ includeCancelled: false })
  })

  it.each(['settling', 'cancelling'] as const)('asks for every item while "Alle Positionen anzeigen" is ticked on %s', (purpose) => {
    expect(searchOptionsFor(purpose, true)).toEqual({})
  })
})

describe('a paid item on the settle and cancel screens', () => {
  const paidBier = itemWith('bier', null, '2026-09-05T18:40:00Z', 400)

  it('shows as paid when taking payment', () => {
    expect(isShownAsPaid(paidBier, 'settling')).toBe(true)
  })

  it('looks like any other item when cancelling', () => {
    expect(isShownAsPaid(paidBier, 'cancelling')).toBe(false)
  })

  it('does not show as paid while it is still open', () => {
    expect(isShownAsPaid(itemWith('bier', null, null, 400), 'settling')).toBe(false)
  })
})
