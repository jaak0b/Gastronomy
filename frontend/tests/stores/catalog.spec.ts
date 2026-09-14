import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { fireHubEvent, forgetHubEvents } from '../support/hubConnection'
import { useCatalogStore } from '../../src/stores/catalog'
import { useConnectionStore } from '../../src/stores/connection'
import { useSessionStore, TOKEN_STORAGE_KEY } from '../../src/stores/session'

vi.mock('@microsoft/signalr', async () => (await import('../support/hubConnection')).signalrModuleFake())

const FULL_CATALOG = {
  categories: [
    { categoryId: 'category-essen', name: 'Essen', colourHex: '#FFEB3B', sortOrder: 1 },
  ],
  items: [
    {
      id: 'item-bratwurst',
      name: 'Bratwurst',
      categoryId: 'category-essen',
      priceCents: 350,
      sortOrder: 1,
      isAvailable: true,
      stationIds: ['station-kueche'],
      productionMinutes: null,
    },
  ],
  stations: [{ id: 'station-kueche', name: 'Küche', sortOrder: 1 }],
}

function laptopAnswers(payload: unknown) {
  vi.stubGlobal(
    'fetch',
    vi.fn(async () => new Response(JSON.stringify(payload), { status: 200 })),
  )
}

describe('the catalog on the phone', () => {
  beforeEach(() => {
    localStorage.setItem(TOKEN_STORAGE_KEY, 'token-here')
    setActivePinia(createPinia())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
    localStorage.clear()
  })

  it('groups the items the laptop sent under their categories', async () => {
    laptopAnswers(FULL_CATALOG)
    const catalog = useCatalogStore()

    await catalog.load()

    expect(catalog.groups.map((group) => group.category.name)).toEqual(['Essen'])
  })

  it('stands empty rather than breaking when the laptop sends no categories', async () => {
    laptopAnswers({ items: [], stations: [] })
    const catalog = useCatalogStore()

    await catalog.load()

    expect(catalog.groups).toEqual([])
  })

  it('stands empty rather than breaking when the laptop sends nothing usable at all', async () => {
    laptopAnswers({})
    const catalog = useCatalogStore()

    await catalog.load()

    expect(catalog.groups).toEqual([])
    expect(catalog.catalog.items).toEqual([])
    expect(catalog.stationName('station-kueche')).toBe('')
  })
})

describe('the catalog a phone follows while it is open', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    forgetHubEvents()
    useSessionStore().deviceToken = 'token-here'
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('loads again when the festival starts or stops', async () => {
    const urls: string[] = []
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string) => {
        urls.push(url)
        return new Response(JSON.stringify(FULL_CATALOG), { status: 200 })
      }),
    )
    useCatalogStore().listen()
    await useConnectionStore().connect({ deviceToken: 'token-here' })
    urls.length = 0

    fireHubEvent('FestivalChanged')

    await vi.waitFor(() => expect(urls).toEqual(['/api/catalog']))
  })
})
