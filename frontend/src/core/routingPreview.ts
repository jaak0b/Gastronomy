import type { CatalogItem } from './apiTypes'

export function candidateLocations(item: CatalogItem): string[] {
  return [...item.locationIds]
}

export function needsStationChoice(item: CatalogItem): boolean {
  return candidateLocations(item).length > 1
}
