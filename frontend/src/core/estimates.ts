import type { CatalogItem, DeliveryMode, StationEstimate } from './apiTypes'
import { assertNever } from './assertNever'

export function readyInMinutes(
  queuedMinutes: number,
  productionMinutes: number | null,
): number {
  return queuedMinutes + (productionMinutes ?? 0)
}

export function queuedMinutesAt(
  estimates: readonly StationEstimate[],
  stationId: string,
): number {
  return estimates.find((estimate) => estimate.stationId === stationId)?.queuedMinutes ?? 0
}

export function sliceEstimateMinutes(
  itemMinutes: readonly number[],
  deliveryMode: DeliveryMode,
): number | null {
  switch (deliveryMode) {
    case 'together':
      return itemMinutes.length === 0 ? null : Math.max(...itemMinutes)
    case 'asItComes':
      return null
    default:
      return assertNever(deliveryMode)
  }
}

export function pickerEstimateMinutes(
  item: CatalogItem,
  estimates: readonly StationEstimate[],
): number | null {
  const perStation = item.stationIds.map((stationId) =>
    readyInMinutes(queuedMinutesAt(estimates, stationId), item.productionMinutes),
  )
  return perStation.length === 0 ? null : Math.min(...perStation)
}
