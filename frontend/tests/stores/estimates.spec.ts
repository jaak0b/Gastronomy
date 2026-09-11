import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { fireHubEvent, forgetHubEvents } from '../support/hubConnection'
import { useEstimatesStore } from '../../src/stores/estimates'
import { useSessionStore } from '../../src/stores/session'
import { useConnectionStore } from '../../src/stores/connection'

vi.mock('@microsoft/signalr', async () => (await import('../support/hubConnection')).signalrModuleFake())

function enrolledPhone() {
  useSessionStore().deviceToken = 'token-here'
}

describe('the waiting times the phone asks the laptop for', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('keeps the queue of every station the laptop named', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(
        async () =>
          new Response(
            JSON.stringify({ stations: [{ stationId: 'station-kueche', queuedMinutes: 12 }] }),
            { status: 200 },
          ),
      ),
    )
    enrolledPhone()
    const estimates = useEstimatesStore()

    await estimates.load()

    expect(estimates.stations).toEqual([{ stationId: 'station-kueche', queuedMinutes: 12 }])
  })

  it('asks the laptop at its own address', async () => {
    const urls: string[] = []
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string) => {
        urls.push(url)
        return new Response(JSON.stringify({ stations: [] }), { status: 200 })
      }),
    )
    enrolledPhone()
    const estimates = useEstimatesStore()

    await estimates.load()

    expect(urls).toEqual(['/api/estimates'])
  })

  it('says the times are missing rather than showing a wrong one', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => {
        throw new TypeError('Failed to fetch')
      }),
    )
    enrolledPhone()
    const estimates = useEstimatesStore()

    await estimates.load()

    expect(estimates.loadFailed).toBe(true)
    expect(estimates.stations).toEqual([])
  })

  it('asks for nothing at all before the phone is set up', async () => {
    const fetched = vi.fn()
    vi.stubGlobal('fetch', fetched)
    const estimates = useEstimatesStore()

    await estimates.load()

    expect(fetched).not.toHaveBeenCalled()
  })
})

describe('the waiting times a phone follows while it takes orders', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    forgetHubEvents()
    useSessionStore().deviceToken = 'token-here'
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  async function listeningPhone(): Promise<string[]> {
    const urls: string[] = []
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string) => {
        urls.push(url)
        return new Response(JSON.stringify({ stations: [] }), { status: 200 })
      }),
    )
    useEstimatesStore().listen()
    await useConnectionStore().connect({ deviceToken: 'token-here' })
    urls.length = 0
    return urls
  }

  async function letTheReloadFinish(): Promise<void> {
    await new Promise((resolve) => setTimeout(resolve, 0))
  }

  it('loads again when a station worked off part of its queue', async () => {
    const urls = await listeningPhone()

    fireHubEvent('StationOrdersChanged', { stationId: 'station-kueche' })
    await letTheReloadFinish()

    expect(urls).toEqual(['/api/estimates'])
  })

  it('loads again when an item moves on in production', async () => {
    const urls = await listeningPhone()

    fireHubEvent('OrderStatusChanged')
    await letTheReloadFinish()

    expect(urls).toEqual(['/api/estimates'])
  })

  it('loads again when the menu changes', async () => {
    const urls = await listeningPhone()

    fireHubEvent('CatalogChanged')
    await letTheReloadFinish()

    expect(urls).toEqual(['/api/estimates'])
  })

  it('loads again when a station is renamed or switched off', async () => {
    const urls = await listeningPhone()

    fireHubEvent('StationsChanged')
    await letTheReloadFinish()

    expect(urls).toEqual(['/api/estimates'])
  })

  it('loads again when the connection comes back', async () => {
    const urls = await listeningPhone()

    await useConnectionStore().refetchAll()

    expect(urls).toEqual(['/api/estimates'])
  })

  it('is left alone once the phone has stopped listening', async () => {
    const urls: string[] = []
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string) => {
        urls.push(url)
        return new Response(JSON.stringify({ stations: [] }), { status: 200 })
      }),
    )
    const stopListening = useEstimatesStore().listen()
    await useConnectionStore().connect({ deviceToken: 'token-here' })
    stopListening()
    urls.length = 0

    fireHubEvent('StationsChanged')
    await letTheReloadFinish()

    expect(urls).toEqual([])
  })
})
