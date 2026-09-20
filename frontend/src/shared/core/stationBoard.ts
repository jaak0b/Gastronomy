import type { ApiErrorBody } from '../api/apiError'
import type { DeliveryMode, StationOrder, StationOrderItem } from '../api/apiTypes'
import { assertNever } from './assertNever'
import { mergeLinesWithSameArticleAndNote } from './collapse'
import type { Translate } from './translation'

export type StationFailure =
  | { kind: 'unreachable' }
  | { kind: 'error'; status: number; body: ApiErrorBody | null; raw: unknown }
  | { kind: 'unreadableAnswer'; status: number; raw: unknown }

export interface ItemLine {
  key: string
  itemName: string
  note: string | null
  units: number
}

export interface StationStats {
  togetherOrders: number
  asItComesOrders: number
  openLines: ItemLine[]
}

export function stationFailureKey(failure: StationFailure): string {
  switch (failure.kind) {
    case 'unreachable':
      return 'station.actionNotReached'
    case 'error':
      return failure.body === null ? 'station.actionFailed' : failure.body.messageKey
    case 'unreadableAnswer':
      return 'station.actionFailed'
    default:
      return assertNever(failure)
  }
}

export function deliveryModeKey(deliveryMode: DeliveryMode): string {
  switch (deliveryMode) {
    case 'together':
      return 'delivery.together'
    case 'asItComes':
      return 'delivery.asItComes'
    default:
      return assertNever(deliveryMode)
  }
}

export type ColourToken = 'together' | 'individual'

export function deliveryModeColourToken(deliveryMode: DeliveryMode): ColourToken {
  switch (deliveryMode) {
    case 'together':
      return 'together'
    case 'asItComes':
      return 'individual'
    default:
      return assertNever(deliveryMode)
  }
}

export function isFulfilled(item: StationOrderItem): boolean {
  return item.fulfilledAtUtc !== null
}

export function openItemsIn(stationOrder: StationOrder): StationOrderItem[] {
  return stationOrder.items.filter((item) => !isFulfilled(item))
}

function compareItemNames(left: string, right: string): number {
  if (left < right) {
    return -1
  }
  return left > right ? 1 : 0
}

function compareLines(left: ItemLine, right: ItemLine): number {
  const byName = compareItemNames(left.itemName, right.itemName)
  return byName !== 0 ? byName : compareItemNames(left.note ?? '', right.note ?? '')
}

export function itemLines(items: readonly StationOrderItem[]): ItemLine[] {
  return mergeLinesWithSameArticleAndNote(
    items,
    (item) => item.itemName,
    (item) => item.note,
  ).map(({ key, line, quantity }) => ({
    key,
    itemName: line.itemName,
    note: line.note,
    units: quantity,
  }))
}

export function linesByCount(lines: readonly ItemLine[]): ItemLine[] {
  return [...lines].sort(
    (left, right) => right.units - left.units || compareLines(left, right),
  )
}

export function itemLineText(line: ItemLine, t: Translate): string {
  const counted = t('station.itemUnits', { count: line.units, item: line.itemName })
  if (line.note === null) {
    return counted
  }
  return counted + t('station.unitSeparator') + t('station.note', { note: line.note })
}

export function selectedUnits(
  stationOrders: readonly StationOrder[],
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
  stationOrder: StationOrder,
  selectedItemIds: readonly string[],
): string[] {
  const selected = new Set(selectedItemIds)
  return openItemsIn(stationOrder)
    .filter((item) => selected.has(item.orderItemId))
    .map((item) => item.orderItemId)
}

export function stationStats(stationOrders: readonly StationOrder[]): StationStats {
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

export function retainOpenItemIds(
  selectedItemIds: readonly string[],
  stationOrders: readonly StationOrder[],
): string[] {
  const open = new Set(
    stationOrders.flatMap((stationOrder) => openItemsIn(stationOrder)).map((item) => item.orderItemId),
  )
  return selectedItemIds.filter((orderItemId) => open.has(orderItemId))
}
