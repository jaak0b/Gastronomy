import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { enableAutoUnmount, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import CatalogPage from '../../../src/phone/views/Catalog.vue'
import { useCatalogStore } from '../../../src/phone/stores/catalog'
import { useEstimatesStore } from '../../../src/phone/stores/estimates'
import { useOrderStore } from '../../../src/phone/stores/order'
import { useSessionStore } from '../../../src/shared/stores/session'
import { CatalogView, ItemEstimateView } from '../../../src/shared/api/generatedSchemas'
import { currentRoute, navigate } from '../../../src/shared/router/router'
import { testPlugins } from '../../support/plugins'

enableAutoUnmount(afterEach)

const CATALOG: CatalogView = {
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

function mountCatalog() {
  const catalog = useCatalogStore()
  catalog.catalog = CATALOG
  return mount(CatalogPage, {
    global: { plugins: testPlugins() },
    attachTo: document.body,
  })
}

type MountedCatalog = ReturnType<typeof mountCatalog>

function categoryButtons(view: MountedCatalog): string[] {
  return view.findAll('[data-test="category-button"]').map((element) => element.text())
}

async function openCategory(view: MountedCatalog, name: string): Promise<void> {
  const button = view
    .findAll('[data-test="category-button"]')
    .find((candidate) => candidate.text().endsWith(name))
  if (button === undefined) {
    throw new Error(`No category button names ${name}.`)
  }
  await button.trigger('click')
}

function itemRowNamed(view: MountedCatalog, name: string) {
  const row = view
    .findAll('[data-test="item-row"]')
    .find((candidate) => candidate.get('[data-test="name"]').text() === name)
  if (row === undefined) {
    throw new Error(`No item row names ${name}.`)
  }
  return row
}

async function tapToAdd(view: MountedCatalog, name: string): Promise<void> {
  await itemRowNamed(view, name).get('[data-test="add"]').trigger('click')
}

function stationChoiceNamed(name: string): HTMLElement {
  const choice = [...document.querySelectorAll<HTMLElement>('[data-test="station-choice"]')].find(
    (candidate) => candidate.textContent?.trim().startsWith(name),
  )
  if (choice === undefined) {
    throw new Error(`No station choice names ${name}.`)
  }
  return choice
}

async function goBackToTheCategories(view: MountedCatalog): Promise<void> {
  await view.get('[data-test="back-to-categories"]').trigger('click')
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

    const grid = view.get('[data-test="category-grid"]')
    expect(grid.findAll('[data-test="category-button"]')).toHaveLength(2)
    view.findAll('[data-test="category-button"]').forEach((button) => {
      expect(button.element.parentElement).toBe(grid.element)
    })
  })

  it('offers every category as a button of its own', () => {
    const view = mountCatalog()

    expect(categoryButtons(view)).toEqual(['Essen', 'Getränke'])
  })

  it('shows no item at all until the waiter has picked a category', () => {
    const view = mountCatalog()

    expect(view.findAll('[data-test="item-row"]')).toHaveLength(0)
  })

  it('docks the basket bar at the bottom, so a long list of categories never hides it', () => {
    const view = mountCatalog()

    expect(view.get('[data-test="to-review"]').element.closest('[data-test="docked-strip"]')).not.toBeNull()
  })

  it('keeps the table and the summary together in one tray at the bottom', () => {
    const view = mountCatalog()

    const tray = view.get('[data-test="docked-strip"]')
    expect(tray.find('[data-test="table-field"]').exists()).toBe(true)
    expect(tray.find('[data-test="to-review"]').exists()).toBe(true)
  })

  it('keeps the order the laptop gives the categories in', () => {
    const catalog = useCatalogStore()
    catalog.catalog = { ...CATALOG, categories: [...CATALOG.categories].reverse() }
    const view = mount(CatalogPage, { global: { plugins: testPlugins() }, attachTo: document.body })

    expect(categoryButtons(view)).toEqual(['Getränke', 'Essen'])
  })

  it('paints every button in the colour the admin chose for that category', () => {
    const view = mountCatalog()

    const painted = view
      .findAll('[data-test="category-button"]')
      .map((element) => (element.element as HTMLElement).style.backgroundColor)

    expect(painted).toEqual(['rgb(255, 235, 59)', 'rgb(198, 40, 40)'])
  })

  it('writes on each button in the lettering colour that stays readable on it', () => {
    const view = mountCatalog()

    const lettering = view
      .findAll('[data-test="category-button"]')
      .map((element) => (element.element as HTMLElement).style.color)

    expect(lettering).toEqual(['rgb(0, 0, 0)', 'rgb(255, 255, 255)'])
  })

  it('shows no category and no item while the laptop still holds no menu', () => {
    const catalog = useCatalogStore()
    catalog.catalog = { ...CATALOG, categories: [], items: [] }
    const view = mount(CatalogPage, { global: { plugins: testPlugins() }, attachTo: document.body })

    expect(view.findAll('[data-test="category-button"]')).toHaveLength(0)
    expect(view.findAll('[data-test="item-row"]')).toHaveLength(0)
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

    await openCategory(view, 'Getränke')

    expect(view.get('[data-test="open-category-name"]').text()).toBe('Getränke')
    expect(view.findAll('[data-test="item-row"] [data-test="name"]').map((element) => element.text())).toEqual(['Wasser'])
    expect(view.find('[data-test="back-to-categories"]').exists()).toBe(true)
  })

  it('puts the basket bar away while the waiter is inside a category', async () => {
    const view = mountCatalog()

    await openCategory(view, 'Essen')

    expect(view.find('[data-test="basket-bar"]').exists()).toBe(false)
  })

  it('docks the way back at the bottom, so a long list of items never hides it', async () => {
    const view = mountCatalog()

    await openCategory(view, 'Essen')

    expect(view.get('[data-test="back-to-categories"]').element.closest('[data-test="docked-strip"]')).not.toBeNull()
  })

  it('returns to the categories, with the order intact, when the way back is tapped', async () => {
    const view = mountCatalog()
    const order = useOrderStore()
    await openCategory(view, 'Essen')
    await tapToAdd(view, 'Bratwurst')

    await goBackToTheCategories(view)

    expect(order.draft.lines).toHaveLength(1)
    expect(categoryButtons(view)).toEqual(['1 x Essen', 'Getränke'])
    expect(view.findAll('[data-test="item-row"]')).toHaveLength(0)
  })

  it('keeps the portions already ordered while the waiter looks at another category', async () => {
    const view = mountCatalog()
    const order = useOrderStore()
    await openCategory(view, 'Essen')
    await tapToAdd(view, 'Bratwurst')
    await goBackToTheCategories(view)

    await openCategory(view, 'Getränke')
    await goBackToTheCategories(view)
    await openCategory(view, 'Essen')

    expect(order.draft.lines).toHaveLength(1)
    expect(view.get('[data-test="item-row"] [data-test="note-group"] [data-test="group-count"]').text()).toBe('1')
  })

  it('sends the waiter back to the categories when the open one leaves the menu', async () => {
    const view = mountCatalog()
    const catalog = useCatalogStore()
    await openCategory(view, 'Essen')

    catalog.catalog = {
      ...CATALOG,
      categories: [CATALOG.categories[1]],
      items: [CATALOG.items[1]],
    }
    await view.vm.$nextTick()

    expect(view.findAll('[data-test="item-row"]')).toHaveLength(0)
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
    await openCategory(view, 'Essen')

    await tapToAdd(view, 'Bratwurst')
    await tapToAdd(view, 'Bratwurst')
    await goBackToTheCategories(view)

    expect(categoryButtons(view)).toEqual(['2 x Essen', 'Getränke'])
  })

  it('writes the category name on its own while nothing from it is on the order', () => {
    const view = mountCatalog()

    expect(categoryButtons(view)).toEqual(['Essen', 'Getränke'])
  })

  it('writes the name on its own again once the last portion is taken off', async () => {
    const view = mountCatalog()
    await openCategory(view, 'Essen')
    await tapToAdd(view, 'Bratwurst')

    await view.get('[data-test="item-row"] [data-test="note-group"] [data-test="group-remove"]').trigger('click')
    await goBackToTheCategories(view)

    expect(categoryButtons(view)).toEqual(['Essen', 'Getränke'])
  })

  it('keeps counting the portions of the category the waiter is not looking at', async () => {
    const view = mountCatalog()
    await openCategory(view, 'Essen')
    await tapToAdd(view, 'Bratwurst')
    await goBackToTheCategories(view)

    await openCategory(view, 'Getränke')
    await tapToAdd(view, 'Wasser')
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
    await openCategory(view, 'Essen')

    await tapToAdd(view, 'Bratwurst')
    await tapToAdd(view, 'Bratwurst')

    expect(order.draft.lines).toHaveLength(2)
    expect(view.get('[data-test="item-row"] [data-test="note-group"] [data-test="group-count"]').text()).toBe('2')
  })

  it('puts a portion carrying the typed note on a line of its own', async () => {
    const view = mountCatalog()
    const order = useOrderStore()
    await openCategory(view, 'Essen')

    await tapToAdd(view, 'Bratwurst')
    await itemRowNamed(view, 'Bratwurst').get('[data-test="add-note"]').trigger('click')
    const field = document.querySelector('[data-test="note-dialog"] [data-test="note-input"] input') as HTMLInputElement
    field.value = 'ohne Eis'
    field.dispatchEvent(new Event('input'))
    await view.vm.$nextTick()
    ;(document.querySelector('[data-test="note-dialog"] [data-test="note-confirm"]') as HTMLElement).click()
    await view.vm.$nextTick()

    expect(order.draft.lines.map((line) => line.note)).toEqual([null, 'ohne Eis'])
    const groups = view.findAll('[data-test="item-row"] [data-test="note-group"]')
    expect(groups).toHaveLength(2)
    expect(
      groups
        .filter((group) => !group.find('[data-test="group-note"]').exists())
        .map((group) => group.get('[data-test="group-count"]').text()),
    ).toEqual(['1'])
    expect(
      groups
        .filter((group) => group.find('[data-test="group-note"]').exists())
        .map((group) => group.get('[data-test="group-note"]').text()),
    ).toEqual(['ohne Eis'])
  })

  it('takes the most recently added portion off again', async () => {
    const view = mountCatalog()
    const order = useOrderStore()
    await openCategory(view, 'Essen')

    await tapToAdd(view, 'Bratwurst')
    await tapToAdd(view, 'Bratwurst')
    await view.get('[data-test="item-row"] [data-test="note-group"] [data-test="group-remove"]').trigger('click')

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
    await openCategory(view, 'Essen')
    await tapToAdd(view, 'Bratwurst')
    await goBackToTheCategories(view)
    return view
  }

  it('keeps the way to the summary open while the table is empty, so tapping it can say so', async () => {
    const view = await catalogWithOnePortion()

    expect(view.get('[data-test="to-review"]').attributes('disabled')).toBeUndefined()
  })

  it('marks the table field instead of moving on when no table was entered', async () => {
    const view = await catalogWithOnePortion()

    await view.get('[data-test="to-review"]').trigger('click')

    expect(currentRoute.value).toEqual({ name: 'home' })
    expect(view.get('[data-test="table-field"]').classes()).toContain('is-missing')
  })

  it('puts the cursor in the table field so the keyboard opens on the thing that is missing', async () => {
    const view = await catalogWithOnePortion()

    await view.get('[data-test="to-review"]').trigger('click')

    expect(document.activeElement).toBe(view.get('[data-test="table-field"] input').element)
  })

  it('takes the mark off again as soon as a table is typed', async () => {
    const view = await catalogWithOnePortion()
    await view.get('[data-test="to-review"]').trigger('click')

    await view.get('[data-test="table-field"] input').setValue('Tisch 12')

    expect(view.get('[data-test="table-field"]').classes()).not.toContain('is-missing')
  })

  it('moves on to the summary once a table is there', async () => {
    const view = await catalogWithOnePortion()
    await view.get('[data-test="table-field"] input').setValue('Tisch 12')

    await view.get('[data-test="to-review"]').trigger('click')

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

  it('stops the table name after forty characters', () => {
    const view = mountCatalog()

    expect(view.get('[data-test="table-input"] input').attributes('maxlength')).toBe('40')
  })
})

const CATALOG_WITH_A_STATION_CHOICE: CatalogView = {
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

describe('the question about which station is to make an item', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
    navigate('/')
    vi.stubGlobal('fetch', vi.fn(async () => new Response('{}', { status: 200 })))
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  function mountCatalogWithAStationChoice() {
    const catalog = useCatalogStore()
    catalog.catalog = CATALOG_WITH_A_STATION_CHOICE
    return mount(CatalogPage, { global: { plugins: testPlugins() }, attachTo: document.body })
  }

  async function askWhereTheCoffeeIsMade(view: MountedCatalog): Promise<void> {
    await openCategory(view, 'Essen')
    await tapToAdd(view, 'Kaffee')
    await vi.waitFor(() => expect(document.querySelector('[data-test="line-station-sheet"]')).not.toBeNull())
  }

  it('forgets the unanswered question when the waiter leaves the category', async () => {
    const view = mountCatalogWithAStationChoice()
    const order = useOrderStore()
    await askWhereTheCoffeeIsMade(view)

    await goBackToTheCategories(view)
    await openCategory(view, 'Getränke')
    await view.vm.$nextTick()
    await view.vm.$nextTick()

    expect(document.querySelector('[data-test="line-station-sheet"]')).toBeNull()
    expect(order.draft.lines).toHaveLength(0)
  })

  it('adds nothing and stays in the category when the waiter cancels the question', async () => {
    const view = mountCatalogWithAStationChoice()
    const order = useOrderStore()
    await askWhereTheCoffeeIsMade(view)

    document.querySelector<HTMLElement>('[data-test="cancel-station-choice"]')?.click()
    await view.vm.$nextTick()

    expect(document.querySelector('[data-test="line-station-sheet"]')).toBeNull()
    expect(order.draft.lines).toHaveLength(0)
    expect(view.findAll('[data-test="item-row"]').length).toBeGreaterThan(0)
  })

  it('takes the note in the station sheet and puts both on the line', async () => {
    const view = mountCatalogWithAStationChoice()
    const order = useOrderStore()
    await openCategory(view, 'Essen')

    await itemRowNamed(view, 'Kaffee').get('[data-test="add-note"]').trigger('click')
    await vi.waitFor(() => expect(document.querySelector('[data-test="line-station-sheet"]')).not.toBeNull())
    expect(document.querySelector('[data-test="line-station-sheet"] [data-test="station-note-input"]')).not.toBeNull()
    expect(
      document.querySelector('[data-test="line-station-sheet"] [data-test="station-where-title"]')?.textContent?.trim(),
    ).toBe('Ausgabestelle für Kaffee')

    const field = document.querySelector(
      '[data-test="line-station-sheet"] [data-test="station-note-input"] input',
    ) as HTMLInputElement
    expect(document.activeElement).toBe(field)
    field.value = 'ohne Zucker'
    field.dispatchEvent(new Event('input'))
    await view.vm.$nextTick()
    stationChoiceNamed('Küche').click()
    await vi.waitFor(() => expect(document.querySelector('[data-test="line-station-sheet"]')).toBeNull())

    expect(order.draft.lines).toHaveLength(1)
    expect(order.draft.lines[0].note).toBe('ohne Zucker')
    expect(order.draft.lines[0].stationId).toBe('station-kueche')
  })

  it('adds another portion to the station of that group', async () => {
    const view = mountCatalogWithAStationChoice()
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

    await openCategory(view, 'Essen')
    await itemRowNamed(view, 'Kaffee').get('[data-test="group-add"]').trigger('click')

    expect(order.draft.lines).toHaveLength(3)
    expect(order.draft.lines[2].stationId).toBe('station-bar')
    expect(order.draft.lines[2].note).toBeNull()
  })

  it('keeps the note of a line while its station is changed', async () => {
    const view = mountCatalogWithAStationChoice()
    const order = useOrderStore()
    order.addItem({
      catalogItemId: 'item-kaffee',
      note: 'ohne Zucker',
      stationId: 'station-bar',
      name: 'Kaffee',
    })

    await openCategory(view, 'Essen')
    await itemRowNamed(view, 'Kaffee').get('[data-test="group-station"]').trigger('click')
    await vi.waitFor(() => expect(document.querySelector('[data-test="line-station-sheet"]')).not.toBeNull())

    expect(document.querySelector('[data-test="line-station-sheet"] [data-test="station-note-input"]')).toBeNull()

    stationChoiceNamed('Küche').click()
    await vi.waitFor(() => expect(document.querySelector('[data-test="line-station-sheet"]')).toBeNull())

    expect(order.draft.lines).toHaveLength(1)
    expect(order.draft.lines[0].note).toBe('ohne Zucker')
    expect(order.draft.lines[0].stationId).toBe('station-kueche')
  })

  it('lifts the station sheet above the keyboard while its note is typed', async () => {
    vi.stubGlobal('innerHeight', 800)
    vi.stubGlobal('visualViewport', {
      height: 400,
      scale: 1,
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
    })
    const view = mountCatalogWithAStationChoice()

    await askWhereTheCoffeeIsMade(view)

    const overlay = document.querySelector('[data-test="line-station-overlay"]') as HTMLElement
    expect(overlay.style.height).toBe('calc(100% - 400px)')
    expect(overlay.style.bottom).toBe('auto')
    expect(overlay.classList).toContain('v-dialog--scrollable')
  })
})

const CATALOG_WITH_TIMED_ITEMS: CatalogView = {
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

const TIMED_ESTIMATES: ItemEstimateView[] = [
  { catalogItemId: 'item-bratwurst', stationId: 'station-kueche', readyInMinutes: 10 },
  { catalogItemId: 'item-kaffee', stationId: 'station-kueche', readyInMinutes: 10 },
  { catalogItemId: 'item-kaffee', stationId: 'station-bar', readyInMinutes: 60 },
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
    useEstimatesStore().items = TIMED_ESTIMATES
    return mount(CatalogPage, { global: { plugins: testPlugins() }, attachTo: document.body })
  }

  it('writes one time on an item only one station makes', async () => {
    const view = mountCatalogWithEstimates()

    await openCategory(view, 'Essen')

    expect(view.findAll('[data-test="item-row"] [data-test="name"]').map((element) => element.text())).toEqual([
      'Bratwurst',
      'Kaffee',
    ])
    expect(view.findAll('[data-test="item-row"] [data-test="estimate"]').map((element) => element.text())).toEqual([
      '~10 Min.',
      '~10 - 60 Min.',
    ])
  })

  function answerEachQuoteWith(minutesAt: Record<string, number | null>): void {
    useSessionStore().deviceToken = 'token-here'
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string, init?: RequestInit) => {
        if (url !== '/api/estimates/quote') {
          return new Response('[]', { status: 200 })
        }
        const { lines } = JSON.parse(String(init?.body)) as {
          lines: { catalogItemId: string; stationId: string }[]
        }
        const stationId = lines.find((quoteLine) => quoteLine.catalogItemId === 'item-kaffee')?.stationId ?? ''
        return new Response(
          JSON.stringify({ stations: [{ stationId, readyInMinutes: minutesAt[stationId] ?? null }] }),
          { status: 200 },
        )
      }),
    )
  }

  async function askWhereTheKaffeeGoes(view: MountedCatalog): Promise<void> {
    await openCategory(view, 'Essen')
    await tapToAdd(view, 'Kaffee')
    await vi.waitFor(() => expect(document.querySelector('[data-test="line-station-sheet"]')).not.toBeNull())
  }

  function stationChoices(): (string | undefined)[] {
    return [...document.querySelectorAll('[data-test="station-choice"]')].map((element) =>
      element.textContent?.trim(),
    )
  }

  it('writes the time the laptop quoted for each station on its button in the station question', async () => {
    answerEachQuoteWith({ 'station-kueche': 25, 'station-bar': 70 })
    const view = mountCatalogWithEstimates()

    await askWhereTheKaffeeGoes(view)

    await vi.waitFor(() => expect(stationChoices()).toEqual(['Küche (~25 Min.)', 'Bar (~70 Min.)']))
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
    useCatalogStore().catalog = CATALOG
    const order = useOrderStore()
    order.addItem({
      catalogItemId: 'item-wasser',
      note: null,
      stationId: 'station-bar',
      name: 'Wasser',
    })
    order.setTable('Tisch 5')
    await order.send('leaveOpen')
    return order
  }

  it('sends the waiter to the summary, where the failure and the retry are', async () => {
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
              messageKey: 'errors.order.cannotBeProcessed',
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
    await order.send('leaveOpen')

    mountCatalog()

    expect(currentRoute.value).toEqual({ name: 'home' })
  })
})

describe('the ordering screen on a phone whose keyboard covers the lower screen', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
    navigate('/')
    vi.stubGlobal('fetch', vi.fn(async () => new Response('{}', { status: 200 })))
    vi.stubGlobal('innerHeight', 800)
    vi.stubGlobal('visualViewport', {
      height: 400,
      scale: 1,
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
    })
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('lifts the tray with the table field above the keyboard', async () => {
    const view = mountCatalog()
    await view.vm.$nextTick()

    expect((view.get('[data-test="catalog"]').element as HTMLElement).style.paddingBottom).toBe('400px')
  })

  it('leaves the tray where it is while the waiter is zoomed in', async () => {
    vi.stubGlobal('visualViewport', {
      height: 400,
      scale: 2,
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
    })
    const view = mountCatalog()
    await view.vm.$nextTick()

    expect((view.get('[data-test="catalog"]').element as HTMLElement).style.paddingBottom).toBe('0px')
  })
})
