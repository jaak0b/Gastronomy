import { mount } from '@vue/test-utils'
import CatalogPage from '../../../src/phone/views/Catalog.vue'
import { useCatalogStore } from '../../../src/phone/stores/catalog'
import { CatalogView, ItemEstimateView } from '../../../src/shared/api/generatedSchemas'
import { testPlugins } from '../../support/plugins'

export const CATALOG: CatalogView = {
  festival: null,
  categories: [
    { categoryId: 'category-essen', name: 'Essen', colourHex: '#FFEB3B', sortOrder: 1 },
    { categoryId: 'category-getraenke', name: 'Getränke', colourHex: '#C62828', sortOrder: 2 },
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
      productionMinutes: null,
      isQueueIndependent: false,
    },
    {
      id: 'item-wasser',
      name: 'Wasser',
      categoryId: 'category-getraenke',
      priceCents: 200,
      sortOrder: 2,
      isAvailable: true,
      stationIds: ['station-bar'],
      productionMinutes: null,
      isQueueIndependent: false,
    },
  ],
  stations: [
    { id: 'station-kueche', name: 'Küche', sortOrder: 1 },
    { id: 'station-bar', name: 'Bar', sortOrder: 2 },
  ],
}

export function mountCatalog() {
  const catalog = useCatalogStore()
  catalog.catalog = CATALOG
  return mount(CatalogPage, {
    global: { plugins: testPlugins() },
    attachTo: document.body,
  })
}

export type MountedCatalog = ReturnType<typeof mountCatalog>

export function categoryButtons(view: MountedCatalog): string[] {
  return view.findAll('[data-test="category-button"]').map((element) => element.text())
}

export async function openCategory(view: MountedCatalog, name: string): Promise<void> {
  const button = view
    .findAll('[data-test="category-button"]')
    .find((candidate) => candidate.text().endsWith(name))
  if (button === undefined) {
    throw new Error(`No category button names ${name}.`)
  }
  await button.trigger('click')
}

export function itemRowNamed(view: MountedCatalog, name: string) {
  const row = view
    .findAll('[data-test="item-row"]')
    .find((candidate) => candidate.get('[data-test="name"]').text() === name)
  if (row === undefined) {
    throw new Error(`No item row names ${name}.`)
  }
  return row
}

export async function tapToAdd(view: MountedCatalog, name: string): Promise<void> {
  await itemRowNamed(view, name).get('[data-test="add"]').trigger('click')
}

export function stationChoiceNamed(name: string): HTMLElement {
  const choice = [...document.querySelectorAll<HTMLElement>('[data-test="station-choice"]')].find(
    (candidate) => candidate.textContent?.trim().startsWith(name),
  )
  if (choice === undefined) {
    throw new Error(`No station choice names ${name}.`)
  }
  return choice
}

export async function goBackToTheCategories(view: MountedCatalog): Promise<void> {
  await view.get('[data-test="back-to-categories"]').trigger('click')
}

export const CATALOG_WITH_A_STATION_CHOICE: CatalogView = {
  ...CATALOG,
  items: [
    ...CATALOG.items,
    {
      id: 'item-kaffee',
      name: 'Kaffee',
      categoryId: 'category-essen',
      priceCents: 250,
      sortOrder: 3,
      isAvailable: true,
      stationIds: ['station-kueche', 'station-bar'],
      productionMinutes: null,
      isQueueIndependent: false,
    },
  ],
}

export const CATALOG_WITH_TIMED_ITEMS: CatalogView = {
  ...CATALOG,
  items: [
    { ...CATALOG.items[0], productionMinutes: 10 },
    { ...CATALOG.items[1], productionMinutes: 2 },
    {
      id: 'item-kaffee',
      name: 'Kaffee',
      categoryId: 'category-essen',
      priceCents: 250,
      sortOrder: 3,
      isAvailable: true,
      stationIds: ['station-kueche', 'station-bar'],
      productionMinutes: 10,
      isQueueIndependent: false,
    },
  ],
}

export const TIMED_ESTIMATES: ItemEstimateView[] = [
  { catalogItemId: 'item-bratwurst', stationId: 'station-kueche', readyInMinutes: 10 },
  { catalogItemId: 'item-kaffee', stationId: 'station-kueche', readyInMinutes: 10 },
  { catalogItemId: 'item-kaffee', stationId: 'station-bar', readyInMinutes: 60 },
]
