import { beforeEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import Catalog from '../../../src/views/Catalog.vue'
import { useCatalogStore } from '../../../src/stores/catalog'
import { useOrderStore } from '../../../src/stores/order'
import type { Catalog as CatalogData } from '../../../src/core/apiTypes'
import { currentRoute, navigate } from '../../../src/router'
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
}

function mountCatalog() {
  const catalog = useCatalogStore()
  catalog.catalog = CATALOG
  return mount(Catalog, {
    global: { plugins: testPlugins() },
    attachTo: document.body,
  })
}

describe('the ordering screen', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
    navigate('/')
    vi.stubGlobal('fetch', vi.fn(async () => new Response('{}', { status: 200 })))
  })

  it('offers every category as a tab, sorted by name', () => {
    const view = mountCatalog()

    const tabs = view.findAll('.category-tabs .category-tab').map((element) => element.text())

    expect(tabs).toEqual(['Essen', 'Getränke'])
  })

  it('shows the items of the first category when the screen opens', () => {
    const view = mountCatalog()

    const names = view.findAll('.item-row .name').map((element) => element.text())

    expect(names).toEqual(['Bratwurst'])
  })

  it('shows the items of another category once its tab is tapped', async () => {
    const view = mountCatalog()

    await view.findAll('.category-tabs .category-tab')[1].trigger('click')

    const names = view.findAll('.item-row .name').map((element) => element.text())

    expect(names).toEqual(['Wasser'])
  })

  it('keeps the portions already ordered while the server looks at another category', async () => {
    const view = mountCatalog()
    const order = useOrderStore()

    await view.findAll('.item-row .add')[0].trigger('click')
    await view.findAll('.category-tabs .category-tab')[1].trigger('click')
    await view.findAll('.category-tabs .category-tab')[0].trigger('click')

    expect(order.draft.lines).toHaveLength(1)
    expect(view.findAll('.item-row .count')[0].text()).toBe('1')
  })

  it('writes in front of the category name how many portions of it are on the order', async () => {
    const view = mountCatalog()

    await view.findAll('.item-row .add')[0].trigger('click')
    await view.findAll('.item-row .add')[0].trigger('click')

    const tabs = view.findAll('.category-tabs .category-tab').map((element) => element.text())

    expect(tabs).toEqual(['2 x Essen', 'Getränke'])
  })

  it('marks the tab of a category that already has portions on the order', async () => {
    const view = mountCatalog()

    await view.findAll('.item-row .add')[0].trigger('click')

    const marked = view
      .findAll('.category-tabs .category-tab')
      .map((element) => element.classes()).map((classes) => classes.includes('holds-portions'))

    expect(marked).toEqual([true, false])
  })

  it('takes the mark off the tab once the last portion of that category is removed', async () => {
    const view = mountCatalog()
    await view.findAll('.item-row .add')[0].trigger('click')

    await view.findAll('.item-row .remove-one')[0].trigger('click')

    expect(view.findAll('.category-tabs .category-tab')[0].classes()).not.toContain('holds-portions')
  })

  it('writes the category name on its own again once its portions are taken off', async () => {
    const view = mountCatalog()
    await view.findAll('.item-row .add')[0].trigger('click')

    await view.findAll('.item-row .remove-one')[0].trigger('click')

    const tabs = view.findAll('.category-tabs .category-tab').map((element) => element.text())

    expect(tabs).toEqual(['Essen', 'Getränke'])
  })

  it('keeps counting the portions of the category the server is not looking at', async () => {
    const view = mountCatalog()
    await view.findAll('.item-row .add')[0].trigger('click')

    await view.findAll('.category-tabs .category-tab')[1].trigger('click')
    await view.findAll('.item-row .add')[0].trigger('click')

    const tabs = view.findAll('.category-tabs .category-tab').map((element) => element.text())

    expect(tabs).toEqual(['1 x Essen', '1 x Getränke'])
  })

  it('shows no items and no tab when the catalog is still empty', () => {
    const catalog = useCatalogStore()
    catalog.catalog = { ...CATALOG, categories: [], items: [] }
    const view = mount(Catalog, { global: { plugins: testPlugins() }, attachTo: document.body })

    expect(view.findAll('.category-tabs .category-tab')).toHaveLength(0)
    expect(view.findAll('.item-row')).toHaveLength(0)
  })

  it('puts one portion on the order when an item is tapped', async () => {
    const view = mountCatalog()
    const order = useOrderStore()

    await view.findAll('.item-row .add')[0].trigger('click')
    await view.findAll('.item-row .add')[0].trigger('click')

    expect(order.draft.lines).toHaveLength(2)
    expect(view.findAll('.item-row .count')[0].text()).toBe('2')
  })

  it('puts a portion carrying the typed note on a line of its own', async () => {
    const view = mountCatalog()
    const order = useOrderStore()

    await view.findAll('.item-row .add')[0].trigger('click')
    await view.findAll('.item-row .add-note')[0].trigger('click')
    const field = document.querySelector('.note-dialog .note-input input') as HTMLInputElement
    field.value = 'ohne Eis'
    field.dispatchEvent(new Event('input'))
    await view.vm.$nextTick()
    ;(document.querySelector('.note-dialog .note-confirm') as HTMLElement).click()
    await view.vm.$nextTick()

    expect(order.draft.lines.map((line) => line.note)).toEqual([null, 'ohne Eis'])
    expect(view.findAll('.item-row .count')[0].text()).toBe('1')
    expect(view.get('.item-row .note-group .group-label').text()).toBe('ohne Eis')
  })

  it('takes the most recently added portion off again', async () => {
    const view = mountCatalog()
    const order = useOrderStore()

    await view.findAll('.item-row .add')[0].trigger('click')
    await view.findAll('.item-row .add')[0].trigger('click')
    await view.findAll('.item-row .remove-one')[0].trigger('click')

    expect(order.draft.lines).toHaveLength(1)
  })

  it('keeps the way to the summary open while the table is empty, so tapping it can say so', async () => {
    const view = mountCatalog()

    await view.findAll('.item-row .add')[0].trigger('click')

    expect(view.get('.to-review').attributes('disabled')).toBeUndefined()
  })

  it('marks the table field instead of moving on when no table was entered', async () => {
    const view = mountCatalog()
    await view.findAll('.item-row .add')[0].trigger('click')

    await view.get('.to-review').trigger('click')

    expect(currentRoute.value).toEqual({ name: 'home' })
    expect(view.get('.table-field').classes()).toContain('is-missing')
  })

  it('puts the cursor in the table field so the keyboard opens on the thing that is missing', async () => {
    const view = mountCatalog()
    await view.findAll('.item-row .add')[0].trigger('click')

    await view.get('.to-review').trigger('click')

    expect(document.activeElement).toBe(view.get('.table-field input').element)
  })

  it('takes the mark off again as soon as a table is typed', async () => {
    const view = mountCatalog()
    await view.findAll('.item-row .add')[0].trigger('click')
    await view.get('.to-review').trigger('click')

    await view.get('.table-field input').setValue('Tisch 12')

    expect(view.get('.table-field').classes()).not.toContain('is-missing')
  })

  it('moves on to the summary once a table is there', async () => {
    const view = mountCatalog()
    await view.findAll('.item-row .add')[0].trigger('click')
    await view.get('.table-field input').setValue('Tisch 12')

    await view.get('.to-review').trigger('click')

    expect(currentRoute.value).toEqual({ name: 'review' })
  })
})
