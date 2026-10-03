import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useAdminStationsStore } from '../../../src/admin/stores/stations'
import { stubLaptopAnswering, answer, stubLaptop, type StubbedLaptop } from '../../support/laptop'

const BACKEND_STATION = {
  stationId: '11111111-1111-1111-1111-111111111111',
  name: 'Küche',
  sortOrder: 1,
  isActive: true,
  hasDevice: false,
  isAtAnyFestival: true,
}

const ZELT = {
  stationId: '22222222-2222-2222-2222-222222222222',
  name: 'Zelt',
  sortOrder: 2,
  isActive: true,
  hasDevice: false,
  isAtAnyFestival: false,
}

function laptopReplyingBy(
  responder: (url: string) => { status: number; payload: unknown },
): StubbedLaptop {
  return stubLaptop().answersEverythingElse((call) => {
    const { status, payload } = responder(call.url)
    return answer(payload, status)(call)
  })
}

describe('the station list the admin configures', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('updates an edited station instead of creating a second one beside it', async () => {
    const laptop = laptopReplyingBy(() => ({ status: 200, payload: { stations: [BACKEND_STATION] } }))
    const stations = useAdminStationsStore()
    await stations.load()

    await stations.save({
      stationId: stations.stations[0].stationId,
      name: 'Küche hinten',
      sortOrder: 1,
    })

    const write = laptop.calls.find((call) => call.method !== 'GET')
    expect(write).toMatchObject({
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
    stubLaptopAnswering((_url, method) => (method === 'GET' ? { stations: [BACKEND_STATION] } : ZELT))
    const stations = useAdminStationsStore()

    const created = await stations.create({ name: 'Zelt', sortOrder: 2 })

    expect(created).toEqual({ kind: 'ok', value: ZELT })
  })

  it('takes the created station from the answer instead of reading the whole list again', async () => {
    const laptop = stubLaptopAnswering((_url, method) =>
      method === 'GET' ? { stations: [BACKEND_STATION] } : ZELT,
    )
    const stations = useAdminStationsStore()

    await stations.create({ name: 'Zelt', sortOrder: 2 })

    expect(laptop.calls.map((call) => call.method)).toEqual(['POST'])
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
    stubLaptopAnswering(
      (_url, method) =>
        method === 'GET'
          ? { stations: [BACKEND_STATION] }
          : {
              code: 'ValidationFailed',
              messageKey: 'errors.admin.stations.nameMissing',
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
      message: { key: 'errors.admin.stations.nameMissing', parameters: {}, count: null },
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
              messageKey: 'errors.admin.stations.nameMissing',
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
      message: { key: 'errors.admin.stations.nameMissing', parameters: {}, count: null },
    })
  })
})

const CREATED_STATION = {
  stationId: 'station-neu',
  name: 'Zelt',
  sortOrder: 2,
  isActive: true,
  hasDevice: false,
  isAtAnyFestival: false,
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
