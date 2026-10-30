import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { fireHubEvent, forgetHubEvents } from '../../support/hubConnection'
import { useConnectionStore } from '../../../src/shared/stores/connection'
import { useOrderOverviewStore } from '../../../src/phone/stores/orderOverview'
import { answer, emptyAnswer, inTurn, noConnection, stubLaptop } from '../../support/laptop'
import { TABLE_PATH, TABLE_REPORT, aWaiterPhone, deferredResponse } from './openItemsFixture'

vi.mock('@microsoft/signalr', async () => (await import('../../support/hubConnection')).signalrModuleFake())

const EVERY_ORDER = { orderStatus: null, paymentStatus: null, includeCancelled: true }

beforeEach(() => {
  setActivePinia(createPinia())
  localStorage.clear()
  forgetHubEvents()
  aWaiterPhone()
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('the orders of a table the waiter opens to look at', () => {
  it('asks the laptop for every order of the table at first', async () => {
    const laptop = stubLaptop().answers('POST', TABLE_PATH, answer(TABLE_REPORT))
    const orderOverview = useOrderOverviewStore()
    orderOverview.openTable('table-12')

    await orderOverview.load()

    expect(laptop.bodiesSentTo('POST', TABLE_PATH)).toEqual([EVERY_ORDER])
    expect(orderOverview.report).toEqual(TABLE_REPORT)
  })

  it('asks again with what the waiter picked in the filter', async () => {
    const laptop = stubLaptop().answers('POST', TABLE_PATH, answer(TABLE_REPORT))
    const orderOverview = useOrderOverviewStore()
    orderOverview.openTable('table-12')

    await orderOverview.changeFilter({ orderStatus: 'open', paymentStatus: 'notPaid', includeCancelled: false })

    expect(laptop.bodiesSentTo('POST', TABLE_PATH)).toEqual([
      { orderStatus: 'open', paymentStatus: 'notPaid', includeCancelled: false },
    ])
  })

  it('keeps the newest answer when an older load answers after it', async () => {
    const older = deferredResponse()
    const newer = deferredResponse()
    stubLaptop().answers('POST', TABLE_PATH, inTurn(() => older.promise, () => newer.promise))
    const orderOverview = useOrderOverviewStore()
    orderOverview.openTable('table-12')

    const firstLoad = orderOverview.load()
    const newestLoad = orderOverview.changeFilter({ ...EVERY_ORDER, paymentStatus: 'paid' })
    newer.answerWith({ ...TABLE_REPORT, orders: [] })
    await newestLoad
    older.answerWith(TABLE_REPORT)
    await firstLoad

    expect(orderOverview.report?.orders).toEqual([])
  })

  it('says the table could not be loaded when the laptop does not know it', async () => {
    stubLaptop().answers('POST', TABLE_PATH, emptyAnswer(404))
    const orderOverview = useOrderOverviewStore()
    orderOverview.openTable('table-12')

    await orderOverview.load()

    expect(orderOverview.loadFailureKey).toBe('phone.openItems.errors.tableCouldNotBeLoaded')
  })

  it('says the laptop could not be reached when no answer came', async () => {
    stubLaptop().answers('POST', TABLE_PATH, noConnection())
    const orderOverview = useOrderOverviewStore()
    orderOverview.openTable('table-12')

    await orderOverview.load()

    expect({ report: orderOverview.report, failure: orderOverview.loadFailureKey }).toEqual({
      report: null,
      failure: 'phone.openItems.errors.loadFailed',
    })
  })

  it('starts again from every order when the waiter closes the table', async () => {
    stubLaptop().answers('POST', TABLE_PATH, answer(TABLE_REPORT))
    const orderOverview = useOrderOverviewStore()
    orderOverview.openTable('table-12')
    await orderOverview.changeFilter({ ...EVERY_ORDER, orderStatus: 'fulfilled' })

    orderOverview.closeTable()

    expect({ table: orderOverview.tableId, filter: orderOverview.filter, report: orderOverview.report }).toEqual({
      table: null,
      filter: EVERY_ORDER,
      report: null,
    })
  })
})

describe('the orders of a table on a phone that follows the laptop', () => {
  it('loads the table once the laptop has put the phone into its groups', async () => {
    const laptop = stubLaptop().answers('POST', TABLE_PATH, answer(TABLE_REPORT))
    const orderOverview = useOrderOverviewStore()
    orderOverview.listen()
    orderOverview.openTable('table-12')

    await useConnectionStore().connect({ deviceToken: 'token-here' })

    await vi.waitFor(() => expect(laptop.urls()).toEqual([TABLE_PATH]))
  })

  it('loads the table again with the picked filter when something happens to an order', async () => {
    const laptop = stubLaptop().answers('POST', TABLE_PATH, answer(TABLE_REPORT))
    const orderOverview = useOrderOverviewStore()
    orderOverview.listen()
    orderOverview.openTable('table-12')
    await useConnectionStore().connect({ deviceToken: 'token-here' })
    await orderOverview.changeFilter({ ...EVERY_ORDER, paymentStatus: 'paid' })
    laptop.forgetCalls()

    fireHubEvent('OrdersChanged')

    await vi.waitFor(() =>
      expect(laptop.bodiesSentTo('POST', TABLE_PATH)).toEqual([{ ...EVERY_ORDER, paymentStatus: 'paid' }]),
    )
  })
})

describe('what the order overview knows about its own loading', () => {
  it('knows it has loaded once the laptop sent the table', async () => {
    stubLaptop().answers('POST', TABLE_PATH, answer(TABLE_REPORT))
    const orderOverview = useOrderOverviewStore()
    orderOverview.openTable('table-12')

    await orderOverview.load()

    expect({ hasLoaded: orderOverview.hasLoaded, loadFailed: orderOverview.loadFailed }).toEqual({
      hasLoaded: true,
      loadFailed: false,
    })
  })

  it('knows the load failed when the laptop cannot be reached', async () => {
    stubLaptop().answers('POST', TABLE_PATH, noConnection())
    const orderOverview = useOrderOverviewStore()
    orderOverview.openTable('table-12')

    await orderOverview.load()

    expect({ hasLoaded: orderOverview.hasLoaded, loadFailed: orderOverview.loadFailed }).toEqual({
      hasLoaded: false,
      loadFailed: true,
    })
  })
})
