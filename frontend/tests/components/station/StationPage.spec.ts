import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { defineComponent } from 'vue'
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import StationPage from '../../../src/views/StationPage.vue'
import { bindLocaleToSession } from '../../../src/localeBinding'
import { useSessionStore } from '../../../src/stores/session'
import { testPlugins } from '../../support/plugins'

const KITCHEN = { id: 'station-kueche', name: 'Küche' }

const TOGETHER_SLICE = {
  stationOrderId: 'slice-1',
  globalOrderNumber: 137,
  stationOrderNumber: 12,
  tableName: '3',
  note: 'Bitte zusammen bringen',
  deliveryMode: 'together',
  createdAtUtc: '2026-09-05T18:00:00Z',
  isHiddenFromAsItComesQueue: false,
  itemCount: 3,
  fulfilledItemCount: 1,
  items: [
    { orderItemId: 'a', itemName: 'Bratwurst', note: 'ohne Senf', fulfilledAtUtc: null },
    { orderItemId: 'b', itemName: 'Pommes', note: null, fulfilledAtUtc: null },
    {
      orderItemId: 'c',
      itemName: 'Bratwurst',
      note: null,
      fulfilledAtUtc: '2026-09-05T18:10:00Z',
    },
  ],
}

const AS_IT_COMES_SLICE = {
  stationOrderId: 'slice-2',
  globalOrderNumber: 138,
  stationOrderNumber: 14,
  tableName: '7',
  note: null,
  deliveryMode: 'asItComes',
  createdAtUtc: '2026-09-05T18:05:00Z',
  isHiddenFromAsItComesQueue: false,
  itemCount: 2,
  fulfilledItemCount: 0,
  items: [
    { orderItemId: 'd', itemName: 'Bier', note: null, fulfilledAtUtc: null },
    { orderItemId: 'e', itemName: 'Bratwurst', note: null, fulfilledAtUtc: null },
  ],
}

const NOTED_SLICE = {
  stationOrderId: 'slice-3',
  globalOrderNumber: 140,
  stationOrderNumber: 16,
  tableName: '5',
  note: null,
  deliveryMode: 'together',
  createdAtUtc: '2026-09-05T18:07:00Z',
  isHiddenFromAsItComesQueue: false,
  itemCount: 2,
  fulfilledItemCount: 0,
  items: [
    { orderItemId: 'f', itemName: 'Frankfurter', note: 'Mit Ketchup', fulfilledAtUtc: null },
    { orderItemId: 'g', itemName: 'Frankfurter', note: 'Ohne Ketchup', fulfilledAtUtc: null },
  ],
}

const DONE_SLICE = {
  ...TOGETHER_SLICE,
  itemCount: 3,
  fulfilledItemCount: 2,
  items: [
    { orderItemId: 'a', itemName: 'Bratwurst', note: null, fulfilledAtUtc: '2026-09-05T19:00:00Z' },
    { orderItemId: 'b', itemName: 'Pommes', note: null, fulfilledAtUtc: null },
    { orderItemId: 'c', itemName: 'Bratwurst', note: null, fulfilledAtUtc: '2026-09-05T18:10:00Z' },
  ],
}

const DONE_NOTED_SLICE = {
  ...NOTED_SLICE,
  itemCount: 2,
  fulfilledItemCount: 2,
  items: [
    {
      orderItemId: 'f',
      itemName: 'Frankfurter',
      note: 'Mit Ketchup',
      fulfilledAtUtc: '2026-09-05T19:00:00Z',
    },
    {
      orderItemId: 'g',
      itemName: 'Frankfurter',
      note: 'Ohne Ketchup',
      fulfilledAtUtc: '2026-09-05T19:01:00Z',
    },
  ],
}

function ok(body: unknown): Response {
  return new Response(JSON.stringify(body), { status: 200 })
}

function refused(code: string, messageKey: string): Response {
  return new Response(JSON.stringify({ code, messageKey, parameters: {}, details: null }), {
    status: 409,
  })
}

function queue(orders: unknown[], asItComes: unknown[] = []): Response {
  return ok({ station: KITCHEN, orders, asItComes })
}

function queueWithBoard(): Response {
  return queue([TOGETHER_SLICE, AS_IT_COMES_SLICE], [AS_IT_COMES_SLICE])
}

