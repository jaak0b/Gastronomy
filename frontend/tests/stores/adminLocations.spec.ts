import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useAdminLocationsStore } from '../../src/stores/admin/locations'

const BACKEND_LOCATION = {
  locationId: '11111111-1111-1111-1111-111111111111',
  name: 'Küche',
  sortOrder: 1,
  slipLanguage: 'de',
  isActive: true,
  accessKey: 'key-kueche',
  breakGlassUrl: 'http://192.168.1.20:5000/s/key-kueche',
  transportKind: 'Mock',
  host: null,
  port: 9100,
  isEnabled: true,
  isOnline: true,
  isPaperEnd: false,
  isCoverOpen: false,
  isFaulty: false,
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
    const calls = stubFetch(() => ({ status: 200, payload: { locations: [BACKEND_LOCATION] } }))
    const locations = useAdminLocationsStore()
    await locations.load()

    await locations.save({
      locationId: locations.locations[0].locationId,
      name: 'Küche hinten',
      sortOrder: 1,
      slipLanguage: 'de',
    })

    const write = calls.find((call) => call.method !== 'GET')
    expect(write).toEqual({
      url: `/api/admin/locations/${BACKEND_LOCATION.locationId}`,
      method: 'PUT',
    })
  })

})
