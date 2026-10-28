import { describe, expect, it } from 'vitest'
import { defaultFilterFor } from '../../../src/phone/core/openItems'

describe('the filter the settle and cancel screens open with', () => {
  it('asks for the items that are neither paid nor cancelled when taking payment', () => {
    expect(defaultFilterFor('settling')).toEqual({ orderStatus: null, paymentStatus: 'notPaid', includeCancelled: false })
  })

  it('asks for every item that is not cancelled, paid or not, when cancelling', () => {
    expect(defaultFilterFor('cancelling')).toEqual({ orderStatus: null, paymentStatus: null, includeCancelled: false })
  })
})
