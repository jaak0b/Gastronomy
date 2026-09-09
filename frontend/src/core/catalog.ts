import type { Catalog, CatalogStation } from './apiTypes'

export function findCatalogStation(catalog: Catalog, stationId: string): CatalogStation | null {
  return catalog.stations.find((station) => station.id === stationId) ?? null
}
