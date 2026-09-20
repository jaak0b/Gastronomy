import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import ItemsList from '../../../src/components/admin/items/ItemsList.vue'
import ItemDialog from '../../../src/components/admin/items/ItemDialog.vue'
import CategoryDialog from '../../../src/components/admin/categories/CategoryDialog.vue'
import FestivalItems from '../../../src/components/admin/festivals/FestivalItems.vue'
import { useAdminFestivalsStore } from '../../../src/stores/admin/festivals'
import { useAdminStationsStore } from '../../../src/stores/admin/stations'
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
      isRunning: false,
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
      isQueueIndependent: false,
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
  festivals?: unknown
  refusal?: { status: number; body: unknown }
  itemRefusal?: { status: number; body: unknown }
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
        return new Response(JSON.stringify(laptop.festivals ?? ONE_FESTIVAL), { status: 200 })
      }
      if (url.includes('/api/admin/items')) {
        if (method !== 'GET' && laptop.itemRefusal !== undefined) {
          return new Response(JSON.stringify(laptop.itemRefusal.body), {
            status: laptop.itemRefusal.status,
          })
        }
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
    expect(line.find('.edit').exists()).toBe(true)
    expect(line.find('.deactivate').exists()).toBe(true)
  })

  it('says the list could not be loaded when the festivals request fails', async () => {
    stubLaptop({ festivals: { notTheFestivals: [] } })

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.admin-items .error').exists()).toBe(true))

    expect(list.get('.admin-items .error').text()).toContain(
      'Laden Sie die Seite neu. Die Daten konnten nicht geladen werden.',
    )
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

    expect(document.querySelector('.form-dialog')).toBeNull()

    await list.get('.new-category').trigger('click')

    await vi.waitFor(() => expect(document.querySelector('.form-dialog')).not.toBeNull())
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

    await vi.waitFor(() => expect(document.querySelector('.form-dialog')).toBeNull())
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

  it('deactivates it once the question is answered with yes', async () => {
    const calls = stubLaptop()

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.item-row').exists()).toBe(true))
    await list.get('.deactivate').trigger('click')
    await pressInDialog('.confirm')

    await vi.waitFor(() => expect(urlsOf(calls)).toContain(`/api/admin/items/${ITEM_ID}/deactivate`))
  })

  it('keeps the global deactivate disabled while the item is on the running festival', async () => {
    const calls = stubLaptop({
      festivals: { festivals: [{ ...ONE_FESTIVAL.festivals[0], isRunning: true }] },
      items: {
        items: [
          { ...ONE_ITEM.items[0] },
          {
            ...ONE_ITEM.items[0],
            itemId: 'aaaa1111-2222-4333-8444-555566667777',
            name: 'Wasser',
            atTheFestival: null,
          },
        ],
      },
    })

    const list = mountList()
    await vi.waitFor(() => expect(list.findAll('.deactivate').length).toBe(2))

    const buttons = list.findAll('.deactivate')
    const onTheMenu = buttons[0].element as HTMLButtonElement
    const elsewhere = buttons[1].element as HTMLButtonElement
    expect(onTheMenu.disabled).toBe(true)
    expect(elsewhere.disabled).toBe(false)

    const wrapper = list.findAll('.deactivate-wrapper')[0]
    const tooltip = wrapper.findComponent({ name: 'VTooltip' })
    expect(tooltip.exists()).toBe(true)
    expect(tooltip.props('disabled')).toBe(false)

    await wrapper.trigger('mouseenter')
    await vi.waitFor(() => expect(document.querySelector('.v-overlay--active')).not.toBeNull())
    expect(document.querySelector('.v-overlay__content')?.textContent).toContain(
      'Ein Artikel auf der Karte eines aktiven Festes kann nicht abgeschaltet werden.',
    )

    onTheMenu.click()
    await list.vm.$nextTick()

    expect(document.querySelector('.confirm-dialog')).toBeNull()
    expect(urlsOf(calls).some((url) => url.endsWith('/deactivate'))).toBe(false)
  })
})

