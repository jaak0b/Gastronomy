import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import ItemsList from '../../../src/components/admin/items/ItemsList.vue'
import NewItemDialog from '../../../src/components/admin/items/NewItemDialog.vue'
import ItemForm from '../../../src/components/admin/items/ItemForm.vue'
import CategoryDialog from '../../../src/components/admin/categories/CategoryDialog.vue'
import { useAdminFestivalsStore } from '../../../src/stores/admin/festivals'
import { useAdminItemsStore } from '../../../src/stores/admin/items'
import { pressInDialog, testPlugins, waitForDialog } from '../../support/plugins'

const ITEM_ID = '22222222-2222-2222-2222-222222222222'
const STATION_ID = '11111111-1111-1111-1111-111111111111'
const FOOD_ID = '33333333-3333-3333-3333-333333333333'
const DRINKS_ID = '44444444-4444-4444-4444-444444444444'
const DESSERT_ID = '55555555-5555-5555-5555-555555555555'

const TWO_CATEGORIES = {
  categories: [
    {
      categoryId: FOOD_ID,
      name: 'Speisen',
      colourHex: '#FFEB3B',
      sortOrder: 1,
      isActive: true,
    },
    {
      categoryId: DRINKS_ID,
      name: 'Getränke',
      colourHex: '#C62828',
      sortOrder: 2,
      isActive: true,
    },
  ],
}

const CREATED_CATEGORY = {
  categoryId: DESSERT_ID,
  name: 'Nachtisch',
  colourHex: '#6D4C41',
  sortOrder: 3,
  isActive: true,
}

const FESTIVAL_ID = '66666666-6666-6666-6666-666666666666'

const ONE_FESTIVAL = {
  festivals: [
    {
      festivalId: FESTIVAL_ID,
      name: 'Sommerfest',
      startsAtUtc: '2026-07-18T10:00:00Z',
      endsAtUtc: '2026-07-19T02:00:00Z',
      isHidden: false,
      isRunning: true,
      stationCount: 1,
      menuItemCount: 1,
      orderCount: 0,
    },
  ],
}

const ONE_ITEM = {
  items: [
    {
      itemId: ITEM_ID,
      name: 'Bratwurst',
      categoryId: FOOD_ID,
      sortOrder: 1,
      isActive: true,
      productionMinutes: null,
      atTheFestival: { priceCents: 350, isAvailable: true, stationIds: [STATION_ID] },
    },
  ],
}

const ONE_STATION = {
  stations: [
    {
      stationId: STATION_ID,
      name: 'Küche',
      sortOrder: 1,
      isActive: true,
      hasDevice: true,
      isAtTheFestival: true,
    },
  ],
}

const SWITCHED_OFF_STATION_ID = '77777777-7777-7777-7777-777777777777'

const TWO_STATIONS = {
  stations: [
    ONE_STATION.stations[0],
    {
      stationId: SWITCHED_OFF_STATION_ID,
      name: 'Zelt',
      sortOrder: 2,
      isActive: false,
      hasDevice: false,
      isAtTheFestival: true,
    },
  ],
}

const DEACTIVATED_ITEM = {
  items: [{ ...ONE_ITEM.items[0], isActive: false }],
}

const THREE_ITEMS = {
  items: [
    { ...ONE_ITEM.items[0], itemId: 'aaaa1111-2222-4333-8444-555566667777', name: 'Wasser', categoryId: DRINKS_ID },
    { ...ONE_ITEM.items[0], itemId: 'bbbb1111-2222-4333-8444-555566667777', name: 'Schnitzel', categoryId: FOOD_ID },
    { ...ONE_ITEM.items[0], itemId: 'cccc1111-2222-4333-8444-555566667777', name: 'Bier', categoryId: DRINKS_ID },
  ],
}

interface Call {
  url: string
  method: string
  body: unknown
}

interface Laptop {
  items?: unknown
  categories?: unknown
  stations?: unknown
  refusal?: { status: number; body: unknown }
}

