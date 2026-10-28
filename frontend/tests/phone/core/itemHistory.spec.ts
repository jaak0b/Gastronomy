import { describe, expect, it } from 'vitest'
import type { OrderItemHistoryView } from '../../../src/shared/api/generatedSchemas'
import { historyEntriesOf } from '../../../src/phone/core/itemHistory'

function historyOf(overrides: Partial<OrderItemHistoryView>): OrderItemHistoryView {
  return {
    statusChanges: [
      { state: 'open', changedAtUtc: '2026-09-05T18:00:00Z', staffMemberName: 'Anna', stationName: null },
    ],
    settledAtUtc: null,
    chargedPriceCents: null,
    paymentMethod: null,
    paymentNotice: null,
    settledByStaffMemberName: null,
    ...overrides,
  }
}

describe('the history of an item that went through the station and back', () => {
  it('names the first status the order, a later open status a put back, and each author', () => {
    const history = historyOf({
      statusChanges: [
        { state: 'open', changedAtUtc: '2026-09-05T18:00:00Z', staffMemberName: 'Anna', stationName: null },
        { state: 'handedOut', changedAtUtc: '2026-09-05T18:02:00Z', staffMemberName: null, stationName: 'Küche' },
        { state: 'open', changedAtUtc: '2026-09-05T18:03:00Z', staffMemberName: null, stationName: 'Küche' },
        { state: 'cancelled', changedAtUtc: '2026-09-05T18:06:00Z', staffMemberName: 'Anna', stationName: null },
        { state: 'cancellationAcknowledged', changedAtUtc: '2026-09-05T18:07:00Z', staffMemberName: null, stationName: 'Küche' },
      ],
    })

    expect(historyEntriesOf(history)).toEqual([
      { kind: 'ordered', atUtc: '2026-09-05T18:00:00Z', by: 'Anna' },
      { kind: 'handedOut', atUtc: '2026-09-05T18:02:00Z', by: 'Küche' },
      { kind: 'putBack', atUtc: '2026-09-05T18:03:00Z', by: 'Küche' },
      { kind: 'cancelled', atUtc: '2026-09-05T18:06:00Z', by: 'Anna' },
      { kind: 'cancellationAcknowledged', atUtc: '2026-09-05T18:07:00Z', by: 'Küche' },
    ])
  })
})

describe('the payment in the history of an item', () => {
  it('stands between the status changes at the time it was taken', () => {
    const history = historyOf({
      statusChanges: [
        { state: 'open', changedAtUtc: '2026-09-05T18:00:00Z', staffMemberName: 'Anna', stationName: null },
        { state: 'cancelled', changedAtUtc: '2026-09-05T18:06:00Z', staffMemberName: 'Anna', stationName: null },
      ],
      settledAtUtc: '2026-09-05T18:04:00Z',
      chargedPriceCents: 380,
      paymentMethod: 'card',
      paymentNotice: 'Stammgast',
      settledByStaffMemberName: 'Berta',
    })

    expect(historyEntriesOf(history).map((entry) => entry.kind)).toEqual(['ordered', 'paid', 'cancelled'])
  })

  it('carries the amount, the method, the reason and who took it', () => {
    const history = historyOf({
      settledAtUtc: '2026-09-05T18:04:00Z',
      chargedPriceCents: 380,
      paymentMethod: 'card',
      paymentNotice: 'Stammgast',
      settledByStaffMemberName: 'Berta',
    })

    expect(historyEntriesOf(history)[1]).toEqual({
      kind: 'paid',
      atUtc: '2026-09-05T18:04:00Z',
      by: 'Berta',
      amountCents: 380,
      paymentMethod: 'card',
      paymentNotice: 'Stammgast',
    })
  })

  it('is left out while nobody has paid for the item', () => {
    expect(historyEntriesOf(historyOf({})).map((entry) => entry.kind)).toEqual(['ordered'])
  })
})
