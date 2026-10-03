import type { DraftOrder } from '../../../src/phone/core/draftCart'
import type { CatalogView } from '../../../src/shared/api/generatedSchemas'

export function catalog(): CatalogView {
  return {
    festival: null,
    categories: [
      { categoryId: 'category-essen', name: 'Essen', colourHex: '#FFEB3B', sortOrder: 1 },
    ],
    items: [
      {
        id: 'item-bratwurst',
        name: 'Bratwurst',
        categoryId: 'category-essen',
        priceCents: 350,
        sortOrder: 1,
        isAvailable: true,
        stationIds: ['station-kueche'],
        productionMinutes: 8,
        isQueueIndependent: false,
      },
      {
        id: 'item-bier',
        name: 'Bier',
        categoryId: 'category-getraenke',
        priceCents: 420,
        sortOrder: 2,
        isAvailable: false,
        stationIds: ['station-theke-innen', 'station-theke-aussen'],
        productionMinutes: null,
        isQueueIndependent: false,
      },
    ],
    stations: [
      { id: 'station-kueche', name: 'Kueche', sortOrder: 1 },
      { id: 'station-theke-innen', name: 'Theke innen', sortOrder: 2 },
      { id: 'station-theke-aussen', name: 'Theke aussen', sortOrder: 3 },
    ],
  }
}

export function draftWith(lines: DraftOrder['lines']): DraftOrder {
  return { festivalId: null, tableName: '', lines, clientOrderId: null, deliveryModes: {} }
}