interface StubRoutes {
  orders?: () => Response
  fulfilled?: () => Response
  fulfill?: () => Response
  unfulfill?: () => Response
  hide?: () => Response
}

function stubTheLaptop(routes: StubRoutes = {}): { url: string; body: unknown }[] {
  const posts: { url: string; body: unknown }[] = []
  vi.stubGlobal(
    'fetch',
    vi.fn(async (url: string, options?: RequestInit) => {
      if (options?.method === 'POST') {
        posts.push({ url, body: JSON.parse(String(options?.body ?? 'null')) })
      }
      if (url === '/api/station/orders') {
        return (routes.orders ?? queueWithBoard)()
      }
      if (url === '/api/station/orders/fulfilled') {
        return (routes.fulfilled ?? (() => ok({ slices: [] })))()
      }
      if (url === '/api/station/items/fulfill') {
        return (routes.fulfill ?? (() => queue([])))()
      }
      if (url === '/api/station/items/unfulfill') {
        return (routes.unfulfill ?? (() => queue([])))()
      }
      if (url.endsWith('/hide')) {
        return (routes.hide ?? (() => queue([])))()
      }
      throw new TypeError(`the stub has no answer for ${url}`)
    }),
  )
  return posts
}

async function mountPage(): Promise<VueWrapper> {
  const session = useSessionStore()
  session.deviceToken = 'token-here'
  session.deviceKind = 'station'
  const page = mount(StationPage, {
    global: { plugins: testPlugins() },
    attachTo: document.body,
  })
  await flushPromises()
  return page
}

const StationPageFollowingTheLanguage = defineComponent({
  components: { StationPage },
  setup() {
    bindLocaleToSession()
  },
  template: '<StationPage />',
})

async function mountPageFollowingTheChosenLanguage(): Promise<VueWrapper> {
  localStorage.setItem('language', 'de')
  const session = useSessionStore()
  session.deviceToken = 'token-here'
  session.deviceKind = 'station'
  const page = mount(StationPageFollowingTheLanguage, {
    global: { plugins: testPlugins() },
    attachTo: document.body,
  })
  await flushPromises()
  return page
}

function textsOf(selector: string): string[] {
  return [...document.querySelectorAll(selector)].map((element) => element.textContent?.trim() ?? '')
}

describe('the screen at a station', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
    stubTheLaptop()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('names the station in its heading, so nobody works the wrong pile', async () => {
    const page = await mountPage()

    expect(page.get('.station-name').text()).toBe('Küche')
  })

  it('writes the page in the language the header picker chooses', async () => {
    const page = await mountPageFollowingTheChosenLanguage()

    expect(page.get('.orders-heading').text()).toBe('Alle Bestellungen')

    await page.get('.language-switch .v-field').trigger('mousedown')
    await vi.waitFor(() => expect(document.querySelector('.option-en')).not.toBeNull())
    ;(document.querySelector('.option-en') as HTMLElement).click()
    await flushPromises()

    expect(page.get('.orders-heading').text()).toBe('All orders')
  })

  it('puts every order in the left column and only the as-it-comes ones in the right', async () => {
    const page = await mountPage()

    expect(page.findAll('.orders-column .station-slice')).toHaveLength(2)
    expect(page.findAll('.as-it-comes-column .station-slice')).toHaveLength(1)
    expect(page.get('.as-it-comes-column .table-name').text()).toBe('Tisch 7')
  })

  it('shows the order number and the number of this station on a card', async () => {
    const page = await mountPage()

    expect(page.findAll('.orders-column .slice-heading')[0].text()).toBe(
      'Bestellung 137 · Nr. 12',
    )
  })

  it('shows the table prominently, because that is what goes on the tray', async () => {
    const page = await mountPage()

    expect(page.findAll('.orders-column .table-name')[0].text()).toBe('Tisch 3')
  })

  it('names the delivery mode in words and marks the card by mode', async () => {
    const page = await mountPage()

    expect(page.findAll('.orders-column .delivery-mode').map((entry) => entry.text())).toEqual([
      'Gemeinsam',
      'Einzeln',
    ])
    expect(page.findAll('.orders-column .station-slice')[0].attributes('style')).toContain(
      'var(--v-theme-together)',
    )
    expect(page.findAll('.orders-column .station-slice')[1].attributes('style')).toContain(
      'var(--v-theme-individual)',
    )
  })

  it('shows the note that belongs to the whole order', async () => {
    const page = await mountPage()

    expect(page.findAll('.orders-column .slice-note')[0].text()).toBe(
      'Hinweis zur Bestellung: Bitte zusammen bringen',
    )
  })

  it('counts what is done on the card', async () => {
    const page = await mountPage()

    expect(page.findAll('.orders-column .done-counter')[0].text()).toBe('1 / 3 erledigt')
    expect(page.findAll('.orders-column .done-counter')[1].text()).toBe('0 / 2 erledigt')
  })

  it('keeps a done item off the card and shows every open one with its note', async () => {
    const page = await mountPage()

    const items = page.findAll('.orders-column .station-slice')[0].findAll('.station-item')

    expect(items).toHaveLength(2)
    expect(items[0].get('.item-name').text()).toBe('Bratwurst')
    expect(items[0].get('.item-note').text()).toBe('Hinweis: ohne Senf')
    expect(items[0].find('.item-note-icon').exists()).toBe(true)
    expect(items[1].get('.item-name').text()).toBe('Pommes')
    expect(items[1].find('.item-note').exists()).toBe(false)
  })
})

