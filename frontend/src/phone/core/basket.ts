import type { DraftLine, DraftOrder } from './draftCart'
import { CatalogItemView, CatalogStationView, CatalogView } from '../../shared/api/generatedSchemas'
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
  isSoldOut: boolean
  isNoLongerOnTheMenu: boolean
  isNoLongerPreparedAtItsStation: boolean
}

export function findCatalogItem(catalog: CatalogView, catalogItemId: string): CatalogItemView | null {
  return catalog.items.find((item) => item.id === catalogItemId) ?? null
}

export function findCatalogStation(catalog: CatalogView, stationId: string): CatalogStationView | null {
  return catalog.stations.find((station) => station.id === stationId) ?? null
}

function stationNameForLine(
  catalog: CatalogView,
  line: DraftLine,
  candidateStationIds: readonly string[],
): string {
  const stationId = routedStationId({ stationId: line.stationId, candidateStationIds })
  if (stationId === null) {
    return ''
  }
  return findCatalogStation(catalog, stationId)?.name ?? line.stationName
}

export function buildBasketView(draft: DraftOrder, catalog: CatalogView): BasketLineView[] {
  return draft.lines.map((line) => {
    const item = findCatalogItem(catalog, line.catalogItemId)
    if (item === null) {
      return {
        catalogItemId: line.catalogItemId,
        name: line.name,
        unitPriceCents: null,
        note: line.note,
        stationId: line.stationId,
        stationName: stationNameForLine(catalog, line, []),
        candidateStationIds: [],
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
      stationName: stationNameForLine(catalog, line, candidateStationIds),
      candidateStationIds,
      isSoldOut: !item.isAvailable,
      isNoLongerOnTheMenu: false,
      isNoLongerPreparedAtItsStation:
        !stationStillPreparesIt({ stationId: line.stationId, candidateStationIds }) ||
        (line.stationId !== null && findCatalogStation(catalog, line.stationId) === null),
    }
  })
}

export function lineCannotBeOrdered(line: BasketLineView): boolean {
  return line.isSoldOut || line.isNoLongerOnTheMenu || line.isNoLongerPreparedAtItsStation
}

export function withoutLinesThatCannotBeOrdered(draft: DraftOrder, catalog: CatalogView): DraftOrder {
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
