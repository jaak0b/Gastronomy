import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useOpenItemsStore } from '../../../src/phone/stores/openItems'
import { TOKEN_STORAGE_KEY, useSessionStore } from '../../../src/shared/stores/session'
import { stubLaptop, answer, inTurn } from '../../support/laptop'
import { OPEN_LIST, SETTLED, TABLE_REPORT, deferredResponse, laptopAnsweringInTurn, storeWithTheOpenList, REPORT_WITH_TWO_ORDERS } from './openItemsFixture'

vi.mock('@microsoft/signalr', async () => (await import('../../support/hubConnection')).signalrModuleFake())

describe('the table a waiter looks up', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('asks the laptop for one table under the exact name the waiter typed', async () => {
    const laptop = laptopAnsweringInTurn([answer(OPEN_LIST), answer(TABLE_REPORT)])
    localStorage.setItem(TOKEN_STORAGE_KEY, 'lookup.token-here')
    useSessionStore().deviceToken = 'token-here'
    const openItems = useOpenItemsStore()
    await openItems.load()

    openItems.openLookup('Tisch 12')
    await openItems.loadTableReport('Tisch 12')

    expect(laptop.calls[1].url).toBe('/api/open-items/table?tableName=Tisch%2012')
    expect(openItems.lookupReport).toEqual(TABLE_REPORT)
  })

  it('keeps the newest answer when an older lookup answers after it', async () => {
    const older = deferredResponse()
    const newer = deferredResponse()
    stubLaptop()
      .answersEverythingElse(answer(OPEN_LIST))
      .answers('GET', '/api/open-items/table', inTurn(() => older.promise, () => newer.promise))
    localStorage.setItem(TOKEN_STORAGE_KEY, 'lookup.token-here')
    useSessionStore().deviceToken = 'token-here'
    const openItems = useOpenItemsStore()
    await openItems.load()

    openItems.openLookup('Tisch 12')
    const firstLookup = openItems.loadTableReport('Tisch 12')
    const newestLookup = openItems.loadTableReport('Tisch 12')
    newer.answerWith({ ...TABLE_REPORT, tableName: 'Tisch 3' })
    await newestLookup
    older.answerWith(TABLE_REPORT)
    await firstLookup

    expect(openItems.lookupReport?.tableName).toBe('Tisch 3')
  })

  it('clears the shown orders when the lookup fails, so a stale table is never trusted', async () => {
    const { openItems } = await storeWithTheOpenList([
      answer(TABLE_REPORT),
      () => {
        throw new TypeError('the laptop cannot be reached')
      },
    ])
    openItems.openLookup('Tisch 12')
    await openItems.loadTableReport('Tisch 12')

    await openItems.loadTableReport('Tisch 12')

    expect(openItems.lookupReport).toBeNull()
    expect(openItems.lookupFailed).toBe(true)
  })

  it('keeps a closed lookup closed when a slow answer arrives afterwards', async () => {
    const slow = deferredResponse()
    stubLaptop()
      .answersEverythingElse(answer(OPEN_LIST))
      .answers('GET', '/api/open-items/table', () => slow.promise)
    localStorage.setItem(TOKEN_STORAGE_KEY, 'lookup.token-here')
    useSessionStore().deviceToken = 'token-here'
    const openItems = useOpenItemsStore()
    await openItems.load()

    openItems.openLookup('Tisch 12')
    const lookup = openItems.loadTableReport('Tisch 12')
    openItems.closeLookup()
    slow.answerWith(TABLE_REPORT)
    await lookup

    expect(openItems.lookupReport).toBeNull()
    expect(openItems.isLookingUp).toBe(false)
  })

  it('reads the ticked positions from the lookup while one is shown', async () => {
    const { openItems } = await storeWithTheOpenList([])
    openItems.openLookup('Tisch 12')
    openItems.lookupReport = TABLE_REPORT

    openItems.toggleItem('lookup-open')

    expect(openItems.selectedItemIds).toEqual(['lookup-open'])
    expect(openItems.selectedTotalCents).toBe(400)
  })

  it('takes no tick from a settled position in the lookup', async () => {
    const { openItems } = await storeWithTheOpenList([])
    openItems.openLookup('Tisch 12')
    openItems.lookupReport = TABLE_REPORT

    openItems.toggleItem('lookup-settled')

    expect(openItems.selectedItemIds).toEqual([])
  })

  it('empties the selection when the lookup opens and when it closes', async () => {
    const { openItems } = await storeWithTheOpenList([])
    openItems.toggleItem('item-1')

    openItems.openLookup('Tisch 12')

    expect(openItems.selectedItemIds).toEqual([])

    openItems.lookupReport = TABLE_REPORT
    openItems.toggleItem('lookup-open')

    expect(openItems.selectedItemIds).toEqual(['lookup-open'])

    openItems.closeLookup()

    expect(openItems.selectedItemIds).toEqual([])
  })

  it('asks the laptop for the looked-up table again after settling, so the colours stay true', async () => {
    const { openItems, laptop } = await storeWithTheOpenList([
      answer(TABLE_REPORT),
      answer(SETTLED),
      answer(OPEN_LIST),
      answer(TABLE_REPORT),
    ])
    openItems.openLookup('Tisch 12')
    await openItems.loadTableReport('Tisch 12')
    openItems.toggleItem('lookup-open')

    await openItems.settle(400, null, 'cash')

    expect(laptop.calls.map((call) => call.url)).toEqual([
      '/api/open-items',
      '/api/open-items/table?tableName=Tisch%2012',
      '/api/open-items/settle',
      '/api/open-items',
      '/api/open-items/table?tableName=Tisch%2012',
    ])
  })
})

describe('taking a whole order from the table a waiter looks up', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('ticks exactly the unsettled items of the order and keeps the other ticks of the table', async () => {
    const { openItems } = await storeWithTheOpenList([])
    openItems.openLookup('Tisch 12')
    openItems.lookupReport = REPORT_WITH_TWO_ORDERS
    openItems.toggleItem('b-open')

    openItems.setWholeOrder('Tisch 12', REPORT_WITH_TWO_ORDERS.orders[0], true)

    expect(openItems.selectedItemIds).toEqual(['b-open', 'a-open-1', 'a-open-2'])
  })

  it('unticks exactly the unsettled items of the order when it is not wanted any more', async () => {
    const { openItems } = await storeWithTheOpenList([])
    openItems.openLookup('Tisch 12')
    openItems.lookupReport = REPORT_WITH_TWO_ORDERS
    openItems.toggleItem('b-open')
    openItems.setWholeOrder('Tisch 12', REPORT_WITH_TWO_ORDERS.orders[0], true)

    openItems.setWholeOrder('Tisch 12', REPORT_WITH_TWO_ORDERS.orders[0], false)

    expect(openItems.selectedItemIds).toEqual(['b-open'])
  })
})
