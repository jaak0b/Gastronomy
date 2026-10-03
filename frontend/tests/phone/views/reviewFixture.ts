import { type VueWrapper } from '@vue/test-utils'
import { useCatalogStore } from '../../../src/phone/stores/catalog'
import { useOrderStore } from '../../../src/phone/stores/order'
import { nextTick } from 'vue'

export const WASSER = {
  id: 'item-wasser',
  name: 'Wasser',
  categoryId: 'category-getraenke',
  priceCents: 200,
  sortOrder: 1,
  isAvailable: true,
  stationIds: ['station-bar'],
  productionMinutes: 0,
  isQueueIndependent: false,
}

export function prepareOrder() {
  const catalog = useCatalogStore()
  catalog.catalog = {
    festival: null,
    categories: [
      { categoryId: 'category-getraenke', name: 'Getränke', colourHex: '#C62828', sortOrder: 1 },
    ],
    items: [WASSER],
    stations: [{ id: 'station-bar', name: 'Bar', sortOrder: 1 }],
  }
  const order = useOrderStore()
  order.addItem({
    catalogItemId: WASSER.id,
    note: null,
    stationId: 'station-bar',
    name: WASSER.name,
  })
  order.setTable('Tisch 3')
  return order
}

export async function sendAndSettleLater(review: VueWrapper): Promise<void> {
  await review.get('[data-test="send-and-settle-later"]').trigger('click')
  await nextTick()
}

export async function sendAndSettle(review: VueWrapper): Promise<void> {
  await review.get('[data-test="send-and-settle"]').trigger('click')
  await nextTick()
}