describe('switching a card between the list and the grouped view', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('groups the open items by name and note and brings the list back', async () => {
    stubTheLaptop({ orders: () => queue([NOTED_SLICE]) })
    const page = await mountPage()
    const card = page.findAll('.orders-column .station-slice')[0]

    expect(card.get('.grouped-toggle').text()).toBe('Gruppiert')

    await card.get('.grouped-toggle').trigger('click')

    expect(card.get('.grouped-toggle').text()).toBe('Liste')
    expect(card.findAll('.grouped-line').map((line) => line.text())).toEqual([
      '1 x Frankfurter · Hinweis: Mit Ketchup',
      '1 x Frankfurter · Hinweis: Ohne Ketchup',
    ])
    expect(card.findAll('.station-item')).toHaveLength(0)
    expect(card.find('.card-actions').exists()).toBe(false)

    await card.get('.grouped-toggle').trigger('click')

    expect(card.get('.grouped-toggle').text()).toBe('Gruppiert')
    expect(card.findAll('.station-item')).toHaveLength(2)
    expect(card.find('.card-actions').exists()).toBe(true)
  })
})

describe('the overview board on the station screen', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
    stubTheLaptop()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('opens from the header with every open article and closes again', async () => {
    const page = await mountPage()

    await page.get('.show-overview').trigger('click')
    await flushPromises()

    expect(textsOf('.station-open-board .stat-together')).toEqual(['Gemeinsam 1'])
    expect(textsOf('.station-open-board .stat-as-it-comes')).toEqual(['Einzeln 1'])
    expect(textsOf('.station-open-board .unit')).toEqual([
      '1 x Bier',
      '1 x Bratwurst',
      '1 x Bratwurst · Hinweis: ohne Senf',
      '1 x Pommes',
    ])

    ;(document.querySelector('.station-open-board .back-to-orders') as HTMLElement).click()
    await flushPromises()

    expect(document.querySelector('.station-open-board')).toBeNull()
    expect(page.findAll('.orders-column .station-slice')).toHaveLength(2)
  })

  it('shows two rows when two of the same article carry different notes', async () => {
    stubTheLaptop({ orders: () => queue([NOTED_SLICE]) })
    const page = await mountPage()

    await page.get('.show-overview').trigger('click')
    await flushPromises()

    expect(textsOf('.station-open-board .unit')).toEqual([
      '1 x Frankfurter · Hinweis: Mit Ketchup',
      '1 x Frankfurter · Hinweis: Ohne Ketchup',
    ])
  })

  it('shows the load warning inside the board when the queue never loaded', async () => {
    stubTheLaptop({
      orders: () => {
        throw new TypeError('Failed to fetch')
      },
    })
    const page = await mountPage()

    await page.get('.show-overview').trigger('click')
    await flushPromises()

    expect(textsOf('.station-open-board .board-failed')).toEqual([
      'Laden Sie die Seite neu. Der Rechner war nicht erreichbar, deshalb kann diese Liste veraltet sein.',
    ])
  })

  it('shows a refused action inside the board', async () => {
    stubTheLaptop({
      hide: () => refused('UnprocessableEntity', 'station.orderNotAtThisStation'),
    })
    const page = await mountPage()

    await page.get('.as-it-comes-column .hide').trigger('click')
    await flushPromises()
    await page.get('.show-overview').trigger('click')
    await flushPromises()

    expect(textsOf('.station-open-board .board-failed')).toEqual([
      'Diese Bestellung gehört nicht zu dieser Ausgabestelle. Laden Sie die Seite neu und versuchen Sie es erneut.',
    ])
  })
})

