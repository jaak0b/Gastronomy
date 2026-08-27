import type { CatalogItem } from './apiTypes'

export function candidateStations(item: CatalogItem): string[] {
  return [...item.stationIds]
}

export function needsStationChoice(item: CatalogItem): boolean {
  return candidateStations(item).length > 1
}