function stubLaptop(laptop: Laptop = {}): Call[] {
  const calls: Call[] = []
  vi.stubGlobal(
    'fetch',
    vi.fn(async (url: string, init?: RequestInit) => {
      const method = init?.method ?? 'GET'
      calls.push({
        url,
        method,
        body: init?.body === undefined ? null : JSON.parse(init.body as string),
      })
      if (url.includes('/api/admin/categories')) {
        if (method !== 'GET' && laptop.refusal !== undefined) {
          return new Response(JSON.stringify(laptop.refusal.body), {
            status: laptop.refusal.status,
          })
        }
        if (method === 'POST' && url === '/api/admin/categories') {
          return new Response(JSON.stringify(CREATED_CATEGORY), { status: 201 })
        }
        return new Response(JSON.stringify(laptop.categories ?? TWO_CATEGORIES), { status: 200 })
      }
      if (url.includes('/api/admin/festivals/')) {
        return new Response(JSON.stringify({}), { status: 200 })
      }
      if (url.includes('/api/admin/festivals')) {
        return new Response(JSON.stringify(ONE_FESTIVAL), { status: 200 })
      }
      if (url.includes('/api/admin/items')) {
        return new Response(JSON.stringify(laptop.items ?? ONE_ITEM), { status: 200 })
      }
      return new Response(JSON.stringify(laptop.stations ?? ONE_STATION), { status: 200 })
    }),
  )
  return calls
}

function urlsOf(calls: Call[]): string[] {
  return calls.map((call) => call.url)
}

function mountList() {
  useAdminFestivalsStore().pick(FESTIVAL_ID)
  return mount(ItemsList, { global: { plugins: testPlugins() }, attachTo: document.body })
}

describe('the item list', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('heads each category in the order the laptop gives, not in alphabetical order', async () => {
    stubLaptop({ items: THREE_ITEMS })

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.item-row').exists()).toBe(true))

    expect(list.findAll('.category-name').map((element) => element.text())).toEqual([
      'Speisen',
      'Getränke',
    ])
  })

  it('sorts the items inside a category by name', async () => {
    stubLaptop({ items: THREE_ITEMS })

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.item-row').exists()).toBe(true))

    expect(list.findAll('.item-row .name').map((element) => element.text())).toEqual([
      'Schnitzel',
      'Bier',
      'Wasser',
    ])
  })

  it('writes the category name on its colour, in lettering that stays readable', async () => {
    stubLaptop({ items: THREE_ITEMS })

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.item-row').exists()).toBe(true))

    const headings = list.findAll('.category-name')

    expect(headings[0].attributes('style')).toContain('background-color: rgb(255, 235, 59)')
    expect(headings[0].attributes('style')).toContain('color: rgb(0, 0, 0)')
    expect(headings[1].attributes('style')).toContain('color: rgb(255, 255, 255)')
  })

  it('keeps a category that holds no items, so it can still be renamed or moved', async () => {
    stubLaptop({ items: { items: [] } })

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.category-heading').exists()).toBe(true))

    expect(list.findAll('.category-name').map((element) => element.text())).toEqual([
      'Speisen',
      'Getränke',
    ])
  })

  it('keeps the name and the buttons of an item on one line', async () => {
    stubLaptop()

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.item-row').exists()).toBe(true))

    const line = list.get('.item-row .item-line')

    expect(line.find('.name').exists()).toBe(true)
    expect(line.find('.sold-out-toggle').exists()).toBe(true)
    expect(line.find('.edit').exists()).toBe(true)
    expect(line.find('.deactivate').exists()).toBe(true)
  })
})

