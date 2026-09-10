import type { CatalogItem, DeliveryMode, StationEstimate } from './apiTypes'
import { assertNever } from './assertNever'

export function readyInMinutes(queuedMinutes: number, productionMinutes: number): number {
  return queuedMinutes + productionMinutes
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
  const productionMinutes = item.productionMinutes
  if (productionMinutes === null) {
    return null
  }
  const perStation = item.stationIds.map((stationId) =>
    readyInMinutes(queuedMinutesAt(estimates, stationId), productionMinutes),
  )
  return perStation.length === 0 ? null : Math.min(...perStation)
}

export function lineEstimateMinutes(
  estimates: readonly StationEstimate[],
  stationId: string | null,
  productionMinutes: number | null,
): number | null {
  if (stationId === null || productionMinutes === null) {
    return null
  }
  return readyInMinutes(queuedMinutesAt(estimates, stationId), productionMinutes)
}
