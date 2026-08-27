import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useStationStore } from '../../src/stores/station'

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
        return new Response(JSON.stringify({ tickets: [] }), { status: 200 })
      }),
    )
    const station = useStationStore()
    station.accessKey = 'abc'
    station.selectedLocationId = 'location-kueche'
    station.tickets = [
      {
        ticketId: 'ticket-1',
        orderId: 'order-1',
        globalOrderNumber: 137,
        sequenceNumber: 42,
        tableLabel: 'Tisch 12',
        createdAtUtc: '2026-08-27T19:00:00Z',
        status: 'Failed',
        canAcknowledge: true,
        refusalReasonKey: null,
        reprintCount: 0,
        orderNote: null,
        lines: [],
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

    expect(fetchCalls).toContain('/api/station/abc/tickets/ticket-1/acknowledge')
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
