import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useAdminStationsStore } from '../../../src/admin/stores/stations'

const BACKEND_STATION = {
  stationId: '11111111-1111-1111-1111-111111111111',
  name: 'Küche',
  sortOrder: 1,
  isActive: true,
  hasDevice: false,
  isAtTheFestival: true,
}

const ZELT = {
  stationId: '22222222-2222-2222-2222-222222222222',
  name: 'Zelt',
  sortOrder: 2,
  isActive: true,
  hasDevice: false,
  isAtTheFestival: false,
}

interface RecordedCall {
  url: string
  method: string
}

interface Call {
  url: string
  method: string
  body: unknown
}

function stubFetch(responder: (url: string) => { status: number; payload: unknown }) {
  const calls: RecordedCall[] = []
  vi.stubGlobal(
    'fetch',
    vi.fn(async (url: string, init?: RequestInit) => {
      calls.push({ url, method: init?.method ?? 'GET' })
      const { status, payload } = responder(url)
      return new Response(JSON.stringify(payload), { status })
    }),
  )
  return calls
}

function answerWith(payloadFor: (url: string, method: string) => unknown, status = 200): Call[] {
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
      const isRead = method === 'GET'
      return new Response(JSON.stringify(payloadFor(url, method)), {
        status: isRead ? 200 : status,
      })
    }),
  )
  return calls
}

describe('the station list the admin configures', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('updates an edited station instead of creating a second one beside it', async () => {
    const calls = stubFetch(() => ({ status: 200, payload: { stations: [BACKEND_STATION] } }))
    const stations = useAdminStationsStore()
    await stations.load()

    await stations.save({
      stationId: stations.stations[0].stationId,
      name: 'Küche hinten',
      sortOrder: 1,
    })

    const write = calls.find((call) => call.method !== 'GET')
    expect(write).toEqual({
      url: `/api/admin/stations/${BACKEND_STATION.stationId}`,
      method: 'PUT',
    })
  })

})

describe('a new station', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('hands back the station the laptop created, so it can be picked right away', async () => {
    answerWith((_url, method) => (method === 'GET' ? { stations: [BACKEND_STATION] } : ZELT))
    const stations = useAdminStationsStore()

    const created = await stations.create({ name: 'Zelt', sortOrder: 2 })

    expect(created).toEqual({ kind: 'ok', value: ZELT })
  })

  it('takes the created station from the answer instead of reading the whole list again', async () => {
    const calls = answerWith((_url, method) =>
      method === 'GET' ? { stations: [BACKEND_STATION] } : ZELT,
    )
    const stations = useAdminStationsStore()

    await stations.create({ name: 'Zelt', sortOrder: 2 })

    expect(calls.map((call) => call.method)).toEqual(['POST'])
    expect(stations.stations).toEqual([ZELT])
  })

  it('stays in the list even when the list cannot be read again afterwards', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async (_url: string, init?: RequestInit) => {
        if ((init?.method ?? 'GET') === 'POST') {
          return new Response(JSON.stringify(ZELT), { status: 201 })
        }
        throw new TypeError('Failed to fetch')
      }),
    )
    const stations = useAdminStationsStore()

    await stations.create({ name: 'Zelt', sortOrder: 2 })

    expect(stations.stations.map((station) => station.name)).toEqual(['Zelt'])
  })

  it('keeps the reason the laptop refused it and appends nothing', async () => {
    answerWith(
      (_url, method) =>
        method === 'GET'
          ? { stations: [BACKEND_STATION] }
          : {
              code: 'ValidationFailed',
              messageKey: 'admin.stationNameMissing',
              parameters: {},
              details: null,
            },
      400,
    )
    const stations = useAdminStationsStore()
    await stations.load()

    const created = await stations.create({ name: '', sortOrder: 2 })

    expect(created).toEqual({
      kind: 'failed',
      message: { key: 'admin.stationNameMissing', parameters: {}, count: null },
    })
    expect(stations.stations).toEqual([BACKEND_STATION])
  })
})

describe('a station the laptop would not save', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  function refuseTheSave() {
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string, init?: RequestInit) => {
        if (init?.method === 'PUT') {
          return new Response(
            JSON.stringify({
              code: 'ValidationFailed',
              messageKey: 'admin.stationNameMissing',
              parameters: {},
              details: null,
            }),
            { status: 400 },
          )
        }
        return new Response(JSON.stringify({ stations: [BACKEND_STATION] }), { status: 200 })
      }),
    )
  }

  it('reports that the change did not happen', async () => {
    refuseTheSave()
    const stations = useAdminStationsStore()

    const wasSaved = await stations.save({
      stationId: BACKEND_STATION.stationId,
      name: '   ',
      sortOrder: 1,
    })

    expect(wasSaved.kind).toBe('failed')
  })

  it('holds the reason the laptop gave', async () => {
    refuseTheSave()
    const stations = useAdminStationsStore()

    const wasSaved = await stations.save({
      stationId: BACKEND_STATION.stationId,
      name: '   ',
      sortOrder: 1,
    })

    expect(wasSaved).toEqual({
      kind: 'failed',
      message: { key: 'admin.stationNameMissing', parameters: {}, count: null },
    })
  })
})

const CREATED_STATION = {
  stationId: 'station-neu',
  name: 'Zelt',
  sortOrder: 2,
  isActive: true,
  hasDevice: false,
  isAtTheFestival: false,
}

describe('a station created while a list read is on the way', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('stays in the list when the slower read answers first', async () => {
    let releaseThePost = (): void => {}
    const thePost = new Promise<Response>((carryOn) => {
      releaseThePost = () => carryOn(new Response(JSON.stringify(CREATED_STATION), { status: 201 }))
    })
    vi.stubGlobal(
      'fetch',
      vi.fn(async (_url: string, init?: RequestInit) =>
        (init?.method ?? 'GET') === 'POST'
          ? await thePost
          : new Response(JSON.stringify({ stations: [] }), { status: 200 }),
      ),
    )
    const stations = useAdminStationsStore()

    const created = stations.create({ name: 'Zelt', sortOrder: 2 })
    await stations.load()
    releaseThePost()
    await created

    expect(stations.stations.map((station) => station.stationId)).toEqual(['station-neu'])
  })

  it('does not show the created station in a list of another festival', async () => {
    let releaseThePost = (): void => {}
    const thePost = new Promise<Response>((carryOn) => {
      releaseThePost = () => carryOn(new Response(JSON.stringify(CREATED_STATION), { status: 201 }))
    })
    vi.stubGlobal(
      'fetch',
      vi.fn(async (_url: string, init?: RequestInit) =>
        (init?.method ?? 'GET') === 'POST'
          ? await thePost
          : new Response(JSON.stringify({ stations: [] }), { status: 200 }),
      ),
    )
    const stations = useAdminStationsStore()

    await stations.loadAtTheFestival('fest-1')
    const created = stations.create({ name: 'Zelt', sortOrder: 2 })
    await stations.loadAtTheFestival('fest-2')
    releaseThePost()
    await created

    expect(stations.stations).toEqual([])
  })
})
