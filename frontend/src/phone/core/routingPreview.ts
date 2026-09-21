import { CatalogItemView } from '../../shared/api/generatedSchemas'

export interface RoutableLine {
  stationId: string | null
  candidateStationIds: readonly string[]
}

export function candidateStations(item: CatalogItemView): string[] {
  return [...item.stationIds]
}

export function needsStationChoice(item: CatalogItemView): boolean {
  return candidateStations(item).length > 1
}

export function stationStillPreparesIt(line: RoutableLine): boolean {
  return line.stationId === null || line.candidateStationIds.includes(line.stationId)
}

export function routedStationId(line: RoutableLine): string | null {
  if (line.stationId !== null) {
    return line.stationId
  }
  return line.candidateStationIds.length === 1 ? line.candidateStationIds[0] : null
}
