import type { OrderSummary } from './apiTypes'
import { assertNever } from './assertNever'

export type OrderPresentationState = 'Printing' | 'Printed' | 'HandledOnPaper' | 'NeedsAttention'

export function presentOrderState(order: OrderSummary): OrderPresentationState {
  switch (order.status) {
    case 'Accepted':
    case 'Printing':
      return 'Printing'
    case 'Printed':
      return order.stationOrders.some((stationOrder) => stationOrder.status === 'HandledOnPaper')
        ? 'HandledOnPaper'
        : 'Printed'
    case 'NeedsAttention':
      return 'NeedsAttention'
    default:
      return assertNever(order.status)
  }
}
