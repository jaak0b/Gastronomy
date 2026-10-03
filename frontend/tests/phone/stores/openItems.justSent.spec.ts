import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useOpenItemsStore } from '../../../src/phone/stores/openItems'
import { TOKEN_STORAGE_KEY, useSessionStore } from '../../../src/shared/stores/session'
import { stubLaptop, answer, inTurn } from '../../support/laptop'
import { EMPTY_LIST, deferredResponse, storeWithTheOpenList, REPORT_AFTER_THE_NEW_ORDER, REPORT_WITH_THE_NEW_ITEM_SETTLED } from './openItemsFixture'

vi.mock('@microsoft/signalr', async () => (await import('../../support/hubConnection')).signalrModuleFake())

describe('a table opened with the items of an order just sent ticked', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('ticks nothing before the table has loaded', async () => {
    const { openItems } = await storeWithTheOpenList([answer(REPORT_AFTER_THE_NEW_ORDER)])

    openItems.openTableAndSelectItemsOnceLoaded('Tisch 12', ['new-open', 'new-settled'])

    expect({ table: openItems.lookupName, ticked: openItems.selectedItemIds }).toEqual({
      table: 'Tisch 12',
      ticked: [],
    })
  })

  it('ticks the items of the order that are still open once the table has loaded, and leaves older ones unticked', async () => {
    const { openItems } = await storeWithTheOpenList([answer(REPORT_AFTER_THE_NEW_ORDER)])
    openItems.openTableAndSelectItemsOnceLoaded('Tisch 12', ['new-open', 'new-settled'])

    await openItems.loadTableReport('Tisch 12')

    expect(openItems.selectedItemIds).toEqual(['new-open'])
  })

  it('ticks from the newest answer when an older lookup answers after it', async () => {
    const older = deferredResponse()
    const newer = deferredResponse()
    stubLaptop()
      .answersEverythingElse(answer(EMPTY_LIST))
      .answers('GET', '/api/open-items/table', inTurn(() => older.promise, () => newer.promise))
    localStorage.setItem(TOKEN_STORAGE_KEY, 'lookup.token-here')
    useSessionStore().deviceToken = 'token-here'
    const openItems = useOpenItemsStore()
    openItems.openTableAndSelectItemsOnceLoaded('Tisch 12', ['new-open'])

    const firstLookup = openItems.loadTableReport('Tisch 12')
    const newestLookup = openItems.loadTableReport('Tisch 12')
    newer.answerWith(REPORT_WITH_THE_NEW_ITEM_SETTLED)
    await newestLookup
    older.answerWith(REPORT_AFTER_THE_NEW_ORDER)
    await firstLookup

    expect(openItems.selectedItemIds).toEqual([])
  })

  it('ticks the handed over items only once, so the table opened again starts with nothing ticked', async () => {
    const { openItems } = await storeWithTheOpenList([answer(REPORT_AFTER_THE_NEW_ORDER)])
    openItems.openTableAndSelectItemsOnceLoaded('Tisch 12', ['new-open'])
    await openItems.loadTableReport('Tisch 12')
    openItems.closeLookup()

    openItems.openLookup('Tisch 12')
    await openItems.loadTableReport('Tisch 12')

    expect(openItems.selectedItemIds).toEqual([])
  })

  it('ticks the new items when a lookup of the same table started before the handover answers first', async () => {
    const before = deferredResponse()
    const after = deferredResponse()
    stubLaptop()
      .answersEverythingElse(answer(EMPTY_LIST))
      .answers('GET', '/api/open-items/table', inTurn(() => before.promise, () => after.promise))
    localStorage.setItem(TOKEN_STORAGE_KEY, 'lookup.token-here')
    useSessionStore().deviceToken = 'token-here'
    const openItems = useOpenItemsStore()
    openItems.openLookup('Tisch 12')
    const lookupBeforeTheHandover = openItems.loadTableReport('Tisch 12')

    openItems.openTableAndSelectItemsOnceLoaded('Tisch 12', ['new-open'])
    before.answerWith({ ...REPORT_AFTER_THE_NEW_ORDER, orders: [REPORT_AFTER_THE_NEW_ORDER.orders[0]] })
    await lookupBeforeTheHandover
    const lookupAfterTheHandover = openItems.loadTableReport('Tisch 12')
    after.answerWith(REPORT_AFTER_THE_NEW_ORDER)
    await lookupAfterTheHandover

    expect(openItems.selectedItemIds).toEqual(['new-open'])
  })
})
