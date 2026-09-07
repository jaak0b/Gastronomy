import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useCatalogStore } from '../../src/stores/catalog'
import { TOKEN_STORAGE_KEY } from '../../src/stores/session'

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
