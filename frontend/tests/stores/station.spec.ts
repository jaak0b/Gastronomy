import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useStationStore } from '../../src/stores/station'
import { useConnectionStore } from '../../src/stores/connection'

describe('the ten second delay before a slip is taken', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.useFakeTimers()
    localStorage.clear()
  })

  afterEach(() => {
    vi.useRealTimers()
    vi.unstubAllGlobals()
  })

  function seedStation() {
    const fetchCalls: string[] = []
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string) => {
        fetchCalls.push(url)
        return new Response(JSON.stringify({ stationOrders: [] }), { status: 200 })
      }),
    )
    const station = useStationStore()
    station.selectedStationId = 'station-kueche'
    station.stationOrders = [
      {
        stationOrderId: 'ticket-1',
        orderId: 'order-1',
        globalOrderNumber: 137,
        stationOrderNumber: 42,
        tableName: 'Tisch 12',
        orderCreatedAtUtc: '2026-08-27T19:00:00Z',
        status: 'Failed',
        canHandleOnPaper: true,
        copyNumber: 0,
        orderNote: null,
        items: [],
      },
    ]
    return { station, fetchCalls }
  }

  it('sends nothing at the moment the row is tapped', () => {
    const { station, fetchCalls } = seedStation()

    station.beginTake('ticket-1')

    expect(fetchCalls).toEqual([])
  })

  it('marks the row as pending while the countdown runs', () => {
    const { station } = seedStation()

    station.beginTake('ticket-1')

    expect(station.isPending('ticket-1')).toBe(true)
  })

  it('sends the acknowledgement once the countdown has run out', async () => {
    const { station, fetchCalls } = seedStation()

    station.beginTake('ticket-1')
    await vi.advanceTimersByTimeAsync(10000)

    expect(fetchCalls).toContain('/api/stations/station-kueche/station-orders/ticket-1/hand-on-paper')
  })

  it('never reaches the acknowledge endpoint when the cook taps undo', async () => {
    const { station, fetchCalls } = seedStation()

    station.beginTake('ticket-1')
    station.undoTake('ticket-1')
    await vi.advanceTimersByTimeAsync(60000)

    expect(fetchCalls).toEqual([])
  })

  it('returns the row to what it was after an undo', () => {
    const { station } = seedStation()

    station.beginTake('ticket-1')
    station.undoTake('ticket-1')

    expect(station.isPending('ticket-1')).toBe(false)
  })
})

describe('a load that does not reach the laptop', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  function stubLaptop(reachablePaths: string[]) {
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string) =>
        reachablePaths.some((path) => url.includes(path))
          ? new Response(
              JSON.stringify({
                stationOrders: [],
                stations: [{ stationId: 'station-kueche', name: 'Küche', canPrint: true }],
              }),
              { status: 200 },
            )
          : new Response('{}', { status: 500 }),
      ),
    )
  }

  it('is admitted when the slip list could not be fetched', async () => {
    stubLaptop(['/status'])
    const station = useStationStore()
    station.selectedStationId = 'station-kueche'

    await station.loadTickets()

    expect(station.loadFailed).toBe(true)
  })

  it('is admitted when the printer status could not be fetched', async () => {
    stubLaptop(['/station-orders'])
    const station = useStationStore()
    station.selectedStationId = 'station-kueche'

    await station.loadPrinter()

    expect(station.loadFailed).toBe(true)
  })

  it('is taken back once the slip list arrives again', async () => {
    stubLaptop([])
    const station = useStationStore()
    station.selectedStationId = 'station-kueche'
    await station.loadTickets()

    stubLaptop(['/station-orders'])
    await station.loadTickets()

    expect(station.loadFailed).toBe(false)
  })

  it('asks for the station list again once the screen catches up', async () => {
    stubLaptop([])
    const station = useStationStore()
    await station.open()

    stubLaptop(['/api/stations', '/station-orders', '/status'])
    await station.refresh()

    expect(station.stations).toHaveLength(1)
    expect(station.loadFailed).toBe(false)
  })
})

describe('the station a cook switches to', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('is asked after with its own printer, because the banner belongs to that station', async () => {
    const urls: string[] = []
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string) => {
        urls.push(url)
        return new Response(JSON.stringify({ stationOrders: [], stations: [] }), { status: 200 })
      }),
    )
    const station = useStationStore()
    station.selectedStationId = 'station-kueche'
    urls.length = 0

    await station.selectStation('station-theke')

    expect(urls).toContain('/api/stations/station-theke/status')
  })
})

describe('a station screen that lost the hub and got it back', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('fetches its slips again when everyone is asked to catch up', async () => {
    const urls: string[] = []
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string) => {
        urls.push(url)
        return new Response(JSON.stringify({ stationOrders: [], stations: [] }), { status: 200 })
      }),
    )
    const station = useStationStore()
    const connection = useConnectionStore()
    station.selectedStationId = 'station-kueche'
    station.listen()
    urls.length = 0

    await connection.refetchAll()

    expect(urls.some((url) => url.includes('/station-orders'))).toBe(true)
  })

  it('asks after its printer again too, because a stale banner is a lie', async () => {
    const urls: string[] = []
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string) => {
        urls.push(url)
        return new Response(JSON.stringify({ stationOrders: [], stations: [] }), { status: 200 })
      }),
    )
    const station = useStationStore()
    const connection = useConnectionStore()
    station.selectedStationId = 'station-kueche'
    station.listen()
    urls.length = 0

    await connection.refetchAll()

    expect(urls.some((url) => url.includes('/status'))).toBe(true)
  })
})
