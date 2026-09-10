import type { DeliveryMode, StationEstimate } from './apiTypes'
import { lineCannotBeOrdered, type BasketLineView } from './basket'
import { lineEstimateMinutes, sliceEstimateMinutes } from './estimates'
import { DELIVERY_MODE_BEFORE_THE_SERVER_CHOOSES, orderSlices } from './orderSlices'

export interface StationDelivery {
  stationId: string | null
  stationName: string
  deliveryMode: DeliveryMode
  minutes: number | null
}

export function stationDeliveries(
  lines: readonly BasketLineView[],
  estimates: readonly StationEstimate[],
  deliveryModeFor: (stationId: string) => DeliveryMode,
): StationDelivery[] {
  return orderSlices(lines).map((slice) => {
    const stationId = slice.stationId
    const deliveryMode =
      stationId === null ? DELIVERY_MODE_BEFORE_THE_SERVER_CHOOSES : deliveryModeFor(stationId)
    const itemMinutes = slice.lines
      .filter((line) => !lineCannotBeOrdered(line))
      .map((line) => lineEstimateMinutes(estimates, stationId, line.productionMinutes))
      .filter((minutes): minutes is number => minutes !== null)
    return {
      stationId,
      stationName: slice.lines[0].stationName,
      deliveryMode,
      minutes: sliceEstimateMinutes(itemMinutes, deliveryMode),
    }
  })
}
