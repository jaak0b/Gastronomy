import type { DeliveryMode, StationDeliveryMode } from './apiTypes'
import { routedStationId, type RoutableLine } from './routingPreview'

export const DELIVERY_MODE_BEFORE_THE_SERVER_CHOOSES: DeliveryMode = 'together'

export interface OrderSlice<TLine> {
  stationId: string | null
  lines: TLine[]
}

export function orderSlices<TLine extends RoutableLine>(
  lines: readonly TLine[],
): OrderSlice<TLine>[] {
  const slices: OrderSlice<TLine>[] = []
  for (const line of lines) {
    const stationId = routedStationId(line)
    const slice = slices.find((candidate) => candidate.stationId === stationId)
    if (slice === undefined) {
      slices.push({ stationId, lines: [line] })
    } else {
      slice.lines.push(line)
    }
  }
  return slices
}

export function chosenDeliveryMode(
  chosen: Readonly<Record<string, DeliveryMode>>,
  stationId: string,
): DeliveryMode {
  return chosen[stationId] ?? DELIVERY_MODE_BEFORE_THE_SERVER_CHOOSES
}

export function deliveryModesOf<TLine extends RoutableLine>(
  slices: readonly OrderSlice<TLine>[],
  chosen: Readonly<Record<string, DeliveryMode>>,
): StationDeliveryMode[] {
  const modes: StationDeliveryMode[] = []
  for (const slice of slices) {
    const stationId = slice.stationId
    if (stationId === null) {
      continue
    }
    modes.push({ stationId, deliveryMode: chosenDeliveryMode(chosen, stationId) })
  }
  return modes
}
