import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import StationPage from '../../../src/views/StationPage.vue'
import { useSessionStore } from '../../../src/stores/session'
import { testPlugins } from '../../support/plugins'

const KITCHEN = { id: 'station-kueche', name: 'Küche' }

const TOGETHER_SLICE = {
  stationOrderId: 'slice-1',
  globalOrderNumber: 137,
  stationOrderNumber: 12,
  tableName: 'Tisch 3',
  note: 'Bitte zusammen bringen',
  deliveryMode: 'together',
  createdAtUtc: '2026-09-05T18:00:00Z',
  items: [
    { orderItemId: 'a', itemName: 'Bratwurst', note: 'ohne Senf', productionStatus: 'waiting' },
    { orderItemId: 'b', itemName: 'Pommes', note: null, productionStatus: 'inProduction' },
  ],
}

const AS_IT_COMES_SLICE = {
  stationOrderId: 'slice-2',
  globalOrderNumber: 138,
  stationOrderNumber: 14,
  tableName: 'Tisch 7',
  note: null,
  deliveryMode: 'asItComes',
  createdAtUtc: '2026-09-05T18:05:00Z',
  items: [
    { orderItemId: 'c', itemName: 'Bier', note: null, productionStatus: 'waiting' },
  ],
}

function stubTheLaptop(action?: () => Response) {
  const bodies: unknown[] = []
  vi.stubGlobal(
    'fetch',
    vi.fn(async (url: string, options?: RequestInit) => {
      if (url === '/api/station/items/status') {
        bodies.push(JSON.parse(String(options?.body ?? 'null')))
        return (
          action?.()
          ?? new Response(
            JSON.stringify({ tableName: 'Tisch 3', slices: [TOGETHER_SLICE, AS_IT_COMES_SLICE] }),
            { status: 200 },
          )
        )
      }
      return new Response(
        JSON.stringify({ station: KITCHEN, slices: [TOGETHER_SLICE, AS_IT_COMES_SLICE] }),
        { status: 200 },
      )
    }),
  )
  return bodies
}

async function mountPage() {
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

  it('puts the orders that go out together in the left column', async () => {
    const page = await mountPage()

    expect(page.get('.together-column').findAll('.station-slice')).toHaveLength(1)
  })

  it('puts each single item of an order that goes out as it is ready in the right column', async () => {
    const page = await mountPage()

    expect(page.get('.single-column').findAll('.station-single')).toHaveLength(1)
  })

  it('shows the order number and the number of this station on a card', async () => {
    const page = await mountPage()

    expect(page.get('.station-slice .slice-heading').text()).toBe(
      'Bestellung 137, hier Nummer 12',
    )
  })

  it('shows the table on a card, because that is what goes on the tray', async () => {
    const page = await mountPage()

    expect(page.get('.station-slice .table-name').text()).toBe('Tisch 3')
  })

  it('shows the note that belongs to the whole order', async () => {
    const page = await mountPage()

    expect(page.get('.station-slice .slice-note').text()).toBe(
      'Hinweis zur Bestellung: Bitte zusammen bringen',
    )
  })

  it('shows every item of the order with the note and the state it is in', async () => {
    const page = await mountPage()

    const items = page.get('.station-slice').findAll('.station-item')

    expect(items).toHaveLength(2)
    expect(items[0].get('.item-name').text()).toBe('Bratwurst')
    expect(items[0].get('.item-note').text()).toBe('Hinweis: ohne Senf')
    expect(items[0].get('.item-status').text()).toBe('wartet')
    expect(items[1].get('.item-status').text()).toBe('in Zubereitung')
  })

  it('keeps the number of this station visible on a single item too, so a gap is noticeable', async () => {
    const page = await mountPage()

    expect(page.get('.station-single .slice-heading').text()).toBe(
      'Bestellung 138, hier Nummer 14',
    )
  })
})

describe('moving work on from the station screen', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('starts one item on its own from inside an order that goes out together', async () => {
    const bodies = stubTheLaptop()
    const page = await mountPage()

    await page.get('.station-slice .station-item .advance-item').trigger('click')

    expect(bodies).toEqual([{ orderItemIds: ['a'], status: 'inProduction' }])
  })

  it('starts everything that is still waiting with the control on the whole order', async () => {
    const bodies = stubTheLaptop()
    const page = await mountPage()

    await page.get('.station-slice .advance-slice').trigger('click')

    expect(bodies).toEqual([{ orderItemIds: ['a'], status: 'inProduction' }])
  })

  it('names the control on the whole order for what it will do next', async () => {
    stubTheLaptop()
    const page = await mountPage()

    expect(page.get('.station-slice .advance-slice').text()).toBe('Ganze Bestellung beginnen')
  })

  it('names the control on a single item for what it will do next', async () => {
    stubTheLaptop()
    const page = await mountPage()

    expect(page.get('.station-single .advance-item').text()).toBe('Zubereitung beginnen')
  })

  it('names the table in a notice once something is ready', async () => {
    stubTheLaptop(
      () =>
        new Response(
          JSON.stringify({
            tableName: 'Tisch 3',
            slices: [
              {
                ...TOGETHER_SLICE,
                items: [
                  { orderItemId: 'a', itemName: 'Bratwurst', note: null, productionStatus: 'inProduction' },
                  { orderItemId: 'b', itemName: 'Pommes', note: null, productionStatus: 'inProduction' },
                ],
              },
            ],
          }),
          { status: 200 },
        ),
    )
    const page = await mountPage()
    await page.get('.station-slice .advance-slice').trigger('click')
    await flushPromises()
    await page.get('.station-slice .advance-slice').trigger('click')
    await flushPromises()

    expect(page.get('.ready-notice').text()).toContain('Tisch 3')
  })

  it('keeps the list as it was and states the reason when the laptop refused', async () => {
    stubTheLaptop(
      () =>
        new Response(
          JSON.stringify({
            code: 'Conflict',
            messageKey: 'station.statusAlreadyPassed',
            parameters: {},
            details: null,
          }),
          { status: 409 },
        ),
    )
    const page = await mountPage()

    await page.get('.station-slice .advance-slice').trigger('click')
    await flushPromises()

    expect(page.get('.action-failed').text()).toBe(
      'Laden Sie die Seite neu. Diese Position ist schon weiter, als hier steht.',
    )
    expect(page.get('.station-slice').findAll('.station-item')).toHaveLength(2)
  })
})

describe('a station tablet that has lost contact with the laptop', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => {
        throw new TypeError('Failed to fetch')
      }),
    )
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('says the list may be out of date, so nobody trusts an empty screen', async () => {
    const page = await mountPage()

    expect(page.get('.load-failed').text()).toBe(
      'Laden Sie die Seite neu. Der Laptop war nicht erreichbar, deshalb kann diese Liste veraltet sein.',
    )
  })

  it('never claims there is nothing to prepare while it is out of contact', async () => {
    const page = await mountPage()

    expect(page.find('.empty').exists()).toBe(false)
  })
})

describe('a station with nothing to prepare', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
    vi.stubGlobal(
      'fetch',
      vi.fn(
        async () => new Response(JSON.stringify({ station: KITCHEN, slices: [] }), { status: 200 }),
      ),
    )
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('says so rather than showing two empty columns without a word', async () => {
    const page = await mountPage()

    expect(page.get('.empty').text()).toBe('Im Moment ist nichts zuzubereiten.')
  })
})
