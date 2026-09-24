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

function addUnitsToQuoteLines(
  quoteLines: EstimateQuoteLine[],
  catalogItemId: string,
  stationId: string,
  units: number,
): void {
  const sameArticleAtSameStation = quoteLines.find(
    (quoteLine) => quoteLine.catalogItemId === catalogItemId && quoteLine.stationId === stationId,
  )
  if (sameArticleAtSameStation === undefined) {
    quoteLines.push({ catalogItemId, stationId, units })
  } else {
    sameArticleAtSameStation.units += units
  }
}

export function quoteLinesFor(lines: readonly BasketLineView[]): EstimateQuoteLine[] {
  const quoteLines: EstimateQuoteLine[] = []
  for (const line of lines.filter((candidate) => !lineCannotBeOrdered(candidate))) {
    const stationId = routedStationId(line)
    if (stationId !== null) {
      addUnitsToQuoteLines(quoteLines, line.catalogItemId, stationId, 1)
    }
  }
  return quoteLines
}

export interface UnitsAwaitingStation {
  catalogItemId: string
  units: number
}

export function quoteLinesForStationChoice(
  lines: readonly BasketLineView[],
  linesAwaitingStation: readonly number[],
  unitsAwaitingStation: UnitsAwaitingStation,
  candidateStationId: string,
): EstimateQuoteLine[] {
  const quoteLines = quoteLinesFor(
    lines.filter((_, index) => !linesAwaitingStation.includes(index)),
  )
  addUnitsToQuoteLines(
    quoteLines,
    unitsAwaitingStation.catalogItemId,
    candidateStationId,
    unitsAwaitingStation.units,
  )
  return quoteLines
}
