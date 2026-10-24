import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useOpenItemsStore } from '../../../src/phone/stores/openItems'
import { stubLaptop, answer, inTurn } from '../../support/laptop'
import {
  REPORT_AFTER_THE_NEW_ORDER,
  REPORT_WITH_THE_NEW_ITEM_SETTLED,
  TABLE_PATH,
  aWaiterPhone,
  deferredResponse,
  laptopAnsweringInTurn,
} from './openItemsFixture'

vi.mock('@microsoft/signalr', async () => (await import('../../support/hubConnection')).signalrModuleFake())

describe('a table opened with the items of an order just sent ticked', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    aWaiterPhone()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('ticks nothing before the table has loaded', () => {
    laptopAnsweringInTurn([answer(REPORT_AFTER_THE_NEW_ORDER)])
    const openItems = useOpenItemsStore()

    openItems.openTableAndSelectItemsOnceLoaded('table-12', ['new-open', 'new-settled'])

    expect({ table: openItems.tableId, ticked: openItems.selectedItemIds }).toEqual({
      table: 'table-12',
      ticked: [],
    })
  })

  it('ticks the items of the order that are still open once the table has loaded, and leaves older ones unticked', async () => {
    laptopAnsweringInTurn([answer(REPORT_AFTER_THE_NEW_ORDER)])
    const openItems = useOpenItemsStore()
    openItems.openTableAndSelectItemsOnceLoaded('table-12', ['new-open', 'new-settled'])

    await openItems.load()

    expect(openItems.selectedItemIds).toEqual(['new-open'])
  })

  it('ticks from the newest answer when an older load answers after it', async () => {
    const older = deferredResponse()
    const newer = deferredResponse()
    stubLaptop().answers('GET', TABLE_PATH, inTurn(() => older.promise, () => newer.promise))
    const openItems = useOpenItemsStore()
    openItems.openTableAndSelectItemsOnceLoaded('table-12', ['new-open'])

    const firstLoad = openItems.load()
    const newestLoad = openItems.load()
    newer.answerWith(REPORT_WITH_THE_NEW_ITEM_SETTLED)
    await newestLoad
    older.answerWith(REPORT_AFTER_THE_NEW_ORDER)
    await firstLoad

    expect(openItems.selectedItemIds).toEqual([])
  })

  it('ticks the handed over items only once, so the table opened again starts with nothing ticked', async () => {
    laptopAnsweringInTurn([answer(REPORT_AFTER_THE_NEW_ORDER), answer(REPORT_AFTER_THE_NEW_ORDER)])
    const openItems = useOpenItemsStore()
    openItems.openTableAndSelectItemsOnceLoaded('table-12', ['new-open'])
    await openItems.load()
    openItems.closeTable()

    openItems.openTable('table-12')
    await openItems.load()

    expect(openItems.selectedItemIds).toEqual([])
  })

  it('ticks the new items when a load of the same table started before the handover answers first', async () => {
    const before = deferredResponse()
    const after = deferredResponse()
    stubLaptop().answers('GET', TABLE_PATH, inTurn(() => before.promise, () => after.promise))
    const openItems = useOpenItemsStore()
    openItems.openTable('table-12')
    const loadBeforeTheHandover = openItems.load()

    openItems.openTableAndSelectItemsOnceLoaded('table-12', ['new-open'])
    before.answerWith({ ...REPORT_AFTER_THE_NEW_ORDER, orders: [REPORT_AFTER_THE_NEW_ORDER.orders[0]] })
    await loadBeforeTheHandover
    const loadAfterTheHandover = openItems.load()
    after.answerWith(REPORT_AFTER_THE_NEW_ORDER)
    await loadAfterTheHandover

    expect(openItems.selectedItemIds).toEqual(['new-open'])
  })
})
