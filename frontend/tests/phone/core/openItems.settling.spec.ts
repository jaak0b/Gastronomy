import { describe, expect, it } from 'vitest'
import { canTheAmountBeSettled, notCancelledItemCountIn, isAPaymentMethodNeeded, isPaymentNoticeNeeded, isPaymentNoticeWritten, noticeAfterSettling, producedCountIn } from '../../../src/phone/core/openItems'
import { itemWith, recordWith } from './openItemsFixture'

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
      key: 'phone.openItems.messages.someWereAlreadySettled',
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

    expect(notice?.key).toBe('phone.openItems.messages.someWereAlreadySettled')
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

describe('the done counter of one order', () => {
  it('counts the positions that have left the station', () => {
    const order = recordWith([
      itemWith('item-1', '2026-09-05T18:05:00Z', null, 350),
      itemWith('item-2', null, null, 350),
    ])

    expect(producedCountIn(order)).toBe(1)
  })

  it('leaves a cancelled position out of the positions it counts', () => {
    const order = recordWith([
      itemWith('item-1', '2026-09-05T18:05:00Z', null, 350),
      { ...itemWith('item-2', null, null, 350), state: 'cancelled' },
    ])

    expect(notCancelledItemCountIn(order)).toBe(1)
  })
})

describe('whether settling asks how the table paid', () => {
  it('asks for cash or card when the table pays something', () => {
    expect(isAPaymentMethodNeeded(1)).toBe(true)
  })

  it('asks nothing when the table pays nothing', () => {
    expect(isAPaymentMethodNeeded(0)).toBe(false)
  })
})
