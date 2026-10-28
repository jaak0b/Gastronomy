import type { ApiErrorBody } from '../api/apiError'
import { DeliveryMode, OrderItemState, StationOrderQueueView, StationQueueItemView } from '../api/generatedSchemas'
import { assertNever } from './assertNever'
import { mergeLinesWithSameArticleAndNote } from './collapse'
import { compareNames } from './compareNames'
import { formatTimeOfDay } from './moments'
import { isCancelled, isHandedOut, isOpen } from './orderItemStates'
import type { Translate } from './translation'

export type StationFailure =
  | { kind: 'unreachable' }
  | { kind: 'error'; status: number; body: ApiErrorBody | null; raw: unknown }
  | { kind: 'unreadableAnswer'; status: number; raw: unknown }

export interface ItemLine {
  key: string
  itemName: string
  note: string | null
  isCancelled: boolean
  units: number
}

export interface StationStats {
  togetherOrders: number
  asItComesOrders: number
  openLines: ItemLine[]
}

export interface StationMessage {
  key: string
  parameters: Record<string, string | number>
}

export function stationFailureMessage(failure: StationFailure): StationMessage {
  switch (failure.kind) {
    case 'unreachable':
      return { key: 'station.board.errors.actionNotReached', parameters: {} }
    case 'error':
      return failure.body === null
        ? { key: 'common.errors.actionFailed', parameters: {} }
        : { key: failure.body.messageKey, parameters: failure.body.parameters }
    case 'unreadableAnswer':
      return { key: 'common.errors.actionFailed', parameters: {} }
    default:
      return assertNever(failure)
  }
}

export function deliveryModeKey(deliveryMode: DeliveryMode): string {
  switch (deliveryMode) {
    case 'together':
      return 'common.delivery.together'
    case 'asItComes':
      return 'common.delivery.asItComes'
    default:
      return assertNever(deliveryMode)
  }
}

export type DeliveryModeColourToken = 'together' | 'individual'

export function deliveryModeColourToken(deliveryMode: DeliveryMode): DeliveryModeColourToken {
  switch (deliveryMode) {
    case 'together':
      return 'together'
    case 'asItComes':
      return 'individual'
    default:
      return assertNever(deliveryMode)
  }
}

export function openItemsIn(stationOrder: StationOrderQueueView): StationQueueItemView[] {
  return stationOrder.items.filter((item) => isOpen(item.state))
}

export function itemsNotHandedOutIn(stationOrder: StationOrderQueueView): StationQueueItemView[] {
  return stationOrder.items.filter((item) => !isHandedOut(item.state))
}

export function itemsAwaitingAcknowledgementIn(stationOrder: StationOrderQueueView): StationQueueItemView[] {
  return stationOrder.items.filter((item) => isCancelled(item.state))
}

export function itemsNotCancelledIn(stationOrder: StationOrderQueueView): StationQueueItemView[] {
  return stationOrder.items.filter((item) => !isCancelled(item.state))
}

function compareLines(left: ItemLine, right: ItemLine): number {
  const byName = compareNames(left.itemName, right.itemName)
  return byName !== 0 ? byName : compareNames(left.note ?? '', right.note ?? '')
}

function linesOf(items: readonly StationQueueItemView[], areCancelled: boolean): ItemLine[] {
  return mergeLinesWithSameArticleAndNote(
    items,
    (item) => item.itemName,
    (item) => item.note,
  ).map(({ key, line, quantity }) => ({
    key: JSON.stringify([key, areCancelled]),
    itemName: line.itemName,
    note: line.note,
    isCancelled: areCancelled,
    units: quantity,
  }))
}

export function itemLines(items: readonly StationQueueItemView[]): ItemLine[] {
  return [
    ...linesOf(items.filter((item) => !isCancelled(item.state)), false),
    ...linesOf(items.filter((item) => isCancelled(item.state)), true),
  ]
}

export function linesByCount(lines: readonly ItemLine[]): ItemLine[] {
  return [...lines].sort(
    (left, right) => right.units - left.units || compareLines(left, right),
  )
}

export function itemLineText(line: ItemLine, t: Translate): string {
  const counted = t('common.labels.countTimesItem', { count: line.units, item: line.itemName })
  if (line.note === null) {
    return counted
  }
  return counted + t('station.board.labels.unitSeparator') + t('common.labels.note', { note: line.note })
}

export function selectedUnits(
  stationOrders: readonly StationOrderQueueView[],
  selectedItemIds: readonly string[],
): ItemLine[] {
  const selected = new Set(selectedItemIds)
  return itemLines(
    stationOrders
      .flatMap((stationOrder) => openItemsIn(stationOrder))
      .filter((item) => selected.has(item.orderItemId)),
  ).sort(compareLines)
}

export function selectedOpenItemIds(
  stationOrder: StationOrderQueueView,
  selectedItemIds: readonly string[],
): string[] {
  const selected = new Set(selectedItemIds)
  return openItemsIn(stationOrder)
    .filter((item) => selected.has(item.orderItemId))
    .map((item) => item.orderItemId)
}

export function selectedItemIdsAwaitingAcknowledgement(
  stationOrder: StationOrderQueueView,
  selectedItemIds: readonly string[],
): string[] {
  const selected = new Set(selectedItemIds)
  return itemsAwaitingAcknowledgementIn(stationOrder)
    .filter((item) => selected.has(item.orderItemId))
    .map((item) => item.orderItemId)
}

export function cancellationText(item: StationQueueItemView, t: Translate, language: string): string | null {
  const time = formatTimeOfDay(new Date(item.stateSetAtUtc), language, false)
  switch (item.state) {
    case 'open':
    case 'handedOut':
      return null
    case 'cancelled':
      return t('station.board.labels.cancelledAt', { time })
    case 'cancellationAcknowledged':
      return t('station.board.labels.cancellationAcknowledgedAt', { time })
    default:
      return assertNever(item.state)
  }
}

export function stationStats(stationOrders: readonly StationOrderQueueView[]): StationStats {
  let togetherOrders = 0
  let asItComesOrders = 0
  for (const stationOrder of stationOrders) {
    switch (stationOrder.deliveryMode) {
      case 'together':
        togetherOrders += 1
        break
      case 'asItComes':
        asItComesOrders += 1
        break
      default:
        assertNever(stationOrder.deliveryMode)
    }
  }
  return {
    togetherOrders,
    asItComesOrders,
    openLines: linesByCount(
      itemLines(stationOrders.flatMap((stationOrder) => openItemsIn(stationOrder))),
    ),
  }
}

function selectableStates(stationOrders: readonly StationOrderQueueView[]): Map<string, OrderItemState> {
  return new Map(
    stationOrders
      .flatMap((stationOrder) => [...openItemsIn(stationOrder), ...itemsAwaitingAcknowledgementIn(stationOrder)])
      .map((item) => [item.orderItemId, item.state]),
  )
}

export function retainStillSelectableItemIds(
  selectedItemIds: readonly string[],
  previousOrders: readonly StationOrderQueueView[],
  nextOrders: readonly StationOrderQueueView[],
): string[] {
  const before = selectableStates(previousOrders)
  const after = selectableStates(nextOrders)
  return selectedItemIds.filter(
    (orderItemId) => after.has(orderItemId) && after.get(orderItemId) === before.get(orderItemId),
  )
}
