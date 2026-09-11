import type { CatalogItem, StationEstimate } from './apiTypes'
import { routedStationId } from './routingPreview'

export interface EstimateRange {
  min: number
  max: number
}

export interface EstimableLine {
  stationId: string | null
  candidateStationIds: readonly string[]
  productionMinutes: number | null
}

export function queuedMinutesAt(
  estimates: readonly StationEstimate[],
  stationId: string,
): number {
  return estimates.find((estimate) => estimate.stationId === stationId)?.queuedMinutes ?? 0
}

function minutesAtTheStation(lines: readonly EstimableLine[], stationId: string): number {
  return lines.reduce(
    (total, line) =>
      routedStationId(line) === stationId ? total + (line.productionMinutes ?? 0) : total,
    0,
  )
}

export function stationReadyInMinutes(
  estimates: readonly StationEstimate[],
  lines: readonly EstimableLine[],
  stationId: string,
): number | null {
  const queuedMinutes = queuedMinutesAt(estimates, stationId)
  const linesAtTheStation = lines.filter((line) => routedStationId(line) === stationId)
  if (queuedMinutes === 0 && linesAtTheStation.every((line) => line.productionMinutes === null)) {
    return null
  }
  return queuedMinutes + minutesAtTheStation(linesAtTheStation, stationId)
}

export function stationEstimateAfterAdding(
  estimates: readonly StationEstimate[],
  lines: readonly EstimableLine[],
  stationId: string,
  productionMinutes: number,
  units: number = 1,
): number {
  return (
    queuedMinutesAt(estimates, stationId) +
    minutesAtTheStation(lines, stationId) +
    productionMinutes * units
  )
}

export function pickerEstimateRange(
  item: CatalogItem,
  estimates: readonly StationEstimate[],
  lines: readonly EstimableLine[],
): EstimateRange | null {
  const productionMinutes = item.productionMinutes
  if (productionMinutes === null) {
    return null
  }
  const perStation = item.stationIds.map((stationId) =>
    stationEstimateAfterAdding(estimates, lines, stationId, productionMinutes),
  )
  if (perStation.length === 0) {
    return null
  }
  return { min: Math.min(...perStation), max: Math.max(...perStation) }
}
