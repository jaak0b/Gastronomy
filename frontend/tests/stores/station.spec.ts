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
        canHandleOnPaperReasonKey: null,
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