describe('the controls beside a category name', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('offer renaming, both directions and switching off', async () => {
    stubLaptop()

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.category-heading').exists()).toBe(true))

    const heading = list.get('.category-heading')

    expect(heading.find('.rename-category').exists()).toBe(true)
    expect(heading.find('.move-category-up').exists()).toBe(true)
    expect(heading.find('.move-category-down').exists()).toBe(true)
    expect(heading.find('.deactivate-category').exists()).toBe(true)
  })

  it('move the category up at its own address', async () => {
    const calls = stubLaptop()

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.category-heading').exists()).toBe(true))
    await list.get('.move-category-up').trigger('click')

    await vi.waitFor(() =>
      expect(calls).toContainEqual({
        url: `/api/admin/categories/${FOOD_ID}/move`,
        method: 'POST',
        body: { direction: 'up' },
      }),
    )
  })

  it('move the category down at its own address', async () => {
    const calls = stubLaptop()

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.category-heading').exists()).toBe(true))
    await list.get('.move-category-down').trigger('click')

    await vi.waitFor(() =>
      expect(calls).toContainEqual({
        url: `/api/admin/categories/${FOOD_ID}/move`,
        method: 'POST',
        body: { direction: 'down' },
      }),
    )
  })

  it('send the new name and colour when the category is renamed', async () => {
    const calls = stubLaptop()

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.category-heading').exists()).toBe(true))
    await list.get('.rename-category').trigger('click')

    await list
      .findComponent(CategoryDialog)
      .vm.$emit('save', { name: 'Kaffee', colourHex: '#6D4C41' })

    await vi.waitFor(() =>
      expect(calls).toContainEqual({
        url: `/api/admin/categories/${FOOD_ID}`,
        method: 'PUT',
        body: { name: 'Kaffee', colourHex: '#6D4C41' },
      }),
    )
  })

  it('ask before the category is switched off', async () => {
    const calls = stubLaptop()

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.category-heading').exists()).toBe(true))
    await list.get('.deactivate-category').trigger('click')

    await waitForDialog()

    expect(urlsOf(calls).some((url) => url.endsWith('/deactivate'))).toBe(false)
  })

  it('switch the category off once the question is answered with yes', async () => {
    const calls = stubLaptop()

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.category-heading').exists()).toBe(true))
    await list.get('.deactivate-category').trigger('click')
    await pressInDialog('.confirm')

    await vi.waitFor(() =>
      expect(urlsOf(calls)).toContain(`/api/admin/categories/${FOOD_ID}/deactivate`),
    )
  })

  it('say why the laptop kept the category switched on', async () => {
    stubLaptop({
      refusal: {
        status: 409,
        body: {
          code: 'Conflict',
          messageKey: 'admin.categoryHasActiveItems',
          parameters: {},
          details: null,
        },
      },
    })

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.category-heading').exists()).toBe(true))
    await list.get('.deactivate-category').trigger('click')
    await pressInDialog('.confirm')

    await vi.waitFor(() =>
      expect(list.get('.admin-items .refusal').text()).toContain(
        'Diese Kategorie hat noch eingeschaltete Artikel.',
      ),
    )
  })
})

describe('a category that is switched off', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('stays on the page and says it is switched off, so moving stays predictable', async () => {
    stubLaptop({
      categories: { categories: [{ ...TWO_CATEGORIES.categories[0], isActive: false }] },
      items: { items: [] },
    })

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.category-heading').exists()).toBe(true))

    expect(list.get('.category-name').text()).toBe('Speisen')
    expect(list.get('.category-heading .deactivated').text()).toBe('Deaktiviert')
  })

  it('offers to switch it on again without asking a question first', async () => {
    const calls = stubLaptop({
      categories: { categories: [{ ...TWO_CATEGORIES.categories[0], isActive: false }] },
      items: { items: [] },
    })

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.category-heading').exists()).toBe(true))
    await list.get('.activate-category').trigger('click')

    await vi.waitFor(() =>
      expect(urlsOf(calls)).toContain(`/api/admin/categories/${FOOD_ID}/activate`),
    )
  })
})

describe('adding a category from the item list', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('asks for the category in a dialog', async () => {
    stubLaptop()

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.new-category').exists()).toBe(true))

    expect(document.querySelector('.category-dialog')).toBeNull()

    await list.get('.new-category').trigger('click')

    await vi.waitFor(() => expect(document.querySelector('.category-dialog')).not.toBeNull())
  })

  it('sends the name and the colour that were entered', async () => {
    const calls = stubLaptop()

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.new-category').exists()).toBe(true))
    await list.get('.new-category').trigger('click')
    await list
      .findComponent(CategoryDialog)
      .vm.$emit('save', { name: 'Nachtisch', colourHex: '#6D4C41' })

    await vi.waitFor(() =>
      expect(calls).toContainEqual({
        url: '/api/admin/categories',
        method: 'POST',
        body: { name: 'Nachtisch', colourHex: '#6D4C41' },
      }),
    )
  })

  it('closes the dialog once the category is saved', async () => {
    stubLaptop()

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.new-category').exists()).toBe(true))
    await list.get('.new-category').trigger('click')
    await list
      .findComponent(CategoryDialog)
      .vm.$emit('save', { name: 'Nachtisch', colourHex: '#6D4C41' })

    await vi.waitFor(() => expect(document.querySelector('.category-dialog')).toBeNull())
  })

  it('keeps the dialog open and says why when the laptop refuses the name', async () => {
    stubLaptop({
      refusal: {
        status: 409,
        body: {
          code: 'Conflict',
          messageKey: 'admin.categoryNameTaken',
          parameters: {},
          details: null,
        },
      },
    })

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.new-category').exists()).toBe(true))
    await list.get('.new-category').trigger('click')
    await list
      .findComponent(CategoryDialog)
      .vm.$emit('save', { name: 'Speisen', colourHex: '#6D4C41' })

    await vi.waitFor(() =>
      expect(list.findComponent(CategoryDialog).props('errorText')).toBe(
        'Es gibt schon eine Kategorie mit diesem Namen. Wählen Sie einen anderen.',
      ),
    )
  })
})

