import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useAdminFestivalsStore } from '../../src/stores/admin/festivals'
import { useAdminItemsStore } from '../../src/stores/admin/items'
import { useAdminStationsStore } from '../../src/stores/admin/stations'
import { useConnectionStore } from '../../src/stores/connection'
import { PICKED_FESTIVAL_STORAGE_KEY } from '../../src/core/pickedFestival'

const SUMMER = {
  festivalId: 'fest-1',
  name: 'Sommerfest',
  startsAtUtc: '2026-07-18T10:00:00Z',
  endsAtUtc: '2026-07-19T02:00:00Z',
  isHidden: false,
  isRunning: true,
  stationCount: 2,
  menuItemCount: 8,
  orderCount: 0,
}

interface Call {
  url: string
  method: string
  body: unknown
}

function stubTheLaptop(): Call[] {
  const calls: Call[] = []
  vi.stubGlobal(
    'fetch',
    vi.fn(async (url: string, init?: RequestInit) => {
      calls.push({
        url,
        method: init?.method ?? 'GET',
        body: init?.body === undefined ? undefined : JSON.parse(String(init.body)),
      })
      if (url.startsWith('/api/admin/festivals?') || url === '/api/admin/festivals') {
        return new Response(JSON.stringify({ festivals: [SUMMER] }), { status: 200 })
      }
      if (url.startsWith('/api/admin/items')) {
        return new Response(JSON.stringify({ items: [] }), { status: 200 })
      }
      if (url.startsWith('/api/admin/stations')) {
        return new Response(JSON.stringify({ stations: [] }), { status: 200 })
      }
      return new Response(JSON.stringify({}), { status: 200 })
    }),
  )
  return calls
}

describe('the festival the admin has open', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
    localStorage.clear()
  })

  it('scopes the item list to that festival', async () => {
    const calls = stubTheLaptop()
    useAdminFestivalsStore().pick('fest-1')

    await useAdminItemsStore().load()

    expect(calls.at(-1)?.url).toBe('/api/admin/items?festivalId=fest-1')
  })

  it('leaves the item list unscoped while no festival is open', async () => {
    const calls = stubTheLaptop()

    await useAdminItemsStore().load()

    expect(calls.at(-1)?.url).toBe('/api/admin/items')
  })

  it('scopes the station list to that festival', async () => {
    const calls = stubTheLaptop()
    useAdminFestivalsStore().pick('fest-1')

    await useAdminStationsStore().load()

    expect(calls.at(-1)?.url).toBe('/api/admin/stations?festivalId=fest-1')
  })

  it('is still the scope after the connection asks for everything again', async () => {
    const calls = stubTheLaptop()
    const festivals = useAdminFestivalsStore()
    const items = useAdminItemsStore()
    const stations = useAdminStationsStore()
    festivals.pick('fest-1')
    const connection = useConnectionStore()
    connection.registerRefetch(items.load)
    stations.listen()

    await connection.refetchAll()

    expect(calls.map((call) => call.url)).toContain('/api/admin/items?festivalId=fest-1')
    expect(calls.map((call) => call.url)).toContain('/api/admin/stations?festivalId=fest-1')
  })

  it('is remembered in the browser, so a reload lands on the same festival', () => {
    useAdminFestivalsStore().pick('fest-1')

    expect(localStorage.getItem(PICKED_FESTIVAL_STORAGE_KEY)).toBe('fest-1')
  })

  it('is dropped when the laptop no longer lists it', async () => {
    stubTheLaptop()
    const festivals = useAdminFestivalsStore()
    festivals.pick('fest-gone')

    await festivals.load()

    expect(festivals.pickedFestivalId).toBeNull()
    expect(localStorage.getItem(PICKED_FESTIVAL_STORAGE_KEY)).toBeNull()
  })
})

describe('an item on a festival menu', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
    localStorage.clear()
  })

  it('is put on the menu of the festival that is open, with its price and its stations', async () => {
    const calls = stubTheLaptop()
    useAdminFestivalsStore().pick('fest-1')

    await useAdminItemsStore().putOnTheMenu('item-1', {
      priceCents: 350,
      stationIds: ['station-kueche'],
    })

    const sent = calls.find((call) => call.method === 'PUT')
    expect(sent?.url).toBe('/api/admin/festivals/fest-1/items/item-1')
    expect(sent?.body).toEqual({ priceCents: 350, stationIds: ['station-kueche'] })
  })

  it('is taken off that festival menu alone', async () => {
    const calls = stubTheLaptop()
    useAdminFestivalsStore().pick('fest-1')

    await useAdminItemsStore().takeOffTheMenu('item-1')

    expect(calls.find((call) => call.method === 'DELETE')?.url).toBe(
      '/api/admin/festivals/fest-1/items/item-1',
    )
  })

  it('is sold out at that festival alone', async () => {
    const calls = stubTheLaptop()
    useAdminFestivalsStore().pick('fest-1')

    await useAdminItemsStore().setAvailability('item-1', false)

    const sent = calls.find((call) => call.method === 'POST')
    expect(sent?.url).toBe('/api/admin/festivals/fest-1/items/item-1/availability')
    expect(sent?.body).toEqual({ isAvailable: false })
  })

  it('is not sent anywhere while no festival is open', async () => {
    const calls = stubTheLaptop()

    const wasPut = await useAdminItemsStore().putOnTheMenu('item-1', {
      priceCents: 350,
      stationIds: ['station-kueche'],
    })

    expect(wasPut).toBe(false)
    expect(calls).toEqual([])
  })
})

describe('a station at a festival', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
    localStorage.clear()
  })

  it('is added to the festival that is open, without a body', async () => {
    const calls = stubTheLaptop()
    useAdminFestivalsStore().pick('fest-1')

    await useAdminStationsStore().addToTheFestival('station-kueche')

    const sent = calls.find((call) => call.method === 'PUT')
    expect(sent?.url).toBe('/api/admin/festivals/fest-1/stations/station-kueche')
    expect(sent?.body).toBeUndefined()
  })

  it('keeps the reason the laptop gave for refusing to remove it', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string, init?: RequestInit) =>
        (init?.method ?? 'GET') === 'DELETE'
          ? new Response(
              JSON.stringify({
                code: 'StationHasOrdersAtTheFestival',
                messageKey: 'admin.stationHasOrdersAtTheFestival',
                parameters: { count: '3' },
                details: null,
              }),
              { status: 409 },
            )
          : new Response(JSON.stringify({ stations: [] }), { status: 200 }),
      ),
    )
    useAdminFestivalsStore().pick('fest-1')
    const stations = useAdminStationsStore()

    await stations.removeFromTheFestival('station-kueche')

    expect(stations.errorMessage?.key).toBe('admin.stationHasOrdersAtTheFestival')
    expect(stations.errorMessage?.count).toBe(3)
  })
})
