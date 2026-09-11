import { beforeEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import Catalog from '../../../src/views/Catalog.vue'
import { useCatalogStore } from '../../../src/stores/catalog'
import { useEstimatesStore } from '../../../src/stores/estimates'
import { useOrderStore } from '../../../src/stores/order'
import type { Catalog as CatalogData, StationEstimate } from '../../../src/core/apiTypes'
import { currentRoute, navigate } from '../../../src/router'
import { testPlugins } from '../../support/plugins'

const CATALOG: CatalogData = {
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

type MountedCatalog = ReturnType<typeof mountCatalog>

function categoryButtons(view: MountedCatalog): string[] {
  return view.findAll('.category-button').map((element) => element.text())
}

async function openCategory(view: MountedCatalog, index: number): Promise<void> {
  await view.findAll('.category-button')[index].trigger('click')
}

async function goBackToTheCategories(view: MountedCatalog): Promise<void> {
  await view.get('.back-to-categories').trigger('click')
}

describe('the categories on the ordering screen', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
    navigate('/')
    vi.stubGlobal('fetch', vi.fn(async () => new Response('{}', { status: 200 })))
  })

  it('lays the category buttons out in a grid of their own', () => {
    const view = mountCatalog()

    const grid = view.get('.category-grid')
    expect(grid.findAll('.category-button')).toHaveLength(2)
    view.findAll('.category-button').forEach((button) => {
      expect(button.element.parentElement).toBe(grid.element)
    })
  })

  it('offers every category as a button of its own', () => {
    const view = mountCatalog()

    expect(categoryButtons(view)).toEqual(['Essen', 'Getränke'])
  })

  it('shows no item at all until the waiter has picked a category', () => {
    const view = mountCatalog()

    expect(view.findAll('.item-row')).toHaveLength(0)
  })

  it('docks the basket bar at the bottom, so a long list of categories never hides it', () => {
    const view = mountCatalog()

    expect(view.get('.to-review').element.closest('.docked-strip')).not.toBeNull()
  })

  it('keeps the order the laptop gives the categories in', () => {
    const catalog = useCatalogStore()
    catalog.catalog = { ...CATALOG, categories: [...CATALOG.categories].reverse() }
    const view = mount(Catalog, { global: { plugins: testPlugins() }, attachTo: document.body })

    expect(categoryButtons(view)).toEqual(['Getränke', 'Essen'])
  })

  it('paints every button in the colour the admin chose for that category', () => {
    const view = mountCatalog()

    const painted = view
      .findAll('.category-button')
      .map((element) => (element.element as HTMLElement).style.backgroundColor)

    expect(painted).toEqual(['rgb(255, 235, 59)', 'rgb(198, 40, 40)'])
  })

  it('writes on each button in the lettering colour that stays readable on it', () => {
    const view = mountCatalog()

    const lettering = view
      .findAll('.category-button')
      .map((element) => (element.element as HTMLElement).style.color)

    expect(lettering).toEqual(['rgb(0, 0, 0)', 'rgb(255, 255, 255)'])
  })

  it('shows no category and no item while the laptop still holds no menu', () => {
    const catalog = useCatalogStore()
    catalog.catalog = { ...CATALOG, categories: [], items: [] }
    const view = mount(Catalog, { global: { plugins: testPlugins() }, attachTo: document.body })

    expect(view.findAll('.category-button')).toHaveLength(0)
    expect(view.findAll('.item-row')).toHaveLength(0)
  })
})

