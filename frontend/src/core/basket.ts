import type { Catalog, CatalogItem, DraftLine, DraftOrder } from './apiTypes'
import { findCatalogStation } from './catalog'
import { candidateStations, routedStationId, stationStillPreparesIt } from './routingPreview'
import { saveDraft } from './draftCart'

export interface BasketLineView {
  catalogItemId: string
  name: string
  unitPriceCents: number | null
  note: string | null
  stationId: string | null
  stationName: string
  candidateStationIds: string[]
  productionMinutes: number | null
  isSoldOut: boolean
  isNoLongerOnTheMenu: boolean
  isNoLongerPreparedAtItsStation: boolean
}

export function findCatalogItem(catalog: Catalog, catalogItemId: string): CatalogItem | null {
  return catalog.items.find((item) => item.id === catalogItemId) ?? null
}

function nameOfTheStationTheLineGoesTo(
  catalog: Catalog,
  line: DraftLine,
  candidateStationIds: readonly string[],
): string {
  const stationId = routedStationId({ stationId: line.stationId, candidateStationIds })
  if (stationId === null) {
    return ''
  }
  return findCatalogStation(catalog, stationId)?.name ?? line.stationName
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
        stationName: nameOfTheStationTheLineGoesTo(catalog, line, []),
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
      stationName: nameOfTheStationTheLineGoesTo(catalog, line, candidateStationIds),
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

function stationNameToRemember(line: DraftLine, shown: BasketLineView): string {
  return line.stationName.length > 0 ? line.stationName : shown.stationName
}

export function withStationNamesTheCatalogStillKnows(
  draft: DraftOrder,
  catalog: Catalog,
): DraftOrder {
  const shown = buildBasketView(draft, catalog)
  const lines = draft.lines.map((line, position) => {
    const stationName = stationNameToRemember(line, shown[position])
    return stationName === line.stationName ? line : { ...line, stationName }
  })
  if (lines.every((line, position) => line === draft.lines[position])) {
    return draft
  }
  const remembered: DraftOrder = { ...draft, lines }
  saveDraft(remembered)
  return remembered
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
