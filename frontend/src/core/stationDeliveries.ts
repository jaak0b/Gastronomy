import type { DeliveryMode, StationEstimate } from './apiTypes'
import { assertNever } from './assertNever'
import { lineCannotBeOrdered, type BasketLineView } from './basket'
import { stationReadyInMinutes } from './estimates'
import { DELIVERY_MODE_BEFORE_THE_SERVER_CHOOSES, orderSlices } from './orderSlices'

export interface StationDelivery {
  stationId: string | null
  stationName: string
  deliveryMode: DeliveryMode
  minutes: number | null
  stationMinutes: number | null
  lines: BasketLineView[]
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
  estimates: readonly StationEstimate[],
  deliveryModeFor: (stationId: string) => DeliveryMode,
): StationDelivery[] {
  return orderSlices(lines).map((slice) => {
    const stationId = slice.stationId
    const deliveryMode =
      stationId === null ? DELIVERY_MODE_BEFORE_THE_SERVER_CHOOSES : deliveryModeFor(stationId)
    const orderableLines = slice.lines.filter((line) => !lineCannotBeOrdered(line))
    const stationMinutes =
      stationId === null ? null : stationReadyInMinutes(estimates, orderableLines, stationId)
    return {
      stationId,
      stationName: slice.lines[0].stationName,
      deliveryMode,
      minutes: minutesForTheChosenMode(deliveryMode, stationMinutes),
      stationMinutes,
      lines: slice.lines,
    }
  })
}
