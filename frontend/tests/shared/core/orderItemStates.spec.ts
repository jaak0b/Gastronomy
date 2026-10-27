import { describe, expect, it } from 'vitest'
import { isCancelled, isHandedOut, isOpen } from '../../../src/shared/core/orderItemStates'

describe('the state an order item is in', () => {
  it.each([
    ['open', true, false, false],
    ['handedOut', false, true, false],
    ['cancelled', false, false, true],
    ['cancellationAcknowledged', false, false, true],
  ] as const)('reads %s as open %s, handed out %s and cancelled %s', (state, open, handedOut, cancelled) => {
    expect([isOpen(state), isHandedOut(state), isCancelled(state)]).toEqual([open, handedOut, cancelled])
  })
})
