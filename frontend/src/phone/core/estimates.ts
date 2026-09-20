import type { CatalogItem, StationEstimate } from '../../shared/api/apiTypes'
import { routedStationId } from './routingPreview'

export interface EstimateRange {
  min: number
  max: number
}

export interface EstimableLine {
  stationId: string | null
  candidateStationIds: readonly string[]
  productionMinutes: number | null
  isQueueIndependent: boolean
}

export function queuedMinutesAt(
  estimates: readonly StationEstimate[],
  stationId: string,
): number {
  return estimates.find((estimate) => estimate.stationId === stationId)?.queuedMinutes ?? 0
}

function queuedMinutesByLane(queuedMinutes: number, lines: readonly EstimableLine[]) {
  const flagged = lines.filter((line) => line.isQueueIndependent)
  const unflagged = lines.filter((line) => !line.isQueueIndependent)
  const sharesTheQueue = unflagged.length > 0 || flagged.length === 0
  return {
    unflaggedLane:
      (sharesTheQueue ? queuedMinutes : 0) +
      unflagged.reduce((total, line) => total + (line.productionMinutes ?? 0), 0),
    flaggedLane: flagged.reduce(
      (longest, line) => Math.max(longest, line.productionMinutes ?? 0),
      0,
    ),
  }
}

function estimateFromLanes(
  queuedMinutes: number,
  lines: readonly EstimableLine[],
): number | null {
  const lanes = queuedMinutesByLane(queuedMinutes, lines)
  const estimate = Math.max(lanes.unflaggedLane, lanes.flaggedLane)
  if (estimate === 0 && lines.every((line) => line.productionMinutes === null)) {
    return null
  }
  return estimate
}

export function stationReadyInMinutes(
  estimates: readonly StationEstimate[],
  lines: readonly EstimableLine[],
  stationId: string,
): number | null {
  const linesAtTheStation = lines.filter((line) => routedStationId(line) === stationId)
  return estimateFromLanes(queuedMinutesAt(estimates, stationId), linesAtTheStation)
}

export function stationEstimateAfterAdding(
  estimates: readonly StationEstimate[],
  lines: readonly EstimableLine[],
  stationId: string,
  productionMinutes: number,
  isQueueIndependent: boolean,
  units: number = 1,
): number {
  const linesAtTheStation = lines.filter((line) => routedStationId(line) === stationId)
  const added: EstimableLine = {
    stationId,
    candidateStationIds: [stationId],
    productionMinutes: isQueueIndependent ? productionMinutes : productionMinutes * units,
    isQueueIndependent,
  }
  return estimateFromLanes(queuedMinutesAt(estimates, stationId), [...linesAtTheStation, added]) ?? 0
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
    stationEstimateAfterAdding(
      estimates,
      lines,
      stationId,
      productionMinutes,
      item.isQueueIndependent,
    ),
  )
  if (perStation.length === 0) {
    return null
  }
  return { min: Math.min(...perStation), max: Math.max(...perStation) }
}
