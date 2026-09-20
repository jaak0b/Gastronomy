import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useAdminStationsStore } from '../../src/stores/admin/stations'

const BACKEND_STATION = {
  stationId: '11111111-1111-1111-1111-111111111111',
  name: 'Küche',
  sortOrder: 1,
  isActive: true,
  hasDevice: false,
  isAtTheFestival: true,
}

interface RecordedCall {
  url: string
  method: string
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