describe('the items of one category on the ordering screen', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
    navigate('/')
    vi.stubGlobal('fetch', vi.fn(async () => new Response('{}', { status: 200 })))
  })

  it('shows that category and its items once its button is tapped', async () => {
    const view = mountCatalog()

    await openCategory(view, 1)

    expect(view.get('.open-category-name').text()).toBe('Getränke')
    expect(view.findAll('.item-row .name').map((element) => element.text())).toEqual(['Wasser'])
    expect(view.find('.back-to-categories').exists()).toBe(true)
  })

  it('puts the basket bar away while the waiter is inside a category', async () => {
    const view = mountCatalog()

    await openCategory(view, 0)

    expect(view.find('.basket-bar').exists()).toBe(false)
  })

  it('docks the way back at the bottom, so a long list of items never hides it', async () => {
    const view = mountCatalog()

    await openCategory(view, 0)

    expect(view.get('.back-to-categories').element.closest('.docked-strip')).not.toBeNull()
  })

  it('returns to the categories, with the order intact, when the way back is tapped', async () => {
    const view = mountCatalog()
    const order = useOrderStore()
    await openCategory(view, 0)
    await view.findAll('.item-row .add')[0].trigger('click')

    await goBackToTheCategories(view)

    expect(order.draft.lines).toHaveLength(1)
    expect(categoryButtons(view)).toEqual(['1 x Essen', 'Getränke'])
    expect(view.findAll('.item-row')).toHaveLength(0)
  })

  it('keeps the portions already ordered while the waiter looks at another category', async () => {
    const view = mountCatalog()
    const order = useOrderStore()
    await openCategory(view, 0)
    await view.findAll('.item-row .add')[0].trigger('click')
    await goBackToTheCategories(view)

    await openCategory(view, 1)
    await goBackToTheCategories(view)
    await openCategory(view, 0)

    expect(order.draft.lines).toHaveLength(1)
    expect(view.findAll('.item-row .count')[0].text()).toBe('1')
  })

  it('sends the waiter back to the categories when the open one leaves the menu', async () => {
    const view = mountCatalog()
    const catalog = useCatalogStore()
    await openCategory(view, 0)

    catalog.catalog = {
      ...CATALOG,
      categories: [CATALOG.categories[1]],
      items: [CATALOG.items[1]],
    }
    await view.vm.$nextTick()

    expect(view.findAll('.item-row')).toHaveLength(0)
    expect(categoryButtons(view)).toEqual(['Getränke'])
  })
})

describe('the portions written on a category button', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
    navigate('/')
    vi.stubGlobal('fetch', vi.fn(async () => new Response('{}', { status: 200 })))
  })

  it('writes how many portions of that category are on the order', async () => {
    const view = mountCatalog()
    await openCategory(view, 0)

    await view.findAll('.item-row .add')[0].trigger('click')
    await view.findAll('.item-row .add')[0].trigger('click')
    await goBackToTheCategories(view)

    expect(categoryButtons(view)).toEqual(['2 x Essen', 'Getränke'])
  })

  it('writes the category name on its own while nothing from it is on the order', () => {
    const view = mountCatalog()

    expect(categoryButtons(view)).toEqual(['Essen', 'Getränke'])
  })

  it('writes the name on its own again once the last portion is taken off', async () => {
    const view = mountCatalog()
    await openCategory(view, 0)
    await view.findAll('.item-row .add')[0].trigger('click')

    await view.findAll('.item-row .remove-one')[0].trigger('click')
    await goBackToTheCategories(view)

    expect(categoryButtons(view)).toEqual(['Essen', 'Getränke'])
  })

  it('keeps counting the portions of the category the waiter is not looking at', async () => {
    const view = mountCatalog()
    await openCategory(view, 0)
    await view.findAll('.item-row .add')[0].trigger('click')
    await goBackToTheCategories(view)

    await openCategory(view, 1)
    await view.findAll('.item-row .add')[0].trigger('click')
    await goBackToTheCategories(view)

    expect(categoryButtons(view)).toEqual(['1 x Essen', '1 x Getränke'])
  })
})

describe('building the order on the ordering screen', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
    navigate('/')
    vi.stubGlobal('fetch', vi.fn(async () => new Response('{}', { status: 200 })))
  })

  it('puts one portion on the order when an item is tapped', async () => {
    const view = mountCatalog()
    const order = useOrderStore()
    await openCategory(view, 0)

    await view.findAll('.item-row .add')[0].trigger('click')
    await view.findAll('.item-row .add')[0].trigger('click')

    expect(order.draft.lines).toHaveLength(2)
    expect(view.findAll('.item-row .count')[0].text()).toBe('2')
  })

  it('puts a portion carrying the typed note on a line of its own', async () => {
    const view = mountCatalog()
    const order = useOrderStore()
    await openCategory(view, 0)

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
    await openCategory(view, 0)

    await view.findAll('.item-row .add')[0].trigger('click')
    await view.findAll('.item-row .add')[0].trigger('click')
    await view.findAll('.item-row .remove-one')[0].trigger('click')

    expect(order.draft.lines).toHaveLength(1)
  })
})

