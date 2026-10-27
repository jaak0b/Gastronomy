import type {
  PaymentMethod,
  SettleLineRequest,
  SettlementView,
  ShareState,
  TableOrderRecordItemView,
  TableOrderRecordView,
  TableOrderReportView,
  TableOrderSearchRequest,
} from '../../shared/api/generatedSchemas'
import { assertNever } from '../../shared/core/assertNever'
import type { AppLanguage } from '../../shared/core/deviceLanguage'
import { formatPrice } from '../../shared/core/money'
import { isCancelled, isHandedOut } from '../../shared/core/orderItemStates'

export type SettleOutcome = 'accepted' | 'refused' | 'answerNeverCame'

export type SettlementPaymentMethod = NonNullable<PaymentMethod>

export interface AmountPaidWithReason {
  amountPaidCents: number
  paymentNotice: string | null
}

export type SelectionPurpose = 'settling' | 'cancelling'

export interface SettleNotice {
  key: string
  parameters: Record<string, string | number>
  count: number | null
}

export function producedCountIn(order: TableOrderRecordView): number {
  return order.items.filter((item) => isHandedOut(item.state)).length
}

export function notCancelledItemCountIn(order: TableOrderRecordView): number {
  return order.items.filter((item) => !isCancelled(item.state)).length
}

export function shareStateOf(count: number, total: number): ShareState {
  if (count === total) {
    return 'all'
  }
  return count === 0 ? 'none' : 'some'
}

export function searchOptionsFor(purpose: SelectionPurpose, showsEveryItem: boolean): TableOrderSearchRequest {
  if (showsEveryItem) {
    return {}
  }
  switch (purpose) {
    case 'settling':
      return { paymentStatus: 'notPaid', includeCancelled: false }
    case 'cancelling':
      return { includeCancelled: false }
    default:
      return assertNever(purpose)
  }
}

export function isShownAsPaid(item: TableOrderRecordItemView, purpose: SelectionPurpose): boolean {
  switch (purpose) {
    case 'settling':
      return item.settledAtUtc !== null
    case 'cancelling':
      return false
    default:
      return assertNever(purpose)
  }
}

export function canBeSelected(item: TableOrderRecordItemView, purpose: SelectionPurpose): boolean {
  switch (purpose) {
    case 'settling':
      return item.settledAtUtc === null && !isCancelled(item.state)
    case 'cancelling':
      return !isCancelled(item.state)
    default:
      return assertNever(purpose)
  }
}

export function selectableItemsIn(
  report: TableOrderReportView,
  purpose: SelectionPurpose,
): TableOrderRecordItemView[] {
  return report.orders.flatMap((order) => order.items).filter((item) => canBeSelected(item, purpose))
}

export function selectableItemIdsIn(report: TableOrderReportView, purpose: SelectionPurpose): string[] {
  return selectableItemsIn(report, purpose).map((item) => item.orderItemId)
}

export function selectedItems(
  report: TableOrderReportView,
  selectedItemIds: readonly string[],
  purpose: SelectionPurpose,
): TableOrderRecordItemView[] {
  return selectableItemsIn(report, purpose).filter((item) => selectedItemIds.includes(item.orderItemId))
}

export function selectedAmountCents(
  report: TableOrderReportView,
  selectedItemIds: readonly string[],
): number {
  return selectedItems(report, selectedItemIds, 'settling').reduce(
    (total, item) => total + item.unitPriceCents,
    0,
  )
}

export function cashToReturnCents(cancelledItems: readonly TableOrderRecordItemView[]): number {
  return cancelledItems.reduce((total, item) => total + (item.chargedPriceCents ?? 0), 0)
}

export function selectionStateOf(
  itemIds: readonly string[],
  selectedItemIds: readonly string[],
): ShareState {
  return shareStateOf(
    itemIds.filter((id) => selectedItemIds.includes(id)).length,
    itemIds.length,
  )
}

export function selectableItemIdsInOrder(order: TableOrderRecordView, purpose: SelectionPurpose): string[] {
  return order.items
    .filter((item) => canBeSelected(item, purpose))
    .map((item) => item.orderItemId)
}

export function withItemToggled(
  selectedItemIds: readonly string[],
  report: TableOrderReportView,
  orderItemId: string,
  purpose: SelectionPurpose,
): string[] {
  if (!selectableItemIdsIn(report, purpose).includes(orderItemId)) {
    return [...selectedItemIds]
  }
  return selectedItemIds.includes(orderItemId)
    ? selectedItemIds.filter((id) => id !== orderItemId)
    : [...selectedItemIds, orderItemId]
}

export function withWholeTable(
  selectedItemIds: readonly string[],
  report: TableOrderReportView,
  isWanted: boolean,
  purpose: SelectionPurpose,
): string[] {
  const tableItemIds = selectableItemIdsIn(report, purpose)
  const untouched = selectedItemIds.filter((id) => !tableItemIds.includes(id))
  return isWanted ? [...untouched, ...tableItemIds] : untouched
}

export function withWholeOrder(
  selectedItemIds: readonly string[],
  order: TableOrderRecordView,
  isWanted: boolean,
  purpose: SelectionPurpose,
): string[] {
  const orderItemIds = selectableItemIdsInOrder(order, purpose)
  const untouched = selectedItemIds.filter((id) => !orderItemIds.includes(id))
  return isWanted ? [...untouched, ...orderItemIds] : untouched
}

export function withoutItemsThatAreGone(
  selectedItemIds: readonly string[],
  report: TableOrderReportView,
  purpose: SelectionPurpose,
): string[] {
  const stillSelectable = selectableItemIdsIn(report, purpose)
  return selectedItemIds.filter((id) => stillSelectable.includes(id))
}

export function isPaymentNoticeWritten(reason: string): boolean {
  return reason.trim().length > 0
}

export function isPaymentNoticeNeeded(
  amountPaidCents: number,
  selectedTotalCents: number,
): boolean {
  return amountPaidCents < selectedTotalCents
}

export function isAPaymentMethodNeeded(amountPaidCents: number): boolean {
  return amountPaidCents > 0
}

export function canTheAmountBeSettled(
  amountPaidCents: number | null,
  reason: string,
  selectedTotalCents: number,
): boolean {
  if (amountPaidCents === null) {
    return false
  }
  return (
    !isPaymentNoticeNeeded(amountPaidCents, selectedTotalCents) || isPaymentNoticeWritten(reason)
  )
}

export type SentSettleLine = SettleLineRequest & { paidPriceCents: number }

export function noticeAfterSettling(
  settlement: SettlementView,
  sentLines: readonly SentSettleLine[],
  language: AppLanguage,
): SettleNotice | null {
  const takenBySomebodyElse = settlement.alreadySettledByOthersOrderItemIds
  if (takenBySomebodyElse.length > 0) {
    const toHandBack = sentLines
      .filter((line) => takenBySomebodyElse.includes(line.orderItemId))
      .reduce((total, line) => total + line.paidPriceCents, 0)
    return {
      key: 'phone.openItems.messages.someWereAlreadySettled',
      parameters: {
        count: takenBySomebodyElse.length,
        amount: formatPrice(toHandBack, language),
      },
      count: takenBySomebodyElse.length,
    }
  }
  return null
}
