import type { OrderItemState } from '../api/generatedSchemas'
import { assertNever } from './assertNever'

export function isOpen(state: OrderItemState): boolean {
  switch (state) {
    case 'open':
      return true
    case 'handedOut':
    case 'cancelled':
    case 'cancellationAcknowledged':
      return false
    default:
      return assertNever(state)
  }
}

export function isHandedOut(state: OrderItemState): boolean {
  switch (state) {
    case 'handedOut':
      return true
    case 'open':
    case 'cancelled':
    case 'cancellationAcknowledged':
      return false
    default:
      return assertNever(state)
  }
}

export function isCancelled(state: OrderItemState): boolean {
  switch (state) {
    case 'cancelled':
    case 'cancellationAcknowledged':
      return true
    case 'open':
    case 'handedOut':
      return false
    default:
      return assertNever(state)
  }
}