describe('the way from the ordering screen to the summary', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
    navigate('/')
    vi.stubGlobal('fetch', vi.fn(async () => new Response('{}', { status: 200 })))
  })

  async function catalogWithOnePortion() {
    const view = mountCatalog()
    await openCategory(view, 0)
    await view.findAll('.item-row .add')[0].trigger('click')
    await goBackToTheCategories(view)
    return view
  }

  it('keeps the way to the summary open while the table is empty, so tapping it can say so', async () => {
    const view = await catalogWithOnePortion()

    expect(view.get('.to-review').attributes('disabled')).toBeUndefined()
  })

  it('marks the table field instead of moving on when no table was entered', async () => {
    const view = await catalogWithOnePortion()

    await view.get('.to-review').trigger('click')

    expect(currentRoute.value).toEqual({ name: 'home' })
    expect(view.get('.table-field').classes()).toContain('is-missing')
  })

  it('puts the cursor in the table field so the keyboard opens on the thing that is missing', async () => {
    const view = await catalogWithOnePortion()

    await view.get('.to-review').trigger('click')

    expect(document.activeElement).toBe(view.get('.table-field input').element)
  })

  it('takes the mark off again as soon as a table is typed', async () => {
    const view = await catalogWithOnePortion()
    await view.get('.to-review').trigger('click')

    await view.get('.table-field input').setValue('Tisch 12')

    expect(view.get('.table-field').classes()).not.toContain('is-missing')
  })

  it('moves on to the summary once a table is there', async () => {
    const view = await catalogWithOnePortion()
    await view.get('.table-field input').setValue('Tisch 12')

    await view.get('.to-review').trigger('click')

    expect(currentRoute.value).toEqual({ name: 'review' })
  })
})

describe('the length of what a waiter types on the ordering screen', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
    navigate('/')
    vi.stubGlobal('fetch', vi.fn(async () => new Response('{}', { status: 200 })))
  })

  it('stops the note for the whole order after two hundred characters', () => {
    const view = mountCatalog()

    expect(view.get('.order-note textarea').attributes('maxlength')).toBe('200')
  })

  it('stops the table name after forty characters', () => {
    const view = mountCatalog()

    expect(view.get('.table-input input').attributes('maxlength')).toBe('40')
  })
})

const CATALOG_WITH_A_STATION_CHOICE: CatalogData = {
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
    },
  ],
}

describe('the question about which station is to make an item', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
    navigate('/')
    vi.stubGlobal('fetch', vi.fn(async () => new Response('{}', { status: 200 })))
  })

  function mountCatalogWithAStationChoice() {
    const catalog = useCatalogStore()
    catalog.catalog = CATALOG_WITH_A_STATION_CHOICE
    return mount(Catalog, { global: { plugins: testPlugins() }, attachTo: document.body })
  }

  async function askWhereTheCoffeeIsMade(view: MountedCatalog): Promise<void> {
    await openCategory(view, 0)
    await view.findAll('.item-row .add')[1].trigger('click')
    await vi.waitFor(() => expect(document.querySelector('.line-station-sheet')).not.toBeNull())
  }

  it('forgets the unanswered question when the waiter leaves the category', async () => {
    const view = mountCatalogWithAStationChoice()
    const order = useOrderStore()
    await askWhereTheCoffeeIsMade(view)

    await goBackToTheCategories(view)
    await openCategory(view, 1)
    await view.vm.$nextTick()
    await view.vm.$nextTick()

    expect(document.querySelector('.line-station-sheet')).toBeNull()
    expect(order.draft.lines).toHaveLength(0)
  })

  it('adds nothing and stays in the category when the waiter cancels the question', async () => {
    const view = mountCatalogWithAStationChoice()
    const order = useOrderStore()
    await askWhereTheCoffeeIsMade(view)

    document.querySelector<HTMLElement>('.cancel-station-choice')?.click()
    await view.vm.$nextTick()

    expect(document.querySelector('.line-station-sheet')).toBeNull()
    expect(order.draft.lines).toHaveLength(0)
    expect(view.findAll('.item-row').length).toBeGreaterThan(0)
  })
})

