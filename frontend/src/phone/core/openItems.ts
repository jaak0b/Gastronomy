import type {
  PaymentMethod,
  SettleLineRequest,
  SettlementView,
  ShareState,
  TableOrderRecordItemView,
  TableOrderRecordView,
  TableOrderReportView,
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

export type PositionState = 'produced' | 'notProduced' | 'cancelled'

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
  if (count === 0) {
    return 'none'
  }
  return count === total ? 'all' : 'some'
}

export function productionStateOf(order: TableOrderRecordView): ShareState {
  const notCancelledItemCount = notCancelledItemCountIn(order)
  return notCancelledItemCount === 0 ? 'all' : shareStateOf(producedCountIn(order), notCancelledItemCount)
}

export function positionStateOf(item: TableOrderRecordItemView): PositionState {
  switch (item.state) {
    case 'open':
      return 'notProduced'
    case 'handedOut':
      return 'produced'
    case 'cancelled':
    case 'cancellationAcknowledged':
      return 'cancelled'
    default:
      return assertNever(item.state)
  }
}

export function canBeSettled(item: TableOrderRecordItemView): boolean {
  return item.settledAtUtc === null && !isCancelled(item.state)
}

export function settleableItemsIn(report: TableOrderReportView): TableOrderRecordItemView[] {
  return report.orders.flatMap((order) => order.items).filter((item) => canBeSettled(item))
}

export function settleableItemIdsIn(report: TableOrderReportView): string[] {
  return settleableItemsIn(report).map((item) => item.orderItemId)
}

export function selectedItems(
  report: TableOrderReportView,
  selectedItemIds: readonly string[],
): TableOrderRecordItemView[] {
  return settleableItemsIn(report).filter((item) => selectedItemIds.includes(item.orderItemId))
}

export function selectedAmountCents(
  report: TableOrderReportView,
  selectedItemIds: readonly string[],
): number {
  return selectedItems(report, selectedItemIds).reduce(
    (total, item) => total + item.unitPriceCents,
    0,
  )
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

export function settleableItemIdsInOrder(order: TableOrderRecordView): string[] {
  return order.items
    .filter((item) => canBeSettled(item))
    .map((item) => item.orderItemId)
}

export function withItemToggled(
  selectedItemIds: readonly string[],
  report: TableOrderReportView,
  orderItemId: string,
): string[] {
  if (!settleableItemIdsIn(report).includes(orderItemId)) {
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
): string[] {
  const tableItemIds = settleableItemIdsIn(report)
  const untouched = selectedItemIds.filter((id) => !tableItemIds.includes(id))
  return isWanted ? [...untouched, ...tableItemIds] : untouched
}

export function withWholeOrder(
  selectedItemIds: readonly string[],
  order: TableOrderRecordView,
  isWanted: boolean,
): string[] {
  const orderItemIds = settleableItemIdsInOrder(order)
  const untouched = selectedItemIds.filter((id) => !orderItemIds.includes(id))
  return isWanted ? [...untouched, ...orderItemIds] : untouched
}

export function withoutItemsThatAreGone(
  selectedItemIds: readonly string[],
  report: TableOrderReportView,
): string[] {
  const stillSettleable = settleableItemIdsIn(report)
  return selectedItemIds.filter((id) => stillSettleable.includes(id))
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