describe('deactivating an item', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('asks before it happens', async () => {
    const calls = stubLaptop()

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.item-row').exists()).toBe(true))
    await list.get('.deactivate').trigger('click')

    await waitForDialog()

    expect(urlsOf(calls).some((url) => url.endsWith('/deactivate'))).toBe(false)
  })

  it('says that the orders already placed are kept', async () => {
    stubLaptop()

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.item-row').exists()).toBe(true))
    await list.get('.deactivate').trigger('click')

    await waitForDialog()

    expect(document.querySelector('.confirm-body')!.textContent).toContain('bleiben gespeichert')
  })

  it('deactivates it once the question is answered with yes', async () => {
    const calls = stubLaptop()

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.item-row').exists()).toBe(true))
    await list.get('.deactivate').trigger('click')
    await pressInDialog('.confirm')

    await vi.waitFor(() => expect(urlsOf(calls)).toContain(`/api/admin/items/${ITEM_ID}/deactivate`))
  })
})

describe('an item that is deactivated', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('is left out of the list until the admin asks to see deactivated entries', async () => {
    stubLaptop({ items: DEACTIVATED_ITEM })

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.show-deactivated').exists()).toBe(true))

    expect(list.find('.item-row').exists()).toBe(false)
  })

  it('offers no sold-out toggle, because nobody can order it', async () => {
    stubLaptop({ items: DEACTIVATED_ITEM })

    const list = mountList()
    await list.get('.show-deactivated input').setValue(true)
    await vi.waitFor(() => expect(list.find('.item-row').exists()).toBe(true))

    expect(list.find('.sold-out-toggle').exists()).toBe(false)
  })

  it('offers to activate it again without asking a question first', async () => {
    const calls = stubLaptop({ items: DEACTIVATED_ITEM })

    const list = mountList()
    await list.get('.show-deactivated input').setValue(true)
    await vi.waitFor(() => expect(list.find('.item-row').exists()).toBe(true))
    await list.get('.reactivate').trigger('click')

    await vi.waitFor(() => expect(urlsOf(calls)).toContain(`/api/admin/items/${ITEM_ID}/activate`))
  })
})

describe('the sold-out button beside an item', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('marks that item sold out at this festival alone', async () => {
    const calls = stubLaptop()

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.item-row').exists()).toBe(true))
    await list.get('.sold-out-toggle').trigger('click')

    await vi.waitFor(() =>
      expect(urlsOf(calls)).toContain(
        `/api/admin/festivals/${FESTIVAL_ID}/items/${ITEM_ID}/availability`,
      ),
    )
  })
})

describe('adding an item', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('asks for the item in a dialog instead of on the page', async () => {
    stubLaptop()

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.new-item').exists()).toBe(true))

    expect(document.querySelector('.new-item-dialog')).toBeNull()

    await list.get('.new-item').trigger('click')

    await vi.waitFor(() => expect(document.querySelector('.new-item-dialog')).not.toBeNull())
  })

  it('drops the refusal to switch a category off once the admin writes a new item', async () => {
    stubLaptop({
      refusal: {
        status: 409,
        body: {
          code: 'Conflict',
          messageKey: 'admin.categoryHasActiveItems',
          parameters: {},
          details: null,
        },
      },
    })

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.category-heading').exists()).toBe(true))
    await list.get('.deactivate-category').trigger('click')
    await pressInDialog('.confirm')
    await vi.waitFor(() => expect(list.find('.admin-items .refusal').exists()).toBe(true))

    await list.get('.new-item').trigger('click')
    await vi.waitFor(() => expect(document.querySelector('.new-item-dialog')).not.toBeNull())
    await list.findComponent(NewItemDialog).vm.$emit('cancel')
    await vi.waitFor(() => expect(document.querySelector('.new-item-dialog')).toBeNull())

    expect(list.find('.admin-items .refusal').exists()).toBe(false)
  })
})