const CATALOG_WITH_TIMED_ITEMS: CatalogData = {
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
    },
  ],
}

const TIMED_QUEUES: StationEstimate[] = [
  { stationId: 'station-kueche', queuedMinutes: 0 },
  { stationId: 'station-bar', queuedMinutes: 50 },
]

describe('the waiting time on the ordering screen', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
    navigate('/')
    vi.stubGlobal('fetch', vi.fn(async () => new Response('{}', { status: 200 })))
  })

  function mountCatalogWithEstimates(): MountedCatalog {
    const catalog = useCatalogStore()
    catalog.catalog = CATALOG_WITH_TIMED_ITEMS
    useEstimatesStore().stations = TIMED_QUEUES
    return mount(Catalog, { global: { plugins: testPlugins() }, attachTo: document.body })
  }

  it('writes one time on an item only one station makes', async () => {
    const view = mountCatalogWithEstimates()

    await openCategory(view, 0)

    expect(view.findAll('.item-row .name').map((element) => element.text())).toEqual([
      'Bratwurst (~10 Min.)',
      'Kaffee (~10 - 60 Min.)',
    ])
  })

  it('shifts the range with the portions already on the order', async () => {
    const view = mountCatalogWithEstimates()
    useOrderStore().addItem({
      catalogItemId: 'item-wasser',
      note: null,
      stationId: 'station-bar',
      name: 'Wasser',
    })

    await openCategory(view, 0)

    expect(view.findAll('.item-row .name').map((element) => element.text())).toEqual([
      'Bratwurst (~10 Min.)',
      'Kaffee (~10 - 62 Min.)',
    ])
  })

  it('writes the time of each station on its button in the station question', async () => {
    const view = mountCatalogWithEstimates()

    await openCategory(view, 0)
    await view.findAll('.item-row .add')[1].trigger('click')
    await vi.waitFor(() => expect(document.querySelector('.line-station-sheet')).not.toBeNull())

    const choices = [...document.querySelectorAll('.station-choice')].map((element) =>
      element.textContent?.trim(),
    )
    expect(choices).toEqual(['Küche (~10 Min.)', 'Bar (~60 Min.)'])
  })

  it('writes no time on the buttons of an item nobody gave a production time', async () => {
    const catalog = useCatalogStore()
    catalog.catalog = CATALOG_WITH_A_STATION_CHOICE
    useEstimatesStore().stations = TIMED_QUEUES
    const view = mount(Catalog, { global: { plugins: testPlugins() }, attachTo: document.body })

    await openCategory(view, 0)
    await view.findAll('.item-row .add')[1].trigger('click')
    await vi.waitFor(() => expect(document.querySelector('.line-station-sheet')).not.toBeNull())

    const choices = [...document.querySelectorAll('.station-choice')].map((element) =>
      element.textContent?.trim(),
    )
    expect(choices).toEqual(['Küche', 'Bar'])
  })

  it('writes the station time after moving the portions that are already on the order', async () => {
    const view = mountCatalogWithEstimates()
    const order = useOrderStore()
    order.addItem({
      catalogItemId: 'item-kaffee',
      note: null,
      stationId: 'station-bar',
      name: 'Kaffee',
    })
    order.addItem({
      catalogItemId: 'item-kaffee',
      note: null,
      stationId: 'station-bar',
      name: 'Kaffee',
    })

    await openCategory(view, 0)
    await view.findAll('.item-row')[1].get('.change-station').trigger('click')
    await vi.waitFor(() => expect(document.querySelector('.line-station-sheet')).not.toBeNull())

    const choices = [...document.querySelectorAll('.station-choice')].map((element) =>
      element.textContent?.trim(),
    )
    expect(choices).toEqual(['Küche (~20 Min.)', 'Bar (~70 Min.)'])
  })

  it('keeps a line that sold out out of the range on the row', async () => {
    const catalogWithASoldOutWasser: CatalogData = {
      ...CATALOG_WITH_TIMED_ITEMS,
      items: CATALOG_WITH_TIMED_ITEMS.items.map((item) =>
        item.id === 'item-wasser' ? { ...item, isAvailable: false } : item,
      ),
    }
    const catalog = useCatalogStore()
    catalog.catalog = catalogWithASoldOutWasser
    useEstimatesStore().stations = TIMED_QUEUES
    useOrderStore().addItem({
      catalogItemId: 'item-wasser',
      note: null,
      stationId: 'station-bar',
      name: 'Wasser',
    })
    const view = mount(Catalog, { global: { plugins: testPlugins() }, attachTo: document.body })

    await openCategory(view, 0)

    expect(view.findAll('.item-row .name').map((element) => element.text())).toEqual([
      'Bratwurst (~10 Min.)',
      'Kaffee (~10 - 60 Min.)',
    ])
  })

  it('writes no time on the station buttons when the item has just sold out', async () => {
    const catalogWithASoldOutKaffee: CatalogData = {
      ...CATALOG_WITH_TIMED_ITEMS,
      items: CATALOG_WITH_TIMED_ITEMS.items.map((item) =>
        item.id === 'item-kaffee' ? { ...item, isAvailable: false } : item,
      ),
    }
    const catalog = useCatalogStore()
    catalog.catalog = catalogWithASoldOutKaffee
    useEstimatesStore().stations = TIMED_QUEUES
    useOrderStore().addItem({
      catalogItemId: 'item-kaffee',
      note: null,
      stationId: 'station-bar',
      name: 'Kaffee',
    })
    const view = mount(Catalog, { global: { plugins: testPlugins() }, attachTo: document.body })

    await openCategory(view, 0)
    await view.findAll('.item-row')[1].get('.change-station').trigger('click')
    await vi.waitFor(() => expect(document.querySelector('.line-station-sheet')).not.toBeNull())

    const choices = [...document.querySelectorAll('.station-choice')].map((element) =>
      element.textContent?.trim(),
    )
    expect(choices).toEqual(['Küche', 'Bar'])
  })
})

