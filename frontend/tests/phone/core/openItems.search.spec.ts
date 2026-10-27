import { describe, expect, it } from 'vitest'
import { searchOptionsFor } from '../../../src/phone/core/openItems'

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