describe('marking selected items as done from a card', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('keeps the done control of a card off until one of its lines is selected', async () => {
    stubTheLaptop()
    const page = await mountPage()
    const done = page.findAll('.orders-column .station-slice')[0].get('.fulfill')

    expect(done.attributes('disabled')).toBeDefined()

    await page.findAll('.orders-column .station-slice')[0].findAll('.station-item')[0].trigger('click')

    expect(page.findAll('.orders-column .station-slice')[0].get('.fulfill').attributes('disabled')).toBeUndefined()
  })

  it('selects and deselects a line on tap', async () => {
    stubTheLaptop()
    const page = await mountPage()
    const line = page.findAll('.orders-column .station-slice')[0].findAll('.station-item')[0]

    await line.trigger('click')
    expect(line.attributes('aria-pressed')).toBe('true')
    expect(line.find('.selected-tick').exists()).toBe(true)

    await line.trigger('click')
    expect(line.attributes('aria-pressed')).toBe('false')
    expect(line.find('.selected-tick').exists()).toBe(false)
  })

  it('asks again on a full screen before anything is done', async () => {
    stubTheLaptop()
    const page = await mountPage()
    const card = page.findAll('.orders-column .station-slice')[0]

    await card.findAll('.station-item')[0].trigger('click')
    await card.findAll('.station-item')[1].trigger('click')
    await card.get('.fulfill').trigger('click')
    await flushPromises()

    expect(textsOf('.station-done-dialog .row-table')).toEqual(['Tisch 3'])
    expect(textsOf('.station-done-dialog .unit')).toEqual([
      '1 x Bratwurst · Hinweis: ohne Senf',
      '1 x Pommes',
    ])
    expect(document.querySelector('.station-done-dialog .confirm')?.textContent?.trim()).toBe(
      'Erledigen',
    )
    expect(document.querySelector('.station-done-dialog .cancel')?.textContent?.trim()).toBe(
      'Abbrechen',
    )
  })

  it('asks again with one line per note, so two different frankfurters are not merged', async () => {
    stubTheLaptop({ orders: () => queue([NOTED_SLICE]) })
    const page = await mountPage()
    const card = page.findAll('.orders-column .station-slice')[0]

    await card.findAll('.station-item')[0].trigger('click')
    await card.findAll('.station-item')[1].trigger('click')
    await card.get('.fulfill').trigger('click')
    await flushPromises()

    expect(textsOf('.station-done-dialog .unit')).toEqual([
      '1 x Frankfurter · Hinweis: Mit Ketchup',
      '1 x Frankfurter · Hinweis: Ohne Ketchup',
    ])
  })

  it('keeps the selection when the employee backs out of the question', async () => {
    stubTheLaptop()
    const page = await mountPage()
    const card = page.findAll('.orders-column .station-slice')[0]
    await card.findAll('.station-item')[0].trigger('click')
    await card.get('.fulfill').trigger('click')
    await flushPromises()

    ;(document.querySelector('.station-done-dialog .cancel') as HTMLElement).click()
    await flushPromises()

    expect(document.querySelector('.station-done-dialog')).toBeNull()
    expect(card.findAll('.station-item')[0].attributes('aria-pressed')).toBe('true')
    expect(card.get('.fulfill').attributes('disabled')).toBeUndefined()
  })

  it('posts the selected items and updates the screen from the answer', async () => {
    const posts = stubTheLaptop({
      fulfill: () => queue([AS_IT_COMES_SLICE], [AS_IT_COMES_SLICE]),
    })
    const page = await mountPage()
    const card = page.findAll('.orders-column .station-slice')[0]

    await card.findAll('.station-item')[0].trigger('click')
    await card.findAll('.station-item')[1].trigger('click')
    await card.get('.fulfill').trigger('click')
    await flushPromises()
    ;(document.querySelector('.station-done-dialog .confirm') as HTMLElement).click()
    await flushPromises()

    expect(posts).toEqual([
      { url: '/api/station/items/fulfill', body: { orderItemIds: ['a', 'b'] } },
    ])
    expect(page.findAll('.orders-column .station-slice')).toHaveLength(1)
    expect(page.get('.orders-column .table-name').text()).toBe('Tisch 7')
  })

  it('states the reason when the laptop did not save the change', async () => {
    stubTheLaptop({
      fulfill: () => refused('ItemNotFulfilled', 'station.changeNotSaved'),
    })
    const page = await mountPage()
    const card = page.findAll('.orders-column .station-slice')[0]

    await card.findAll('.station-item')[0].trigger('click')
    await card.get('.fulfill').trigger('click')
    await flushPromises()
    ;(document.querySelector('.station-done-dialog .confirm') as HTMLElement).click()
    await flushPromises()

    expect(page.get('.action-failed').text()).toBe(
      'Die Bestellung konnte nicht aktualisiert werden. Laden Sie die Seite neu und versuchen Sie es erneut.',
    )
  })
})

