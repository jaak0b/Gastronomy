import { CatalogItemView, StationEstimateView } from '../../shared/api/generatedSchemas'
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
  estimates: readonly StationEstimateView[],
  stationId: string,
): number | null {
  return estimates.find((estimate) => estimate.stationId === stationId)?.queuedMinutes ?? null
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
  queuedMinutes: number | null,
  lines: readonly EstimableLine[],
): number | null {
  if (queuedMinutes === null) {
    return null
  }
  const lanes = queuedMinutesByLane(queuedMinutes, lines)
  const estimate = Math.max(lanes.unflaggedLane, lanes.flaggedLane)
  if (estimate === 0 && lines.every((line) => line.productionMinutes === null)) {
    return null
  }
  return estimate
}

export function stationReadyInMinutes(
  estimates: readonly StationEstimateView[],
  lines: readonly EstimableLine[],
  stationId: string,
): number | null {
  const linesAtTheStation = lines.filter((line) => routedStationId(line) === stationId)
  return estimateFromLanes(queuedMinutesAt(estimates, stationId), linesAtTheStation)
}

export function stationEstimateAfterAdding(
  estimates: readonly StationEstimateView[],
  lines: readonly EstimableLine[],
  stationId: string,
  productionMinutes: number,
  isQueueIndependent: boolean,
  units: number = 1,
): number | null {
  const linesAtTheStation = lines.filter((line) => routedStationId(line) === stationId)
  const added: EstimableLine = {
    stationId,
    candidateStationIds: [stationId],
    productionMinutes: isQueueIndependent ? productionMinutes : productionMinutes * units,
    isQueueIndependent,
  }
  return estimateFromLanes(queuedMinutesAt(estimates, stationId), [...linesAtTheStation, added])
}

export function pickerEstimateRange(
  item: CatalogItemView,
  estimates: readonly StationEstimateView[],
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
  if (!perStation.every((estimate): estimate is number => estimate !== null)) {
    return null
  }
  return { min: Math.min(...perStation), max: Math.max(...perStation) }
}
