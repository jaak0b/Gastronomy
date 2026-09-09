import type { Catalog, CatalogItem, DraftOrder } from './apiTypes'
import { candidateStations, stationStillPreparesIt } from './routingPreview'
import { saveDraft } from './draftCart'

export interface BasketLineView {
  catalogItemId: string
  name: string
  unitPriceCents: number | null
  note: string | null
  stationId: string | null
  candidateStationIds: string[]
  productionMinutes: number | null
  isSoldOut: boolean
  isNoLongerOnTheMenu: boolean
  isNoLongerPreparedAtItsStation: boolean
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
        unitPriceCents: null,
        note: line.note,
        stationId: line.stationId,
        candidateStationIds: [],
        productionMinutes: null,
        isSoldOut: false,
        isNoLongerOnTheMenu: true,
        isNoLongerPreparedAtItsStation: false,
      }
    }
    const candidateStationIds = candidateStations(item)
    return {
      catalogItemId: item.id,
      name: item.name,
      unitPriceCents: item.priceCents,
      note: line.note,
      stationId: line.stationId,
      candidateStationIds,
      productionMinutes: item.productionMinutes,
      isSoldOut: !item.isAvailable,
      isNoLongerOnTheMenu: false,
      isNoLongerPreparedAtItsStation: !stationStillPreparesIt({
        stationId: line.stationId,
        candidateStationIds,
      }),
    }
  })
}

export function lineCannotBeOrdered(line: BasketLineView): boolean {
  return line.isSoldOut || line.isNoLongerOnTheMenu || line.isNoLongerPreparedAtItsStation
}

export function withoutLinesThatCannotBeOrdered(draft: DraftOrder, catalog: Catalog): DraftOrder {
  const shown = buildBasketView(draft, catalog)
  const kept: DraftOrder = {
    ...draft,
    lines: draft.lines.filter((_, position) => !lineCannotBeOrdered(shown[position])),
  }
  saveDraft(kept)
  return kept
}

export function basketItemCount(draft: DraftOrder): number {
  return draft.lines.length
}
