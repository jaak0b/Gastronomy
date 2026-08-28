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
  tableSuggestions: [],
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

  it('sorts the categories and the items inside them by name', () => {
    const view = mountCatalog()

    expect(view.findAll('.category-heading').map((element) => element.text())).toEqual([
      'Essen',
      'Getränke',
    ])
    expect(view.findAll('.item-row .name').map((element) => element.text())).toEqual([
      'Bratwurst',
      'Wasser',
    ])
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
