import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { fireHubEvent, forgetHubEvents } from '../../support/hubConnection'
import { useConnectionStore } from '../../../src/shared/stores/connection'
import { useOpenItemsStore } from '../../../src/phone/stores/openItems'
import { answer, emptyAnswer, inTurn, noConnection, refusal, stubLaptop } from '../../support/laptop'
import {
  REPORT_WITH_TWO_ORDERS,
  SETTLED,
  TABLE_PATH,
  TABLE_REPORT,
  aWaiterPhone,
  deferredResponse,
  laptopAnsweringInTurn,
  storeShowingTisch12,
} from './openItemsFixture'

vi.mock('@microsoft/signalr', async () => (await import('../../support/hubConnection')).signalrModuleFake())

describe('the table a waiter opens to settle', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('asks the laptop for that one table by its id', async () => {
    const { openItems, laptop } = await storeShowingTisch12([])

    expect(laptop.urls()).toEqual([TABLE_PATH])
    expect(openItems.report).toEqual(TABLE_REPORT)
  })

  it('asks the laptop nothing while no table is open', async () => {
    const laptop = laptopAnsweringInTurn([answer(TABLE_REPORT)])
    aWaiterPhone()
    const openItems = useOpenItemsStore()

    await openItems.load()

    expect(laptop.urls()).toEqual([])
  })

  it('keeps the newest answer when an older load answers after it', async () => {
    const older = deferredResponse()
    const newer = deferredResponse()
    stubLaptop().answers('POST', TABLE_PATH, inTurn(() => older.promise, () => newer.promise))
    aWaiterPhone()
    const openItems = useOpenItemsStore()
    openItems.openTable('table-12')

    const firstLoad = openItems.load()
    const newestLoad = openItems.load()
    newer.answerWith({ ...TABLE_REPORT, tableName: 'Tisch 12a' })
    await newestLoad
    older.answerWith(TABLE_REPORT)
    await firstLoad

    expect(openItems.report?.tableName).toBe('Tisch 12a')
  })

  it('clears the shown orders when the laptop cannot be reached, so a stale table is never trusted', async () => {
    const { openItems } = await storeShowingTisch12([noConnection()])

    await openItems.load()

    expect(openItems.report).toBeNull()
    expect(openItems.loadFailureKey).toBe('phone.openItems.errors.loadFailed')
  })

  it('keeps what the waiter ticked while the laptop cannot be reached, and ticks it again once it answers', async () => {
    const { openItems } = await storeShowingTisch12([noConnection(), answer(TABLE_REPORT)])
    openItems.toggleItem('bier-open')
    await openItems.load()

    await openItems.load()

    expect(openItems.selectedItemIds).toEqual(['bier-open'])
  })

  it('says the table could not be loaded when the laptop does not know it', async () => {
    const { openItems } = await storeShowingTisch12([emptyAnswer(404)])
    openItems.toggleItem('bier-open')

    await openItems.load()

    expect({
      report: openItems.report,
      ticked: openItems.selectedItemIds,
      failure: openItems.loadFailureKey,
    }).toEqual({ report: null, ticked: [], failure: 'phone.openItems.errors.tableCouldNotBeLoaded' })
  })

  it('shows what the laptop says when it refuses to read the table', async () => {
    const { openItems } = await storeShowingTisch12([
      refusal('errors.settlement.cannotBeProcessed', { status: 400, code: 'ValidationFailed' }),
    ])

    await openItems.load()

    expect(openItems.loadFailureKey).toBe('errors.settlement.cannotBeProcessed')
  })

  it('keeps a closed table closed when a slow answer arrives afterwards', async () => {
    const slow = deferredResponse()
    stubLaptop().answers('POST', TABLE_PATH, () => slow.promise)
    aWaiterPhone()
    const openItems = useOpenItemsStore()
    openItems.openTable('table-12')
    const load = openItems.load()

    openItems.closeTable()
    slow.answerWith(TABLE_REPORT)
    await load

    expect({ table: openItems.tableId, report: openItems.report }).toEqual({ table: null, report: null })
  })

  it('adds up the positions the waiter ticked', async () => {
    const { openItems } = await storeShowingTisch12([])

    openItems.toggleItem('bier-open')

    expect(openItems.selectedTotalCents).toBe(400)
  })

  it('takes no tick from a settled position', async () => {
    const { openItems } = await storeShowingTisch12([])

    openItems.toggleItem('wasser-settled')

    expect(openItems.selectedItemIds).toEqual([])
  })

  it('empties the selection when another table is opened', async () => {
    const { openItems } = await storeShowingTisch12([])
    openItems.toggleItem('bier-open')

    openItems.openTable('table-3')

    expect(openItems.selectedItemIds).toEqual([])
  })

  it('empties the selection when the table is closed', async () => {
    const { openItems } = await storeShowingTisch12([])
    openItems.toggleItem('bier-open')

    openItems.closeTable()

    expect(openItems.selectedItemIds).toEqual([])
  })

  it('asks the laptop for the table again after settling, so the colours stay true', async () => {
    const { openItems, laptop } = await storeShowingTisch12([answer(SETTLED), answer(TABLE_REPORT)])
    openItems.toggleItem('bier-open')

    await openItems.settle(400, null, 'cash')

    expect(laptop.urls()).toEqual([TABLE_PATH, '/api/open-items/settle', TABLE_PATH])
  })
})