describe('hiding an order from the second column', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('offers the hide control on the as-it-comes card only', async () => {
    stubTheLaptop()
    const page = await mountPage()

    expect(page.findAll('.hide')).toHaveLength(1)
    expect(page.get('.as-it-comes-column .hide').text()).toBe('Hier ausblenden')
  })

  it('takes the card out of the second column and leaves it in the first', async () => {
    const posts = stubTheLaptop({
      hide: () =>
        queue([TOGETHER_SLICE, { ...AS_IT_COMES_SLICE, isHiddenFromAsItComesQueue: true }]),
    })
    const page = await mountPage()

    await page.get('.as-it-comes-column .hide').trigger('click')
    await flushPromises()

    expect(posts).toEqual([{ url: '/api/station/orders/slice-2/hide', body: null }])
    expect(page.findAll('.as-it-comes-column .station-slice')).toHaveLength(0)
    expect(page.findAll('.orders-column .station-slice')).toHaveLength(2)
  })

  it('states the reason when the order belongs to another station', async () => {
    stubTheLaptop({
      hide: () => refused('UnprocessableEntity', 'station.orderNotAtThisStation'),
    })
    const page = await mountPage()

    await page.get('.as-it-comes-column .hide').trigger('click')
    await flushPromises()

    expect(page.get('.action-failed').text()).toBe(
      'Diese Bestellung gehört nicht zu dieser Ausgabestelle. Laden Sie die Seite neu und versuchen Sie es erneut.',
    )
  })
})

