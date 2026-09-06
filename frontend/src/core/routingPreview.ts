import type { CatalogItem } from './apiTypes'

export interface RoutableLine {
  stationId: string | null
  candidateStationIds: readonly string[]
}

export function candidateStations(item: CatalogItem): string[] {
  return [...item.stationIds]
}

export function needsStationChoice(item: CatalogItem): boolean {
  return candidateStations(item).length > 1
}

export function routedStationId(line: RoutableLine): string | null {
  if (line.stationId !== null) {
    return line.stationId
  }
  return line.candidateStationIds.length === 1 ? line.candidateStationIds[0] : null
}
