import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useCatalogStore } from '../../src/stores/catalog'
import { useOrderStore } from '../../src/stores/order'
import { TOKEN_STORAGE_KEY } from '../../src/stores/session'
import { DRAFT_STORAGE_KEY, restoreDraft } from '../../src/core/draftCart'

const BRATWURST = {
  id: 'item-bratwurst',
  name: 'Bratwurst',
  categoryId: 'category-essen',
  priceCents: 350,
  sortOrder: 1,
  isAvailable: true,
  stationIds: ['station-kueche'],
  productionMinutes: null,
}

function catalogOf(festivalId: string | null) {
  return {
    festival: festivalId === null ? null : { festivalId, name: 'Sommerfest' },
    categories: [
      { categoryId: 'category-essen', name: 'Essen', colourHex: '#FFEB3B', sortOrder: 1 },
    ],
    items: [BRATWURST],
    stations: [{ id: 'station-kueche', name: 'Küche', sortOrder: 1 }],
  }
}

function laptopAnswers(payload: unknown): void {
  vi.stubGlobal(
    'fetch',
    vi.fn(async () => new Response(JSON.stringify(payload), { status: 200 })),
  )
}

function laptopCannotBeReached(): void {
  vi.stubGlobal(
    'fetch',
    vi.fn(async () => {
      throw new TypeError('the laptop is not there')
    }),
  )
}

function laptopRefuses(): void {
  vi.stubGlobal(
    'fetch',
    vi.fn(
      async () =>
        new Response(JSON.stringify({ code: 'ValidationFailed', messageKey: 'x' }), {
          status: 400,
        }),
    ),
  )
}

function addABratwurst(order: ReturnType<typeof useOrderStore>): void {
  order.addItem({
    catalogItemId: 'item-bratwurst',
    note: null,
    stationId: 'station-kueche',
    name: 'Bratwurst',
  })
}

describe('the order on the phone and the festival that is running', () => {
  beforeEach(() => {
    localStorage.setItem(TOKEN_STORAGE_KEY, 'token-here')
    setActivePinia(createPinia())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
    localStorage.clear()
  })

  it('carries the running festival as soon as the first line is added', async () => {
    laptopAnswers(catalogOf('fest-1'))
    const catalog = useCatalogStore()
    const order = useOrderStore()
    await catalog.load()

    addABratwurst(order)

    expect(order.draft.festivalId).toBe('fest-1')
    expect(restoreDraft().draft.festivalId).toBe('fest-1')
  })

  it('carries the running festival on a table name typed before any line', async () => {
    laptopAnswers(catalogOf('fest-1'))
    const catalog = useCatalogStore()
    const order = useOrderStore()
    await catalog.load()

    order.setTable('Tisch 4')

    expect(order.draft.festivalId).toBe('fest-1')
  })

  it('is thrown away without a word when the next catalog names another festival', async () => {
    laptopAnswers(catalogOf('fest-1'))
    const catalog = useCatalogStore()
    const order = useOrderStore()
    await catalog.load()
    addABratwurst(order)

    laptopAnswers(catalogOf('fest-2'))
    await catalog.load()

    expect(order.draft.lines).toEqual([])
    expect(order.draft.festivalId).toBe('fest-2')
    expect(order.draftWasLost).toBe(false)
  })

  it('survives a catalog that names the same festival again', async () => {
    laptopAnswers(catalogOf('fest-1'))
    const catalog = useCatalogStore()
    const order = useOrderStore()
    await catalog.load()
    addABratwurst(order)

    await catalog.load()

    expect(order.draft.lines).toHaveLength(1)
  })

  it('is left alone when the laptop cannot be reached', async () => {
    laptopAnswers(catalogOf('fest-1'))
    const catalog = useCatalogStore()
    const order = useOrderStore()
    await catalog.load()
    addABratwurst(order)

    laptopCannotBeReached()
    await catalog.load()

    expect(order.draft.lines).toHaveLength(1)
    expect(order.draft.festivalId).toBe('fest-1')
  })

  it('is left alone when the laptop refuses the catalog', async () => {
    laptopAnswers(catalogOf('fest-1'))
    const catalog = useCatalogStore()
    const order = useOrderStore()
    await catalog.load()
    addABratwurst(order)

    laptopRefuses()
    await catalog.load()

    expect(order.draft.lines).toHaveLength(1)
  })

  it('stays on the screen while it is frozen, even once another festival runs', async () => {
    laptopAnswers(catalogOf('fest-1'))
    const catalog = useCatalogStore()
    const order = useOrderStore()
    await catalog.load()
    addABratwurst(order)
    order.setTable('Tisch 4')
    laptopCannotBeReached()
    await order.send(false)
    expect(order.changesAreRefused).toBe(true)

    laptopAnswers(catalogOf('fest-2'))
    await catalog.load()

    expect(order.draft.lines).toHaveLength(1)
    expect(localStorage.getItem(DRAFT_STORAGE_KEY)).not.toBeNull()
  })
})
