import type {
  OrderItemHistoryView,
  OrderItemStatusChangeView,
  PaymentMethod,
} from '../../shared/api/generatedSchemas'
import { assertNever } from '../../shared/core/assertNever'

export type ItemStatusEventKind = 'ordered' | 'handedOut' | 'putBack' | 'cancelled' | 'cancellationAcknowledged'

export type ItemHistoryEntry =
  | { kind: ItemStatusEventKind; atUtc: string; by: string | null }
  | {
      kind: 'paid'
      atUtc: string
      by: string | null
      amountCents: number | null
      paymentMethod: PaymentMethod
      paymentNotice: string | null
    }

function statusEventKindOf(change: OrderItemStatusChangeView, isTheFirstChange: boolean): ItemStatusEventKind {
  switch (change.state) {
    case 'open':
      return isTheFirstChange ? 'ordered' : 'putBack'
    case 'handedOut':
      return 'handedOut'
    case 'cancelled':
      return 'cancelled'
    case 'cancellationAcknowledged':
      return 'cancellationAcknowledged'
    default:
      return assertNever(change.state)
  }
}

function paymentEntriesOf(history: OrderItemHistoryView): ItemHistoryEntry[] {
  if (history.settledAtUtc === null) {
    return []
  }
  return [
    {
      kind: 'paid',
      atUtc: history.settledAtUtc,
      by: history.settledByStaffMemberName,
      amountCents: history.chargedPriceCents,
      paymentMethod: history.paymentMethod,
      paymentNotice: history.paymentNotice,
    },
  ]
}

export function historyEntriesOf(history: OrderItemHistoryView): ItemHistoryEntry[] {
  const statusEntries: ItemHistoryEntry[] = history.statusChanges.map((change, index) => ({
    kind: statusEventKindOf(change, index === 0),
    atUtc: change.changedAtUtc,
    by: change.staffMemberName ?? change.stationName,
  }))
  return [...statusEntries, ...paymentEntriesOf(history)].sort(
    (first, second) => Date.parse(first.atUtc) - Date.parse(second.atUtc),
  )
}
