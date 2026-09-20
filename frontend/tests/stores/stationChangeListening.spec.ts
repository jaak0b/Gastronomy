import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { fireHubEvent, forgetHubEvents } from '../support/hubConnection'

vi.mock('@microsoft/signalr', async () => (await import('../support/hubConnection')).signalrModuleFake())

const { useConnectionStore } = await import('../../src/shared/stores/connection')
const { useCatalogStore } = await import('../../src/stores/catalog')
const { useStationStore } = await import('../../src/stores/station')
const { useSessionStore } = await import('../../src/shared/stores/session')

const KITCHEN = { id: 'station-kueche', name: 'Kueche am Zelt' }

function stubTheLaptop(): string[] {
  const urls: string[] = []
  vi.stubGlobal(
    'fetch',
    vi.fn(async (url: string) => {
      urls.push(url)
      if (url === '/api/station/orders/fulfilled') {
        return new Response(JSON.stringify({ stationOrders: [] }), { status: 200 })
      }
      return new Response(
        JSON.stringify({
          festival: null,
          categories: [],
          items: [],
          stations: [],
          station: KITCHEN,
          orders: [],
          asItComes: [],
        }),
        { status: 200 },
      )
    }),
  )
  return urls
}

async function letTheReloadFinish(): Promise<void> {
  await new Promise((resolve) => setTimeout(resolve, 0))
}

describe('a production location the admin renamed or switched off', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    forgetHubEvents()
    useSessionStore().deviceToken = 'token-here'
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('reaches the phone, which loads the menu again to get the current list', async () => {
    const urls = stubTheLaptop()
    useCatalogStore().listen()
    await useConnectionStore().connect({ deviceToken: 'token-here' })
    urls.length = 0

    fireHubEvent('StationsChanged')
    await letTheReloadFinish()

    expect(urls).toEqual(['/api/catalog'])
  })

  it('reaches the tablet standing at it, which loads its board again to get the current name', async () => {
    const urls = stubTheLaptop()
    useStationStore().listen()
    await useConnectionStore().connect({ deviceToken: 'token-here' })
    urls.length = 0

    fireHubEvent('StationsChanged')
    await letTheReloadFinish()

    expect(urls).toEqual(['/api/station/orders'])
  })

  it('is left alone once the tablet has stopped listening', async () => {
    const urls = stubTheLaptop()
    const stopListening = useStationStore().listen()
    await useConnectionStore().connect({ deviceToken: 'token-here' })
    stopListening()
    urls.length = 0

    fireHubEvent('StationsChanged')
    await letTheReloadFinish()

    expect(urls).toEqual([])
  })
})

describe('a festival whose start state changed', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    forgetHubEvents()
    useSessionStore().deviceToken = 'token-here'
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('makes the tablet load its board again', async () => {
    const urls = stubTheLaptop()
    useStationStore().listen()
    await useConnectionStore().connect({ deviceToken: 'token-here' })
    urls.length = 0

    fireHubEvent('FestivalChanged')
    await letTheReloadFinish()

    expect(urls).toEqual(['/api/station/orders'])
  })
})

describe('a change to the orders at a station', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    forgetHubEvents()
    useSessionStore().deviceToken = 'token-here'
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('makes the tablet at that station load its queue again', async () => {
    const urls = stubTheLaptop()
    useStationStore().listen()
    await useConnectionStore().connect({ deviceToken: 'token-here' })
    urls.length = 0

    fireHubEvent('StationOrdersChanged', { stationId: KITCHEN.id })
    await letTheReloadFinish()

    expect(urls).toEqual(['/api/station/orders'])
  })

  it('loads the done list too while it is open', async () => {
    const urls = stubTheLaptop()
    const station = useStationStore()
    station.listen()
    await useConnectionStore().connect({ deviceToken: 'token-here' })
    await station.openFulfilled()
    urls.length = 0

    fireHubEvent('StationOrdersChanged', { stationId: KITCHEN.id })
    await letTheReloadFinish()

    expect(urls).toEqual(['/api/station/orders', '/api/station/orders/fulfilled'])
  })

  it('leaves the done list alone while the queue is on screen', async () => {
    const urls = stubTheLaptop()
    useStationStore().listen()
    await useConnectionStore().connect({ deviceToken: 'token-here' })
    urls.length = 0

    fireHubEvent('StationOrdersChanged', { stationId: KITCHEN.id })
    await letTheReloadFinish()

    expect(urls).toEqual(['/api/station/orders'])
  })
})