describe('the done view', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('opens from the button and lists every order with at least one done item', async () => {
    stubTheLaptop({ fulfilled: () => ok({ slices: [DONE_SLICE] }) })
    const page = await mountPage()

    await page.get('.show-done').trigger('click')
    await flushPromises()

    expect(page.get('.done-heading').text()).toBe('Erledigte Bestellungen')
    expect(page.findAll('.station-fulfilled')).toHaveLength(1)
    expect(page.get('.back-to-orders').text()).toBe('Zurück zu den Bestellungen')
  })

  it('shows every item and ticks the done ones', async () => {
    stubTheLaptop({ fulfilled: () => ok({ slices: [DONE_SLICE] }) })
    const page = await mountPage()
    await page.get('.show-done').trigger('click')
    await flushPromises()

    const items = page.get('.station-fulfilled').findAll('.station-item')

    expect(items).toHaveLength(3)
    expect(items[0].find('.item-tick').exists()).toBe(true)
    expect(items[1].find('.item-tick').exists()).toBe(false)
    expect(items[2].find('.item-tick').exists()).toBe(true)
  })

  it('summarises on one line what the order contained', async () => {
    stubTheLaptop({ fulfilled: () => ok({ slices: [DONE_SLICE] }) })
    const page = await mountPage()
    await page.get('.show-done').trigger('click')
    await flushPromises()

    expect(page.get('.station-fulfilled .unit-summary').text()).toBe('2 x Bratwurst · 1 x Pommes')
  })

  it('keeps two different notes apart in the summary line', async () => {
    stubTheLaptop({ fulfilled: () => ok({ slices: [DONE_NOTED_SLICE] }) })
    const page = await mountPage()
    await page.get('.show-done').trigger('click')
    await flushPromises()

    expect(page.get('.station-fulfilled .unit-summary').text()).toBe(
      '1 x Frankfurter · Hinweis: Mit Ketchup · 1 x Frankfurter · Hinweis: Ohne Ketchup',
    )
  })

  it('offers a put back control on a done item only and posts it', async () => {
    const posts = stubTheLaptop({
      fulfilled: () => ok({ slices: [DONE_SLICE] }),
      unfulfill: () => queue([{ ...DONE_SLICE, fulfilledItemCount: 1 }]),
    })
    const page = await mountPage()
    await page.get('.show-done').trigger('click')
    await flushPromises()

    expect(page.findAll('.station-fulfilled .put-back')).toHaveLength(2)
    expect(page.get('.station-fulfilled .put-back').text()).toBe('Zurücklegen')

    await page.get('.station-fulfilled .put-back').trigger('click')
    await flushPromises()

    expect(posts).toEqual([{ url: '/api/station/items/unfulfill', body: { orderItemIds: ['a'] } }])
  })

  it('states the reason when the laptop did not put an item back', async () => {
    stubTheLaptop({
      fulfilled: () => ok({ slices: [DONE_SLICE] }),
      unfulfill: () => refused('ItemNotFulfilled', 'station.changeNotSaved'),
    })
    const page = await mountPage()
    await page.get('.show-done').trigger('click')
    await flushPromises()

    await page.get('.station-fulfilled .put-back').trigger('click')
    await flushPromises()

    expect(page.get('.action-failed').text()).toBe(
      'Die Bestellung konnte nicht aktualisiert werden. Laden Sie die Seite neu und versuchen Sie es erneut.',
    )
  })

  it('goes back to the queue', async () => {
    stubTheLaptop({ fulfilled: () => ok({ slices: [DONE_SLICE] }) })
    const page = await mountPage()
    await page.get('.show-done').trigger('click')
    await flushPromises()

    await page.get('.back-to-orders').trigger('click')
    await flushPromises()

    expect(page.findAll('.orders-column .station-slice')).toHaveLength(2)
    expect(page.find('.station-fulfilled').exists()).toBe(false)
  })

  it('says so when nothing is done yet', async () => {
    stubTheLaptop({ fulfilled: () => ok({ slices: [] }) })
    const page = await mountPage()

    await page.get('.show-done').trigger('click')
    await flushPromises()

    expect(page.get('.nothing-done').text()).toBe('Es ist noch keine Bestellung erledigt.')
  })
})

describe('a station tablet that has lost contact with the laptop', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
    stubTheLaptop({
      orders: () => {
        throw new TypeError('Failed to fetch')
      },
    })
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('says the list may be out of date, so nobody trusts an empty screen', async () => {
    const page = await mountPage()

    expect(page.get('.load-failed').text()).toBe(
      'Laden Sie die Seite neu. Der Rechner war nicht erreichbar, deshalb kann diese Liste veraltet sein.',
    )
  })
})

describe('a station tablet the laptop turned away', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('says that no festival is running instead of blaming the connection', async () => {
    stubTheLaptop({
      orders: () => refused('NoRunningFestival', 'station.noFestivalIsRunning'),
    })

    const page = await mountPage()

    expect(page.get('.load-failed').text()).toBe('Es ist kein Fest aktiv.')
  })

  it('says the station does not belong to this festival', async () => {
    stubTheLaptop({
      orders: () => refused('StationNotAtTheFestival', 'station.notPartOfTheFestival'),
    })

    const page = await mountPage()

    expect(page.get('.load-failed').text()).toBe(
      'Diese Ausgabestelle gehört nicht zum laufenden Fest.',
    )
  })
})

