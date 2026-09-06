import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useStationStore } from '../../src/stores/station'
import { useSessionStore } from '../../src/stores/session'

const KITCHEN = { id: 'station-kueche', name: 'Küche' }

const WAITING_SLICE = {
  stationOrderId: 'slice-1',
  globalOrderNumber: 137,
  stationOrderNumber: 12,
  tableName: 'Tisch 3',
  note: null,
  deliveryMode: 'together',
  createdAtUtc: '2026-09-05T18:00:00Z',
  items: [
    { orderItemId: 'a', itemName: 'Bratwurst', note: null, productionStatus: 'waiting' },
  ],
}

const STARTED_SLICE = {
  ...WAITING_SLICE,
  items: [
    { orderItemId: 'a', itemName: 'Bratwurst', note: null, productionStatus: 'inProduction' },
  ],
}

function enrolledStationTablet() {
  const session = useSessionStore()
  session.deviceToken = 'token-here'
  session.deviceKind = 'station'
}

function stubTheLaptop(action: () => Response) {
  const bodies: unknown[] = []
  vi.stubGlobal(
    'fetch',
    vi.fn(async (url: string, options?: RequestInit) => {
      if (url === '/api/station/items/status') {
        bodies.push(JSON.parse(String(options?.body ?? 'null')))
        return action()
      }
      return new Response(
        JSON.stringify({ station: KITCHEN, slices: [WAITING_SLICE] }),
        { status: 200 },
      )
    }),
  )
  return bodies
}

function accepted(): Response {
  return new Response(
    JSON.stringify({ tableName: 'Tisch 3', slices: [STARTED_SLICE] }),
    { status: 200 },
  )
}

describe('the orders a station tablet is showing', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('names the station the laptop says this tablet belongs to', async () => {
    stubTheLaptop(accepted)
    enrolledStationTablet()
    const station = useStationStore()

    await station.load()

    expect(station.station).toEqual(KITCHEN)
  })

  it('sorts the slices into the ones that go out together and the single items', async () => {
    stubTheLaptop(accepted)
    enrolledStationTablet()
    const station = useStationStore()

    await station.load()

    expect(station.board.together.map((slice) => slice.stationOrderNumber)).toEqual([12])
    expect(station.board.single).toEqual([])
  })

  it('says the list may be out of date when the laptop could not be reached', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => {
        throw new TypeError('Failed to fetch')
      }),
    )
    enrolledStationTablet()
    const station = useStationStore()

    await station.load()

    expect(station.loadFailed).toBe(true)
  })
})

describe('moving an item on', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('tells the laptop which items move and where to', async () => {
    const bodies = stubTheLaptop(accepted)
    enrolledStationTablet()
    const station = useStationStore()

    await station.advance(['a'], 'inProduction')

    expect(bodies).toEqual([{ orderItemIds: ['a'], status: 'inProduction' }])
  })

  it('takes the fresh list out of the answer, so the screen matches the laptop', async () => {
    stubTheLaptop(accepted)
    enrolledStationTablet()
    const station = useStationStore()
    await station.load()

    await station.advance(['a'], 'inProduction')

    expect(station.board.together[0].items[0].productionStatus).toBe('inProduction')
  })

  it('keeps the other orders on screen when only one of them moved on', async () => {
    const otherSlice = {
      ...WAITING_SLICE,
      stationOrderId: 'slice-2',
      globalOrderNumber: 138,
      stationOrderNumber: 13,
      tableName: 'Tisch 9',
      items: [{ orderItemId: 'b', itemName: 'Pommes', note: null, productionStatus: 'waiting' }],
    }
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string) => {
        if (url === '/api/station/items/status') {
          return new Response(
            JSON.stringify({ tableName: 'Tisch 3', slices: [STARTED_SLICE] }),
            { status: 200 },
          )
        }
        return new Response(
          JSON.stringify({ station: KITCHEN, slices: [WAITING_SLICE, otherSlice] }),
          { status: 200 },
        )
      }),
    )
    enrolledStationTablet()
    const station = useStationStore()
    await station.load()

    await station.advance(['a'], 'inProduction')

    expect(station.board.together.map((slice) => slice.stationOrderNumber)).toEqual([12, 13])
  })

  it('takes an order off the board once every one of its items is ready', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string) => {
        if (url === '/api/station/items/status') {
          return new Response(
            JSON.stringify({
              tableName: 'Tisch 3',
              slices: [
                {
                  ...WAITING_SLICE,
                  items: [
                    { orderItemId: 'a', itemName: 'Bratwurst', note: null, productionStatus: 'finished' },
                  ],
                },
              ],
            }),
            { status: 200 },
          )
        }
        return new Response(
          JSON.stringify({ station: KITCHEN, slices: [WAITING_SLICE] }),
          { status: 200 },
        )
      }),
    )
    enrolledStationTablet()
    const station = useStationStore()
    await station.load()

    await station.advance(['a'], 'finished')

    expect(station.board.together).toEqual([])
    expect(station.readyTableName).toBe('Tisch 3')
  })

  it('names the table on screen once something is ready, so it can be written on the tray', async () => {
    stubTheLaptop(accepted)
    enrolledStationTablet()
    const station = useStationStore()

    await station.advance(['a'], 'finished')

    expect(station.readyTableName).toBe('Tisch 3')
  })

  it('names no table when preparation only started', async () => {
    stubTheLaptop(accepted)
    enrolledStationTablet()
    const station = useStationStore()

    await station.advance(['a'], 'inProduction')

    expect(station.readyTableName).toBeNull()
  })

  it('drops the notice again when the person taps it away', async () => {
    stubTheLaptop(accepted)
    enrolledStationTablet()
    const station = useStationStore()
    await station.advance(['a'], 'finished')

    station.dismissReadyNotice()

    expect(station.readyTableName).toBeNull()
  })

  it('drops the notice again as soon as the next thing happens', async () => {
    stubTheLaptop(accepted)
    enrolledStationTablet()
    const station = useStationStore()
    await station.advance(['a'], 'finished')

    await station.advance(['a'], 'inProduction')

    expect(station.readyTableName).toBeNull()
  })
})

describe('a change the laptop did not take', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('shows the reason the laptop gave and leaves the list as it was', async () => {
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
    enrolledStationTablet()
    const station = useStationStore()
    await station.load()

    await station.advance(['a'], 'finished')

    expect(station.failureKey).toBe('station.statusAlreadyPassed')
    expect(station.board.together[0].items[0].productionStatus).toBe('waiting')
  })

  it('asks the person to tap again when the laptop could not be reached', async () => {
    enrolledStationTablet()
    const station = useStationStore()
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => {
        throw new TypeError('Failed to fetch')
      }),
    )

    await station.advance(['a'], 'finished')

    expect(station.failureKey).toBe('station.actionNotReached')
  })
})
