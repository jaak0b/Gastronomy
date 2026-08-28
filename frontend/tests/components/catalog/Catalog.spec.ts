import { beforeEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import Catalog from '../../../src/views/Catalog.vue'
import { useCatalogStore } from '../../../src/stores/catalog'
import type { Catalog as CatalogData } from '../../../src/core/apiTypes'
import { testPlugins } from '../../support/plugins'

const CATALOG: CatalogData = {
  version: '1',
  categories: [
    { name: 'Essen', sortOrder: 1 },
    { name: 'Getränke', sortOrder: 2 },
  ],
  items: [
    {
      id: 'item-bratwurst',
      name: 'Bratwurst',
      categoryName: 'Essen',
      priceCents: 350,
      sortOrder: 1,
      isAvailable: true,
      stationIds: ['station-kueche'],
    },
    {
      id: 'item-wasser',
      name: 'Wasser',
      categoryName: 'Getränke',
      priceCents: 200,
      sortOrder: 2,
      isAvailable: true,
      stationIds: ['station-bar'],
    },
  ],
  stations: [
    { id: 'station-kueche', name: 'Küche', sortOrder: 1 },
    { id: 'station-bar', name: 'Bar', sortOrder: 2 },
  ],
  tableSuggestions: [],
}

function mountCatalog() {
  const catalog = useCatalogStore()
  catalog.catalog = CATALOG
  return mount(Catalog, {
    global: { plugins: testPlugins(), stubs: { BasketBar: true } },
    attachTo: document.body,
  })
}

function chipFor(view: ReturnType<typeof mountCatalog>, name: string) {
  const chip = view
    .findAll('.category-strip .v-chip')
    .find((candidate) => candidate.text() === name)
  if (chip === undefined) {
    throw new Error(`no chip for ${name}`)
  }
  return chip
}

describe('the ordering screen', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
    vi.stubGlobal('fetch', vi.fn(async () => new Response('{}', { status: 200 })))
  })

  it('shows the items of every category at once', () => {
    const view = mountCatalog()

    const names = view.findAll('.item-row .name').map((element) => element.text())

    expect(names).toEqual(['Bratwurst', 'Wasser'])
  })

  it('heads each category so a long list stays readable', () => {
    const view = mountCatalog()

    const headings = view.findAll('.category-heading').map((element) => element.text())

    expect(headings).toEqual(['Essen', 'Getränke'])
  })

  it('scrolls to a category when its chip is tapped, rather than hiding the others', async () => {
    const view = mountCatalog()
    const heading = view.get('[data-category="Getränke"]').element
    const scrollIntoView = vi.fn()
    heading.scrollIntoView = scrollIntoView

    await chipFor(view, 'Getränke').trigger('click')

    expect(scrollIntoView).toHaveBeenCalled()
    expect(view.findAll('.item-row .name').map((element) => element.text())).toEqual([
      'Bratwurst',
      'Wasser',
    ])
  })

  it('drops the headings while a search is running', async () => {
    const view = mountCatalog()

    await view.get('.catalog-search input').setValue('wass')

    expect(view.findAll('.category-heading')).toHaveLength(0)
    expect(view.findAll('.item-row .name').map((element) => element.text())).toEqual(['Wasser'])
  })
})
