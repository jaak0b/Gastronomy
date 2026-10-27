import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { enableAutoUnmount, flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import OrderOverview from '../../../src/phone/views/OrderOverview.vue'
import { useOrderOverviewStore } from '../../../src/phone/stores/orderOverview'
import type { AppLanguage } from '../../../src/shared/core/deviceLanguage'
import { currentRoute, navigate } from '../../../src/shared/router/router'
import { TOKEN_STORAGE_KEY, useSessionStore } from '../../../src/shared/stores/session'
import { testPlugins } from '../../support/plugins'
import { answer, stubLaptop } from '../../support/laptop'

enableAutoUnmount(afterEach)

const SEARCH = '/api/open-items/tables/table-12/search'

function itemOfOrder141(orderItemId: string, itemName: string, state: string, settledAtUtc: string | null) {
  return {
    orderItemId,
    orderId: 'order-141',
    globalOrderNumber: 141,
    itemName,
    note: null,
    unitPriceCents: 400,
    orderedAtUtc: '2026-09-05T18:20:00Z',
    state,
    stateSetAtUtc: '2026-09-05T18:25:00Z',
    settledAtUtc,
    chargedPriceCents: settledAtUtc === null ? null : 400,
  }
}

const TISCH_12 = {
  tableId: 'table-12',
  tableName: 'Tisch 12',
  orders: [
    {
      orderId: 'order-141',
      globalOrderNumber: 141,
      createdAtUtc: '2026-09-05T18:20:00Z',
      staffMemberName: 'Anna',
      items: [
        itemOfOrder141('bier', 'Bier', 'open', null),
        itemOfOrder141('wasser', 'Wasser', 'handedOut', '2026-09-05T18:40:00Z'),
        itemOfOrder141('cola', 'Cola', 'cancelled', null),
      ],
    },
  ],
}

async function mountTheOrders(language: AppLanguage = 'de') {
  localStorage.setItem(TOKEN_STORAGE_KEY, 'lookup.token-here')
  const session = useSessionStore()
  session.deviceToken = 'token-here'
  session.language = language
  useOrderOverviewStore().openTable('table-12')
  const screen = mount(OrderOverview, { global: { plugins: testPlugins(language) }, attachTo: document.body })
  await flushPromises()
  return screen
}

type Screen = Awaited<ReturnType<typeof mountTheOrders>>

function markOf(screen: Screen, mark: 'paid-mark' | 'handed-out-mark', orderItemId: string): string[] {
  return screen.get(`[data-test="${mark}"][data-test-id="${orderItemId}"]`).classes()
}

async function tap(screen: Screen, selector: string): Promise<void> {
  await screen.get(selector).trigger('click')
  await flushPromises()
}

beforeEach(() => {
  setActivePinia(createPinia())
  localStorage.clear()
  document.body.innerHTML = ''
  navigate('/order-overview')
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('the orders of a table, shown to read', () => {
  it('heads the two columns with Bezahlt and Ausgegeben', async () => {
    stubLaptop().answers('POST', SEARCH, answer(TISCH_12))

    const screen = await mountTheOrders()

    expect([
      screen.get('[data-test="paid-heading"]').text(),
      screen.get('[data-test="handed-out-heading"]').text(),
    ]).toEqual(['Bezahlt', 'Ausgegeben'])
  })

  it('heads the two columns in English on an English phone', async () => {
    stubLaptop().answers('POST', SEARCH, answer(TISCH_12))

    const screen = await mountTheOrders('en')

    expect([
      screen.get('[data-test="paid-heading"]').text(),
      screen.get('[data-test="handed-out-heading"]').text(),
    ]).toEqual(['Paid', 'Handed out'])
  })

  it('puts a check under Bezahlt for a paid item and a cross for an unpaid one', async () => {
    stubLaptop().answers('POST', SEARCH, answer(TISCH_12))

    const screen = await mountTheOrders()

    expect(markOf(screen, 'paid-mark', 'wasser')).toEqual(expect.arrayContaining(['mdi-check', 'text-success']))
    expect(markOf(screen, 'paid-mark', 'bier')).toEqual(expect.arrayContaining(['mdi-close', 'text-error']))
  })

  it('puts a check under Ausgegeben for a handed out item and a cross for one still at the station', async () => {
    stubLaptop().answers('POST', SEARCH, answer(TISCH_12))

    const screen = await mountTheOrders()

    expect(markOf(screen, 'handed-out-mark', 'wasser')).toEqual(expect.arrayContaining(['mdi-check', 'text-success']))
    expect(markOf(screen, 'handed-out-mark', 'bier')).toEqual(expect.arrayContaining(['mdi-close', 'text-error']))
  })

  it('strikes a cancelled item through and leaves the others as they are', async () => {
    stubLaptop().answers('POST', SEARCH, answer(TISCH_12))

    const screen = await mountTheOrders()

    expect(screen.get('[data-test="overview-line"][data-test-id="cola"]').classes()).toContain('cancelled-item')
    expect(screen.get('[data-test="overview-line"][data-test-id="bier"]').classes()).not.toContain('cancelled-item')
  })

  it('counts how many items of the order are prepared, leaving the cancelled one out', async () => {
    stubLaptop().answers('POST', SEARCH, answer(TISCH_12))

    const screen = await mountTheOrders()

    expect(screen.get('[data-test="done-counter"]').text()).toBe('1 / 2 zubereitet')
  })

  it('says that no order matches when the laptop finds none', async () => {
    stubLaptop().answers('POST', SEARCH, answer({ ...TISCH_12, orders: [] }))

    const screen = await mountTheOrders()

    expect(screen.get('[data-test="no-orders"]').text()).toBe('An diesem Tisch passt keine Bestellung zu dieser Auswahl.')
  })

  it('sends the waiter to the table plan when no table was chosen, as after a reload', async () => {
    const laptop = stubLaptop()
    localStorage.setItem(TOKEN_STORAGE_KEY, 'lookup.token-here')
    useSessionStore().deviceToken = 'token-here'

    mount(OrderOverview, { global: { plugins: testPlugins() }, attachTo: document.body })
    await flushPromises()

    expect({ route: currentRoute.value, asked: laptop.urls() }).toEqual({ route: { name: 'home' }, asked: [] })
  })
})

describe('the filter above the orders of a table', () => {
  it('asks for every order, paid or not, cancelled items included, when the screen opens', async () => {
    const laptop = stubLaptop().answers('POST', SEARCH, answer(TISCH_12))

    await mountTheOrders()

    expect(laptop.bodiesSentTo('POST', SEARCH)).toEqual([
      { orderStatus: null, paymentStatus: null, includeCancelled: true },
    ])
  })

  it.each([
    ['open', 'open'],
    ['partially-fulfilled', 'partiallyFulfilled'],
    ['fulfilled', 'fulfilled'],
  ])('asks for the orders in the status the waiter tapped, %s', async (choice, orderStatus) => {
    const laptop = stubLaptop().answers('POST', SEARCH, answer(TISCH_12))
    const screen = await mountTheOrders()

    await tap(screen, `[data-test="order-status-choice"][data-test-id="${choice}"]`)

    expect(laptop.bodiesSentTo('POST', SEARCH)[1]).toEqual({ orderStatus, paymentStatus: null, includeCancelled: true })
  })

  it.each([
    ['paid', 'paid'],
    ['not-paid', 'notPaid'],
  ])('asks for the items in the payment status the waiter tapped, %s', async (choice, paymentStatus) => {
    const laptop = stubLaptop().answers('POST', SEARCH, answer(TISCH_12))
    const screen = await mountTheOrders()

    await tap(screen, `[data-test="payment-status-choice"][data-test-id="${choice}"]`)

    expect(laptop.bodiesSentTo('POST', SEARCH)[1]).toEqual({ orderStatus: null, paymentStatus, includeCancelled: true })
  })

  it('leaves the cancelled items out once the waiter unticks them', async () => {
    const laptop = stubLaptop().answers('POST', SEARCH, answer(TISCH_12))
    const screen = await mountTheOrders()

    await tap(screen, '[data-test="include-cancelled"] input')

    expect(laptop.bodiesSentTo('POST', SEARCH)[1]).toEqual({
      orderStatus: null,
      paymentStatus: null,
      includeCancelled: false,
    })
  })

  it('keeps an earlier pick when the waiter taps a second one', async () => {
    const laptop = stubLaptop().answers('POST', SEARCH, answer(TISCH_12))
    const screen = await mountTheOrders()
    await tap(screen, '[data-test="order-status-choice"][data-test-id="open"]')

    await tap(screen, '[data-test="payment-status-choice"][data-test-id="not-paid"]')

    expect(laptop.bodiesSentTo('POST', SEARCH)[2]).toEqual({
      orderStatus: 'open',
      paymentStatus: 'notPaid',
      includeCancelled: true,
    })
  })

  it('names the choices in English on an English phone', async () => {
    stubLaptop().answers('POST', SEARCH, answer(TISCH_12))

    const screen = await mountTheOrders('en')

    expect(screen.findAll('[data-test="order-status-choice"]').map((chip) => chip.text())).toEqual([
      'All',
      'Open',
      'Partly handed out',
      'Handed out',
    ])
    expect(screen.findAll('[data-test="payment-status-choice"]').map((chip) => chip.text())).toEqual([
      'All',
      'Paid',
      'Not paid',
    ])
  })
})
