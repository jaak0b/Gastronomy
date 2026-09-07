import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { fireHubEvent, forgetHubEvents } from '../support/hubConnection'

vi.mock('@microsoft/signalr', async () => (await import('../support/hubConnection')).signalrModuleFake())

const { useConnectionStore } = await import('../../src/stores/connection')
const { useCatalogStore } = await import('../../src/stores/catalog')
const { useStationStore } = await import('../../src/stores/station')
const { useSessionStore } = await import('../../src/stores/session')

const KITCHEN = { id: 'station-kueche', name: 'Kueche am Zelt' }

function stubTheLaptop(): string[] {
  const urls: string[] = []
  vi.stubGlobal(
    'fetch',
    vi.fn(async (url: string) => {
      urls.push(url)
      return new Response(
        JSON.stringify({ categories: [], items: [], stations: [], station: KITCHEN, slices: [] }),
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
