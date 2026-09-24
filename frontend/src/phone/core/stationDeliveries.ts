import { DeliveryMode, StationQuoteView } from '../../shared/api/generatedSchemas'
import { assertNever } from '../../shared/core/assertNever'
import type { BasketLineView } from './basket'
import { quotedMinutesAt } from './estimates'
import { buildStationOrders, DELIVERY_MODE_BEFORE_THE_SERVER_CHOOSES } from './stationOrders'

export interface StationDelivery {
  stationId: string | null
  stationName: string
  deliveryMode: DeliveryMode
  minutes: number | null
  stationMinutes: number | null
  lines: BasketLineView[]
}

export interface StationDeliveryWithStation extends StationDelivery {
  stationId: string
}

export function deliveriesWithAStation(
  deliveries: readonly StationDelivery[],
): StationDeliveryWithStation[] {
  return deliveries.filter(
    (delivery): delivery is StationDeliveryWithStation => delivery.stationId !== null,
  )
}

function minutesForTheChosenMode(
  deliveryMode: DeliveryMode,
  stationMinutes: number | null,
): number | null {
  switch (deliveryMode) {
    case 'together':
      return stationMinutes
    case 'asItComes':
      return null
    default:
      return assertNever(deliveryMode)
  }
}

export function stationDeliveries(
  lines: readonly BasketLineView[],
  quotedStations: readonly StationQuoteView[],
  deliveryModeFor: (stationId: string) => DeliveryMode,
): StationDelivery[] {
  return buildStationOrders(lines).map((stationOrder) => {
    const stationId = stationOrder.stationId
    const deliveryMode =
      stationId === null ? DELIVERY_MODE_BEFORE_THE_SERVER_CHOOSES : deliveryModeFor(stationId)
    const stationMinutes = stationId === null ? null : quotedMinutesAt(quotedStations, stationId)
    return {
      stationId,
      stationName: stationOrder.lines[0].stationName,
      deliveryMode,
      minutes: minutesForTheChosenMode(deliveryMode, stationMinutes),
      stationMinutes,
      lines: stationOrder.lines,
    }
  })
}
