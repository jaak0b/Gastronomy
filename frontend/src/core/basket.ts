import type { Catalog, CatalogItem, DraftOrder } from './apiTypes'
import { candidateStations } from './routingPreview'
import { saveDraft } from './draftCart'

export interface BasketLineView {
  catalogItemId: string
  name: string
  unitPriceCents: number
  note: string | null
  stationId: string | null
  candidateStationIds: string[]
  productionMinutes: number | null
  isSoldOut: boolean
  isNoLongerOnTheMenu: boolean
}

export function findCatalogItem(catalog: Catalog, catalogItemId: string): CatalogItem | null {
  return catalog.items.find((item) => item.id === catalogItemId) ?? null
}

export function buildBasketView(draft: DraftOrder, catalog: Catalog): BasketLineView[] {
  return draft.lines.map((line) => {
    const item = findCatalogItem(catalog, line.catalogItemId)
    if (item === null) {
      return {
        catalogItemId: line.catalogItemId,
        name: line.name,
        unitPriceCents: line.unitPriceCents,
        note: line.note,
        stationId: line.stationId,
        candidateStationIds: [],
        productionMinutes: null,
        isSoldOut: false,
        isNoLongerOnTheMenu: true,
      }
    }
    return {
      catalogItemId: item.id,
      name: item.name,
      unitPriceCents: item.priceCents,
      note: line.note,
      stationId: line.stationId,
      candidateStationIds: candidateStations(item),
      productionMinutes: item.productionMinutes,
      isSoldOut: !item.isAvailable,
      isNoLongerOnTheMenu: false,
    }
  })
}

export function refreshLineSnapshots(draft: DraftOrder, catalog: Catalog): DraftOrder {
  const refreshed: DraftOrder = {
    ...draft,
    lines: draft.lines.map((line) => {
      const item = findCatalogItem(catalog, line.catalogItemId)
      if (item === null) {
        return line
      }
      return { ...line, name: item.name, unitPriceCents: item.priceCents }
    }),
  }
  saveDraft(refreshed)
  return refreshed
}

export function withoutLinesNoLongerOnTheMenu(draft: DraftOrder, catalog: Catalog): DraftOrder {
  const kept: DraftOrder = {
    ...draft,
    lines: draft.lines.filter((line) => findCatalogItem(catalog, line.catalogItemId) !== null),
  }
  saveDraft(kept)
  return kept
}

export function basketItemCount(draft: DraftOrder): number {
  return draft.lines.length
}
