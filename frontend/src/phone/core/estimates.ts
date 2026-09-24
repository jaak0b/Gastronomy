import type {
  EstimateQuoteLine,
  ItemEstimateView,
  StationQuoteView,
} from '../../shared/api/generatedSchemas'
import { lineCannotBeOrdered, type BasketLineView } from './basket'
import { routedStationId } from './routingPreview'

export interface EstimateRange {
  min: number
  max: number
}

export function readyInMinutesAt(
  estimates: readonly ItemEstimateView[],
  catalogItemId: string,
  stationId: string,
): number | null {
  return (
    estimates.find(
      (estimate) => estimate.catalogItemId === catalogItemId && estimate.stationId === stationId,
    )?.readyInMinutes ?? null
  )
}

export function estimateRangeForItem(
  estimates: readonly ItemEstimateView[],
  catalogItemId: string,
): EstimateRange | null {
  const minutes = estimates
    .filter((estimate) => estimate.catalogItemId === catalogItemId)
    .map((estimate) => estimate.readyInMinutes)
  if (minutes.length === 0) {
    return null
  }
  return { min: Math.min(...minutes), max: Math.max(...minutes) }
}

export function quotedMinutesAt(
  quotedStations: readonly StationQuoteView[],
  stationId: string,
): number | null {
  return quotedStations.find((station) => station.stationId === stationId)?.readyInMinutes ?? null
}

export function quoteLinesFor(lines: readonly BasketLineView[]): EstimateQuoteLine[] {
  const quoteLines: EstimateQuoteLine[] = []
  for (const line of lines.filter((candidate) => !lineCannotBeOrdered(candidate))) {
    const stationId = routedStationId(line)
    if (stationId === null) {
      continue
    }
    const sameArticleAtSameStation = quoteLines.find(
      (quoteLine) =>
        quoteLine.catalogItemId === line.catalogItemId && quoteLine.stationId === stationId,
    )
    if (sameArticleAtSameStation === undefined) {
      quoteLines.push({ catalogItemId: line.catalogItemId, stationId, units: 1 })
    } else {
      sameArticleAtSameStation.units += 1
    }
  }
  return quoteLines
}