describe('an item that is not on this festival menu', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('stands under its own heading, away from the menu', async () => {
    stubLaptop({ items: { items: [{ ...ONE_ITEM.items[0], atTheFestival: null }] } })

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.item-row').exists()).toBe(true))

    expect(list.get('.not-on-the-menu-heading').text()).toBe('Nicht auf der Karte')
    expect(list.findAll('.item-row.rest')).toHaveLength(1)
  })

  it('sends nothing while the admin is still filling the dialog in', async () => {
    const calls = stubLaptop({
      items: { items: [{ ...ONE_ITEM.items[0], atTheFestival: null }] },
    })

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.put-on-the-menu').exists()).toBe(true))
    await list.get('.put-on-the-menu').trigger('click')
    await vi.waitFor(() =>
      expect(document.querySelector('.menu-item-dialog')).not.toBeNull(),
    )

    expect(calls.every((call) => call.method === 'GET')).toBe(true)
  })

  it('goes on the menu with its price and its station once the dialog is confirmed', async () => {
    const calls = stubLaptop({
      items: { items: [{ ...ONE_ITEM.items[0], atTheFestival: null }] },
    })

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.put-on-the-menu').exists()).toBe(true))
    await list.get('.put-on-the-menu').trigger('click')
    await vi.waitFor(() =>
      expect(document.querySelector('.menu-item-dialog')).not.toBeNull(),
    )
    const dialog = document.querySelector('.menu-item-dialog') as HTMLElement
    const price = dialog.querySelector('.price-field input') as HTMLInputElement
    price.value = '3,50'
    price.dispatchEvent(new Event('input'))
    ;(dialog.querySelector('.station-chip') as HTMLElement).click()
    await vi.waitFor(() =>
      expect(dialog.querySelector('.station-chip.is-selected')).not.toBeNull(),
    )
    ;(dialog.querySelector('.confirm') as HTMLElement).click()

    await vi.waitFor(() => {
      const sent = calls.find((call) => call.method === 'PUT')
      expect(sent?.url).toBe(`/api/admin/festivals/${FESTIVAL_ID}/items/${ITEM_ID}`)
      expect(sent?.body).toEqual({ priceCents: 350, stationIds: [STATION_ID] })
    })
  })
})

describe('an item on this festival menu', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('shows its price and the stations that prepare it here', async () => {
    stubLaptop()

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.item-row').exists()).toBe(true))

    expect(list.get('.item-row .price').text()).toBe('3,50 €')
    expect(list.get('.item-row .station-chip.is-selected').text()).toBe('Küche')
  })

  it('offers only the stations that are switched on', async () => {
    stubLaptop({ stations: TWO_STATIONS })

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.item-row .station-chip').exists()).toBe(true))

    expect(list.findAll('.item-row .station-chip').map((chip) => chip.text())).toEqual(['Küche'])
  })

  it('comes off this festival menu alone', async () => {
    const calls = stubLaptop()

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.take-off-the-menu').exists()).toBe(true))
    await list.get('.take-off-the-menu').trigger('click')

    await vi.waitFor(() =>
      expect(
        calls.find((call) => call.method === 'DELETE')?.url,
      ).toBe(`/api/admin/festivals/${FESTIVAL_ID}/items/${ITEM_ID}`),
    )
  })
})

describe('the item page while no festival is open', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('says where the prices and the stations live, and offers no menu control', async () => {
    stubLaptop({ items: { items: [{ ...ONE_ITEM.items[0], atTheFestival: null }] } })

    const list = mount(ItemsList, {
      global: { plugins: testPlugins() },
      attachTo: document.body,
    })
    await vi.waitFor(() => expect(list.find('.item-row').exists()).toBe(true))

    expect(list.get('.no-festival-picked').text()).toBe(
      'Preise und Ausgabestellen gehören zu einem Fest. Öffnen Sie ein Fest unter "Feste".',
    )
    expect(list.find('.put-on-the-menu').exists()).toBe(false)
  })
})
