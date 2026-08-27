import type { Catalog, CatalogItem, DraftOrder } from './apiTypes'
import { candidateStations } from './routingPreview'
import { saveDraft } from './draftCart'

export interface BasketLineView {
  catalogItemId: string
  name: string
  unitPriceCents: number
  quantity: number
  note: string | null
  stationId: string | null
  candidateStationIds: string[]
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
        quantity: line.quantity,
        note: line.note,
        stationId: line.stationId,
        candidateStationIds: [],
        isSoldOut: false,
        isNoLongerOnTheMenu: true,
      }
    }
    return {
      catalogItemId: item.id,
      name: item.name,
      unitPriceCents: item.priceCents,
      quantity: line.quantity,
      note: line.note,
      stationId: line.stationId,
      candidateStationIds: candidateStations(item),
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

export function basketItemCount(draft: DraftOrder): number {
  return draft.lines.reduce((count, line) => count + line.quantity, 0)
}
