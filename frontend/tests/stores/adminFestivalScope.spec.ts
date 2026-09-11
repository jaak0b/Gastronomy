import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useAdminItemsStore } from '../../src/stores/admin/items'
import { useAdminStationsStore } from '../../src/stores/admin/stations'
import { useConnectionStore } from '../../src/stores/connection'

const FESTIVAL_ID = 'fest-1'

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

describe('the lists the admin reads', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('carry every item of the laptop while no festival is named', async () => {
    const calls = stubTheLaptop()

    await useAdminItemsStore().load()

    expect(calls.at(-1)?.url).toBe('/api/admin/items')
  })

  it('carry the items of the festival whose page is open', async () => {
    const calls = stubTheLaptop()

    await useAdminItemsStore().loadAtTheFestival(FESTIVAL_ID)

    expect(calls.at(-1)?.url).toBe('/api/admin/items?festivalId=fest-1')
  })

  it('carry every station of the laptop while no festival is named', async () => {
    const calls = stubTheLaptop()

    await useAdminStationsStore().load()

    expect(calls.at(-1)?.url).toBe('/api/admin/stations')
  })

  it('carry the stations of the festival whose page is open', async () => {
    const calls = stubTheLaptop()

    await useAdminStationsStore().loadAtTheFestival(FESTIVAL_ID)

    expect(calls.at(-1)?.url).toBe('/api/admin/stations?festivalId=fest-1')
  })

  it('stay on that festival after the connection asks for everything again', async () => {
    const calls = stubTheLaptop()
    const stations = useAdminStationsStore()
    stations.listen()
    await stations.loadAtTheFestival(FESTIVAL_ID)

    await useConnectionStore().refetchAll()

    expect(calls.at(-1)?.url).toBe('/api/admin/stations?festivalId=fest-1')
  })
})

describe('an item at a festival', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('is put at the festival in the address, with its price and its stations', async () => {
    const calls = stubTheLaptop()

    await useAdminItemsStore().putAtTheFestival(FESTIVAL_ID, 'item-1', {
      priceCents: 350,
      stationIds: ['station-kueche'],
    })

    const sent = calls.find((call) => call.method === 'PUT')
    expect(sent?.url).toBe('/api/admin/festivals/fest-1/items/item-1')
    expect(sent?.body).toEqual({ priceCents: 350, stationIds: ['station-kueche'] })
  })

  it('is removed from that festival alone', async () => {
    const calls = stubTheLaptop()

    await useAdminItemsStore().removeFromTheFestival(FESTIVAL_ID, 'item-1')

    expect(calls.find((call) => call.method === 'DELETE')?.url).toBe(
      '/api/admin/festivals/fest-1/items/item-1',
    )
  })

  it('is sold out at that festival alone', async () => {
    const calls = stubTheLaptop()

    await useAdminItemsStore().setAvailability(FESTIVAL_ID, 'item-1', false)

    const sent = calls.find((call) => call.method === 'POST')
    expect(sent?.url).toBe('/api/admin/festivals/fest-1/items/item-1/availability')
    expect(sent?.body).toEqual({ isAvailable: false })
  })
})

describe('a station at a festival', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('is added to that festival, without a body', async () => {
    const calls = stubTheLaptop()

    await useAdminStationsStore().addToTheFestival(FESTIVAL_ID, 'station-kueche')

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
    const stations = useAdminStationsStore()

    await stations.removeFromTheFestival(FESTIVAL_ID, 'station-kueche')

    expect(stations.errorMessage?.key).toBe('admin.stationHasOrdersAtTheFestival')
    expect(stations.errorMessage?.count).toBe(3)
  })

  it('is created and handed back with its id, so it can be added straight away', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string, init?: RequestInit) =>
        (init?.method ?? 'GET') === 'POST'
          ? new Response(JSON.stringify({ stationId: 'station-new' }), { status: 201 })
          : new Response(JSON.stringify({ stations: [] }), { status: 200 }),
      ),
    )

    const stationId = await useAdminStationsStore().create({ name: 'Theke', sortOrder: 1 })

    expect(stationId).toBe('station-new')
  })
})