describe('the item list while the running festival changes', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('reads the item list of the festival that is now running', async () => {
    const laptop: Laptop = {
      festivals: { festivals: [{ ...ONE_FESTIVAL.festivals[0], isRunning: true }] },
    }
    const calls = stubLaptop(laptop)

    mountList()
    await vi.waitFor(() =>
      expect(urlsOf(calls)).toContain(`/api/admin/items?festivalId=${FESTIVAL_ID}`),
    )

    const SECOND_FESTIVAL_ID = '77777777-7777-4777-8777-777777777777'
    laptop.festivals = {
      festivals: [
        { ...ONE_FESTIVAL.festivals[0], isRunning: false },
        {
          ...ONE_FESTIVAL.festivals[0],
          festivalId: SECOND_FESTIVAL_ID,
          name: 'Herbstfest',
          isRunning: true,
        },
      ],
    }
    calls.length = 0

    await useAdminFestivalsStore().load()

    await vi.waitFor(() =>
      expect(urlsOf(calls)).toContain(`/api/admin/items?festivalId=${SECOND_FESTIVAL_ID}`),
    )
  })

  it('reads the whole item list again when no festival is running', async () => {
    const laptop: Laptop = {
      festivals: { festivals: [{ ...ONE_FESTIVAL.festivals[0], isRunning: true }] },
    }
    const calls = stubLaptop(laptop)

    mountList()
    await vi.waitFor(() =>
      expect(urlsOf(calls)).toContain(`/api/admin/items?festivalId=${FESTIVAL_ID}`),
    )

    laptop.festivals = { festivals: [{ ...ONE_FESTIVAL.festivals[0], isRunning: false }] }
    calls.length = 0

    await useAdminFestivalsStore().load()

    await vi.waitFor(() => expect(urlsOf(calls)).toContain('/api/admin/items'))
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

  it('offers to activate it again without asking a question first', async () => {
    const calls = stubLaptop({ items: DEACTIVATED_ITEM })

    const list = mountList()
    await list.get('.show-deactivated input').setValue(true)
    await vi.waitFor(() => expect(list.find('.item-row').exists()).toBe(true))
    await list.get('.reactivate').trigger('click')

    await vi.waitFor(() => expect(urlsOf(calls)).toContain(`/api/admin/items/${ITEM_ID}/activate`))
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

    expect(document.querySelector('.form-dialog')).toBeNull()

    await list.get('.new-item').trigger('click')

    await vi.waitFor(() => expect(document.querySelector('.form-dialog')).not.toBeNull())
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
    await vi.waitFor(() => expect(document.querySelector('.form-dialog')).not.toBeNull())
    await list.findComponent(ItemDialog).vm.$emit('cancel')
    await vi.waitFor(() => expect(document.querySelector('.form-dialog')).toBeNull())

    expect(list.find('.admin-items .refusal').exists()).toBe(false)
  })
})

describe('a refusal beside an open item form', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('shows why the laptop kept the category while the item is being edited', async () => {
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
    await vi.waitFor(() => expect(list.find('.item-row').exists()).toBe(true))
    await list.get('.edit').trigger('click')
    await vi.waitFor(() => expect(document.querySelector('.item-name-field')).not.toBeNull())

    await list.get('.move-category-up').trigger('click')

    await vi.waitFor(() =>
      expect(list.get('.admin-items .refusal').text()).toContain(
        'Diese Kategorie hat noch eingeschaltete Artikel.',
      ),
    )
  })

  it('shows why the laptop refused an item action while no form or dialog is open', async () => {
    stubLaptop({
      itemRefusal: {
        status: 409,
        body: {
          code: 'Conflict',
          messageKey: 'admin.actionFailed',
          parameters: {},
          details: null,
        },
      },
    })

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.item-row').exists()).toBe(true))
    await list.get('.deactivate').trigger('click')
    await pressInDialog('.confirm')

    await vi.waitFor(() =>
      expect(list.get('.admin-items .refusal').text()).toContain('Die Aktion ist fehlgeschlagen.'),
    )
  })

  it('leaves an item refusal to the open item form rather than the page', async () => {
    stubLaptop({
      itemRefusal: {
        status: 409,
        body: {
          code: 'Conflict',
          messageKey: 'admin.actionFailed',
          parameters: {},
          details: null,
        },
      },
    })

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.item-row').exists()).toBe(true))
    await list.get('.edit').trigger('click')
    await vi.waitFor(() => expect(document.querySelector('.item-name-field')).not.toBeNull())

    await list.findComponent(ItemDialog).vm.$emit('save', {
      itemId: ITEM_ID,
      name: 'Bratwurst',
      categoryId: FOOD_ID,
      sortOrder: 1,
      productionMinutes: null,
    })

    await vi.waitFor(() =>
      expect(list.findComponent(ItemDialog).props('errorText')).toContain('Die Aktion ist fehlgeschlagen.'),
    )
    expect(list.find('.admin-items .refusal').exists()).toBe(false)
  })
})

describe('a refusal the admin has walked away from', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  function stubTheLaptop(): void {
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string, init?: RequestInit) => {
        const method = init?.method ?? 'GET'
        if (method !== 'GET') {
          return new Response(
            JSON.stringify({
              code: 'Conflict',
              messageKey: 'admin.actionFailed',
              parameters: {},
              details: null,
            }),
            { status: 409 },
          )
        }
        if (url.startsWith('/api/admin/categories')) {
          return new Response(JSON.stringify(TWO_CATEGORIES), { status: 200 })
        }
        if (url.startsWith('/api/admin/stations')) {
          return new Response(JSON.stringify(ONE_STATION), { status: 200 })
        }
        return new Response(JSON.stringify(ONE_ITEM), { status: 200 })
      }),
    )
  }

  function mountFestivalItems() {
    return mount(FestivalItems, {
      props: { festivalId: FESTIVAL_ID, isRunning: false },
      global: { plugins: testPlugins() },
      attachTo: document.body,
    })
  }

  it('does not follow the admin from the item list to a festival', async () => {
    stubTheLaptop()

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.item-row').exists()).toBe(true))
    await list.get('.deactivate').trigger('click')
    await pressInDialog('.confirm')
    await vi.waitFor(() => expect(list.find('.admin-items .refusal').exists()).toBe(true))
    list.unmount()

    await useAdminStationsStore().load()
    const festival = mountFestivalItems()
    await vi.waitFor(() => expect(festival.find('.festival-item-row').exists()).toBe(true))

    expect(festival.find('.festival-items .refusal').exists()).toBe(false)
  })

  it('does not follow the admin from a festival to the item list', async () => {
    stubTheLaptop()

    await useAdminStationsStore().load()
    const festival = mountFestivalItems()
    await vi.waitFor(() => expect(festival.find('.new-item').exists()).toBe(true))
    await festival.get('.new-item').trigger('click')
    await vi.waitFor(() => expect(festival.findComponent({ name: 'ItemDialog' }).exists()).toBe(true))
    festival.findComponent({ name: 'ItemDialog' }).vm.$emit('save', {
      name: 'Pommes',
      categoryId: FOOD_ID,
      sortOrder: 1,
      productionMinutes: null,
    })
    await vi.waitFor(() =>
      expect(festival.findComponent({ name: 'ItemDialog' }).props('errorText')).not.toBeNull(),
    )
    festival.unmount()

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.item-row').exists()).toBe(true))

    expect(list.find('.admin-items .refusal').exists()).toBe(false)
  })
})
