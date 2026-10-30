import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { fireHubEvent, forgetHubEvents } from '../../support/hubConnection'
import { useCatalogStore } from '../../../src/phone/stores/catalog'
import { useConnectionStore } from '../../../src/shared/stores/connection'
import { useSessionStore, TOKEN_STORAGE_KEY } from '../../../src/shared/stores/session'
import { aHold, stubLaptop, answer, heldUntil, inTurn, noConnection } from '../../support/laptop'

vi.mock('@microsoft/signalr', async () => (await import('../../support/hubConnection')).signalrModuleFake())

const FULL_CATALOG = {
  festival: null,
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
      isQueueIndependent: false,
    },
  ],
  stations: [{ id: 'station-kueche', name: 'Küche', sortOrder: 1 }],
}

function laptopAnswers(payload: unknown) {
  stubLaptop().answersEverythingElse(answer(payload))
}

describe('the catalog on the phone', () => {
  beforeEach(() => {
    localStorage.setItem(TOKEN_STORAGE_KEY, 'lookup.token-here')
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
    laptopAnswers({ festival: null, categories: [], items: [], stations: [] })
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

describe('what the catalog on the phone knows about its own loading', () => {
  beforeEach(() => {
    localStorage.setItem(TOKEN_STORAGE_KEY, 'lookup.token-here')
    setActivePinia(createPinia())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
    localStorage.clear()
  })

  it('knows it has loaded once the laptop sent a catalog', async () => {
    laptopAnswers(FULL_CATALOG)
    const catalog = useCatalogStore()

    await catalog.load()

    expect({ hasLoaded: catalog.hasLoaded, loadFailed: catalog.loadFailed }).toEqual({
      hasLoaded: true,
      loadFailed: false,
    })
  })

  it('knows the load failed when the laptop cannot be reached', async () => {
    stubLaptop().answersEverythingElse(noConnection())
    const catalog = useCatalogStore()

    await catalog.load()

    expect({ hasLoaded: catalog.hasLoaded, loadFailed: catalog.loadFailed }).toEqual({
      hasLoaded: false,
      loadFailed: true,
    })
  })

  it('forgets the failure once the laptop answers again', async () => {
    stubLaptop().answersEverythingElse(inTurn(noConnection(), answer(FULL_CATALOG)))
    const catalog = useCatalogStore()
    await catalog.load()

    await catalog.load()

    expect({ hasLoaded: catalog.hasLoaded, loadFailed: catalog.loadFailed }).toEqual({
      hasLoaded: true,
      loadFailed: false,
    })
  })
})

describe('the answers of the catalog on the phone that arrive out of turn', () => {
  beforeEach(() => {
    localStorage.setItem(TOKEN_STORAGE_KEY, 'lookup.token-here')
    setActivePinia(createPinia())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
    localStorage.clear()
  })

  it('ignore a failure that arrives after a newer load succeeded', async () => {
    const holdTheFailure = aHold()
    stubLaptop().answersEverythingElse(
      inTurn(heldUntil(holdTheFailure.released, noConnection()), answer(FULL_CATALOG)),
    )
    const catalog = useCatalogStore()
    const olderLoad = catalog.load()
    await catalog.load()

    holdTheFailure.release()
    await olderLoad

    expect({ hasLoaded: catalog.hasLoaded, loadFailed: catalog.loadFailed }).toEqual({
      hasLoaded: true,
      loadFailed: false,
    })
  })

  it('ignore a success that arrives after a newer load failed', async () => {
    const holdTheSuccess = aHold()
    stubLaptop().answersEverythingElse(
      inTurn(heldUntil(holdTheSuccess.released, answer(FULL_CATALOG)), noConnection()),
    )
    const catalog = useCatalogStore()
    const olderLoad = catalog.load()
    await catalog.load()

    holdTheSuccess.release()
    await olderLoad

    expect({ hasLoaded: catalog.hasLoaded, loadFailed: catalog.loadFailed, categories: catalog.catalog.categories }).toEqual({
      hasLoaded: false,
      loadFailed: true,
      categories: [],
    })
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
    const laptop = stubLaptop().answersEverythingElse(answer(FULL_CATALOG))
    useCatalogStore().listen()
    await useConnectionStore().connect({ deviceToken: 'token-here' })
    laptop.forgetCalls()

    fireHubEvent('ConfigurationChanged')

    await vi.waitFor(() => expect(laptop.urls()).toEqual(['/api/catalog']))
  })
})
