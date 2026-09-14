import type { ApiErrorBody } from './apiError'
import type { DeliveryMode, StationSlice, StationSliceItem } from './apiTypes'
import { assertNever } from './assertNever'

export type StationFailure =
  | { kind: 'unreachable' }
  | { kind: 'error'; status: number; body: ApiErrorBody | null; raw: unknown }

export interface ItemUnits {
  itemName: string
  units: number
}

export interface StationStats {
  togetherOrders: number
  asItComesOrders: number
  openUnits: ItemUnits[]
}

export function stationFailureKey(failure: StationFailure): string {
  switch (failure.kind) {
    case 'unreachable':
      return 'station.actionNotReached'
    case 'error':
      return failure.body === null ? 'station.actionFailed' : failure.body.messageKey
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

export function deliveryModeColour(deliveryMode: DeliveryMode): 'together' | 'individual' {
  switch (deliveryMode) {
    case 'together':
      return 'together'
    case 'asItComes':
      return 'individual'
    default:
      return assertNever(deliveryMode)
  }
}

export function isFulfilled(item: StationSliceItem): boolean {
  return item.fulfilledAtUtc !== null
}

export function openItemsOf(slice: StationSlice): StationSliceItem[] {
  return slice.items.filter((item) => !isFulfilled(item))
}

function compareItemNames(left: string, right: string): number {
  if (left < right) {
    return -1
  }
  return left > right ? 1 : 0
}

export function itemUnits(items: readonly StationSliceItem[]): ItemUnits[] {
  const unitsByName = new Map<string, number>()
  for (const item of items) {
    unitsByName.set(item.itemName, (unitsByName.get(item.itemName) ?? 0) + 1)
  }
  return [...unitsByName.entries()]
    .map(([itemName, units]) => ({ itemName, units }))
    .sort((left, right) => compareItemNames(left.itemName, right.itemName))
}

export function selectedUnits(
  slices: readonly StationSlice[],
  selectedItemIds: readonly string[],
): ItemUnits[] {
  const selected = new Set(selectedItemIds)
  return itemUnits(
    slices.flatMap((slice) => openItemsOf(slice)).filter((item) => selected.has(item.orderItemId)),
  )
}

export function selectedOpenItemIds(
  slice: StationSlice,
  selectedItemIds: readonly string[],
): string[] {
  const selected = new Set(selectedItemIds)
  return openItemsOf(slice)
    .filter((item) => selected.has(item.orderItemId))
    .map((item) => item.orderItemId)
}

export function stationStats(orders: readonly StationSlice[]): StationStats {
  let togetherOrders = 0
  let asItComesOrders = 0
  for (const slice of orders) {
    switch (slice.deliveryMode) {
      case 'together':
        togetherOrders += 1
        break
      case 'asItComes':
        asItComesOrders += 1
        break
      default:
        assertNever(slice.deliveryMode)
    }
  }
  return {
    togetherOrders,
    asItComesOrders,
    openUnits: itemUnits(orders.flatMap((slice) => openItemsOf(slice))),
  }
}

export function retainOpenItemIds(
  selectedItemIds: readonly string[],
  orders: readonly StationSlice[],
): string[] {
  const open = new Set(
    orders.flatMap((slice) => openItemsOf(slice)).map((item) => item.orderItemId),
  )
  return selectedItemIds.filter((orderItemId) => open.has(orderItemId))
}