describe('the items screen while an order is frozen on the laptop', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
    navigate('/')
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => {
        throw new TypeError('the laptop cannot be reached')
      }),
    )
  })

  async function anOrderThatCouldNotBeSent() {
    const order = useOrderStore()
    order.addItem({
      catalogItemId: 'item-wasser',
      note: null,
      stationId: 'station-bar',
      name: 'Wasser',
    })
    order.setTable('Tisch 5')
    await order.send(false)
    return order
  }

  it('sends the waiter to the summary, where the failure and both ways out are', async () => {
    await anOrderThatCouldNotBeSent()

    mountCatalog()

    expect(currentRoute.value).toEqual({ name: 'review' })
  })

  it('leaves the frozen order untouched on the way there', async () => {
    const order = await anOrderThatCouldNotBeSent()

    mountCatalog()

    expect(order.draft.lines).toHaveLength(1)
    expect(order.draft.tableName).toBe('Tisch 5')
  })

  it('stays on the items while the order still belongs to the waiter', () => {
    mountCatalog()

    expect(currentRoute.value).toEqual({ name: 'home' })
  })
})

describe('the items screen after the laptop refused an order', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
    navigate('/')
    vi.stubGlobal(
      'fetch',
      vi.fn(
        async () =>
          new Response(
            JSON.stringify({
              code: 'ValidationFailed',
              messageKey: 'order.cannotBeProcessed',
              parameters: {},
              details: null,
            }),
            { status: 400 },
          ),
      ),
    )
  })

  it('leaves the waiter on the items, because that is where the refusal is put right', async () => {
    const order = useOrderStore()
    order.addItem({
      catalogItemId: 'item-wasser',
      note: null,
      stationId: null,
      name: 'Wasser',
    })
    order.setTable('Tisch 5')
    await order.send(false)

    mountCatalog()

    expect(currentRoute.value).toEqual({ name: 'home' })
  })
})
