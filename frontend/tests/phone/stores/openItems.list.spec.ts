import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { fireHubEvent, forgetHubEvents } from '../../support/hubConnection'
import { useConnectionStore } from '../../../src/shared/stores/connection'
import { useOpenItemsStore } from '../../../src/phone/stores/openItems'
import { TOKEN_STORAGE_KEY, useSessionStore } from '../../../src/shared/stores/session'
import { stubLaptop, answer } from '../../support/laptop'
import { OPEN_LIST, EMPTY_LIST, TABLE_REPORT, laptopAnsweringInTurn, storeWithTheOpenList, REPORT_WITH_TWO_ORDERS } from './openItemsFixture'

vi.mock('@microsoft/signalr', async () => (await import('../../support/hubConnection')).signalrModuleFake())

describe('the list of what the tables still owe', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('holds every table with something open', async () => {
    const { openItems } = await storeWithTheOpenList([])

    expect(openItems.tables).toHaveLength(1)
    expect(openItems.hasLoaded).toBe(true)
  })

  it('knows nothing about the tables until the first list has arrived', () => {
    const openItems = useOpenItemsStore()

    expect(openItems.hasLoaded).toBe(false)
  })

  it('fetches the table names for the ordering screen on their own address', async () => {
    const laptop = laptopAnsweringInTurn([answer({ tableNames: ['Tisch 12', 'Tisch 3'] })])
    localStorage.setItem(TOKEN_STORAGE_KEY, 'lookup.token-here')
    useSessionStore().deviceToken = 'token-here'
    const openItems = useOpenItemsStore()

    await openItems.loadTableNames()

    expect(laptop.calls[0].url).toBe('/api/open-items/table-names')
    expect(openItems.knownTableNames).toEqual(['Tisch 12', 'Tisch 3'])
  })

  it('says how many items are missing from the list, so nothing disappears in silence', async () => {
    laptopAnsweringInTurn([answer({ tables: [], itemsWithoutAnOrderCount: 2 })])
    localStorage.setItem(TOKEN_STORAGE_KEY, 'lookup.token-here')
    useSessionStore().deviceToken = 'token-here'
    const openItems = useOpenItemsStore()

    await openItems.load()

    expect(openItems.itemsWithoutAnOrderCount).toBe(2)
  })

  it('says so when the laptop could not be reached, so nobody trusts a stale list', async () => {
    laptopAnsweringInTurn([
      () => {
        throw new TypeError('the laptop cannot be reached')
      },
    ])
    localStorage.setItem(TOKEN_STORAGE_KEY, 'lookup.token-here')
    useSessionStore().deviceToken = 'token-here'
    const openItems = useOpenItemsStore()

    await openItems.load()

    expect(openItems.loadFailed).toBe(true)
  })

  it('adds up what the waiter ticked, so the cash can be counted out', async () => {
    const { openItems } = await storeWithTheOpenList([])

    openItems.toggleItem('item-1')

    expect(openItems.selectedTotalCents).toBe(350)
  })
})

describe('the open list a phone follows', () => {
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
    const laptop = stubLaptop().answersEverythingElse(answer(EMPTY_LIST))
    useOpenItemsStore().listen()
    await useConnectionStore().connect({ deviceToken: 'token-here' })
    laptop.forgetCalls()

    fireHubEvent('ConfigurationChanged')

    await vi.waitFor(() => expect(laptop.urls()).toEqual(['/api/open-items']))
  })

  it('loads the looked-up table again when the station board changes', async () => {
    const laptop = stubLaptop()
      .answersEverythingElse(answer(EMPTY_LIST))
      .answers('GET', '/api/open-items/table', answer(TABLE_REPORT))
    const openItems = useOpenItemsStore()
    openItems.listen()
    openItems.openLookup('Tisch 12')
    await openItems.loadTableReport('Tisch 12')
    await useConnectionStore().connect({ deviceToken: 'token-here' })
    laptop.forgetCalls()

    fireHubEvent('OrdersChanged')

    await vi.waitFor(() =>
      expect(laptop.urls()).toEqual([
        '/api/open-items',
        '/api/open-items/table?tableName=Tisch%2012',
      ]),
    )
  })
})

describe('taking a whole order while the plain list of tables is shown', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('ignores the order of another table while a different table holds the selection', async () => {
    const { openItems } = await storeWithTheOpenList([])
    openItems.toggleItem('item-1')

    openItems.setWholeOrder('Tisch 99', REPORT_WITH_TWO_ORDERS.orders[0], true)

    expect(openItems.selectedItemIds).toEqual(['item-1'])
  })
})

describe('the reload a waiter asks for on the open items screen', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('fetches the whole list again while no table is looked up', async () => {
    const { openItems, laptop } = await storeWithTheOpenList([answer(OPEN_LIST)])

    await openItems.reloadTheActiveView()

    expect(laptop.calls.map((call) => call.url)).toEqual(['/api/open-items', '/api/open-items'])
  })

  it('fetches only the looked up table again while one is looked up', async () => {
    const { openItems, laptop } = await storeWithTheOpenList([answer(TABLE_REPORT)])
    openItems.openLookup('Tisch 12')

    await openItems.reloadTheActiveView()

    expect(laptop.calls.map((call) => call.url)).toEqual([
      '/api/open-items',
      '/api/open-items/table?tableName=Tisch%2012',
    ])
  })
})