describe('taking a whole order from the open table', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('ticks exactly the unsettled items of the order and keeps the other ticks of the table', async () => {
    const { openItems } = await storeShowingTisch12([], REPORT_WITH_TWO_ORDERS)
    openItems.toggleItem('b-open')

    openItems.setWholeOrder(REPORT_WITH_TWO_ORDERS.orders[0], true)

    expect(openItems.selectedItemIds).toEqual(['b-open', 'a-open-1', 'a-open-2'])
  })

  it('unticks exactly the unsettled items of the order when it is not wanted any more', async () => {
    const { openItems } = await storeShowingTisch12([], REPORT_WITH_TWO_ORDERS)
    openItems.toggleItem('b-open')
    openItems.setWholeOrder(REPORT_WITH_TWO_ORDERS.orders[0], true)

    openItems.setWholeOrder(REPORT_WITH_TWO_ORDERS.orders[0], false)

    expect(openItems.selectedItemIds).toEqual(['b-open'])
  })

  it('ticks every unsettled item of the table at once', async () => {
    const { openItems } = await storeShowingTisch12([], REPORT_WITH_TWO_ORDERS)

    openItems.setWholeTable(true)

    expect(openItems.selectedItemIds).toEqual(['a-open-1', 'a-open-2', 'b-open'])
  })
})

describe('the open table a phone follows', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    forgetHubEvents()
    aWaiterPhone()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  async function aPhoneFollowingTisch12() {
    const laptop = stubLaptop().answers('POST', TABLE_PATH, answer(TABLE_REPORT))
    const openItems = useOpenItemsStore()
    openItems.listen()
    openItems.openTable('table-12')
    await useConnectionStore().connect({ deviceToken: 'token-here' })
    await vi.waitFor(() => expect(openItems.report).toEqual(TABLE_REPORT))
    laptop.forgetCalls()
    return laptop
  }

  it('loads the table once the laptop has put the phone into its groups', async () => {
    const laptop = stubLaptop().answers('POST', TABLE_PATH, answer(TABLE_REPORT))
    const openItems = useOpenItemsStore()
    openItems.listen()
    openItems.openTable('table-12')

    await useConnectionStore().connect({ deviceToken: 'token-here' })

    await vi.waitFor(() => expect(laptop.urls()).toEqual([TABLE_PATH]))
  })

  it('loads the table again when something happens to an order', async () => {
    const laptop = await aPhoneFollowingTisch12()

    fireHubEvent('OrdersChanged')

    await vi.waitFor(() => expect(laptop.urls()).toEqual([TABLE_PATH]))
  })

  it('loads the table again with the filter the waiter picked when something happens to an order', async () => {
    const laptop = await aPhoneFollowingTisch12()
    const picked = { orderStatus: 'open' as const, paymentStatus: 'paid' as const, includeCancelled: true }
    await useOpenItemsStore().changeFilter(picked)
    laptop.forgetCalls()

    fireHubEvent('OrdersChanged')

    await vi.waitFor(() => expect(laptop.bodiesSentTo('POST', TABLE_PATH)).toEqual([picked]))
  })

  it('applies only the newest answer when the waiter changes the filter while a load is on its way', async () => {
    const older = deferredResponse()
    const newer = deferredResponse()
    stubLaptop().answers('POST', TABLE_PATH, inTurn(() => older.promise, () => newer.promise))
    aWaiterPhone()
    const openItems = useOpenItemsStore()
    openItems.openTable('table-12')

    const firstLoad = openItems.load()
    const newestLoad = openItems.changeFilter({ orderStatus: 'fulfilled', paymentStatus: null, includeCancelled: false })
    newer.answerWith({ ...TABLE_REPORT, orders: [] })
    await newestLoad
    older.answerWith(TABLE_REPORT)
    await firstLoad

    expect(openItems.report?.orders).toEqual([])
  })

  it('loads the table again when the admin changes the plan, so a renamed table shows its new name', async () => {
    const laptop = await aPhoneFollowingTisch12()

    fireHubEvent('ConfigurationChanged')

    await vi.waitFor(() => expect(laptop.urls()).toEqual([TABLE_PATH]))
  })
})
