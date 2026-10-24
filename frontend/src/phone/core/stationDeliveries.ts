import { DeliveryMode, StationQuoteView } from '../../shared/api/generatedSchemas'
import type { BasketLineView } from './basket'
import { quotedMinutesAt } from './estimates'
import { buildStationOrders, DELIVERY_MODE_BEFORE_THE_SERVER_CHOOSES } from './stationOrders'

export interface StationDelivery {
  stationId: string | null
  stationName: string
  deliveryMode: DeliveryMode
  stationMinutes: number | null
  lines: BasketLineView[]
}

export function stationDeliveries(
  lines: readonly BasketLineView[],
  quotedStations: readonly StationQuoteView[],
  deliveryModeFor: (stationId: string) => DeliveryMode,
): StationDelivery[] {
  return buildStationOrders(lines).map((stationOrder) => {
    const stationId = stationOrder.stationId
    return {
      stationId,
      stationName: stationOrder.lines[0].stationName,
      deliveryMode:
        stationId === null ? DELIVERY_MODE_BEFORE_THE_SERVER_CHOOSES : deliveryModeFor(stationId),
      stationMinutes: stationId === null ? null : quotedMinutesAt(quotedStations, stationId),
      lines: stationOrder.lines,
    }
  })
}
