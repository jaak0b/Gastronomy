import { DeliveryMode, OrderDeliveryModeRequest } from '../../shared/api/generatedSchemas'
import { routedStationId, type RoutableLine } from './routingPreview'

export const DELIVERY_MODE_BEFORE_THE_SERVER_CHOOSES: DeliveryMode = 'together'

export interface StationOrderQueueView<TLine> {
  stationId: string | null
  lines: TLine[]
}

export function buildStationOrders<TLine extends RoutableLine>(
  lines: readonly TLine[],
): StationOrderQueueView<TLine>[] {
  const stationOrders: StationOrderQueueView<TLine>[] = []
  for (const line of lines) {
    const stationId = routedStationId(line)
    const stationOrder = stationOrders.find((candidate) => candidate.stationId === stationId)
    if (stationOrder === undefined) {
      stationOrders.push({ stationId, lines: [line] })
    } else {
      stationOrder.lines.push(line)
    }
  }
  return stationOrders
}

export function deliveryModeChosenOrDefault(
  chosen: Readonly<Record<string, DeliveryMode>>,
  stationId: string,
): DeliveryMode {
  return chosen[stationId] ?? DELIVERY_MODE_BEFORE_THE_SERVER_CHOOSES
}

export function buildStationDeliveryModes<TLine extends RoutableLine>(
  stationOrders: readonly StationOrderQueueView<TLine>[],
  chosen: Readonly<Record<string, DeliveryMode>>,
): OrderDeliveryModeRequest[] {
  const modes: OrderDeliveryModeRequest[] = []
  for (const stationOrder of stationOrders) {
    const stationId = stationOrder.stationId
    if (stationId === null) {
      continue
    }
    modes.push({ stationId, deliveryMode: deliveryModeChosenOrDefault(chosen, stationId) })
  }
  return modes
}
