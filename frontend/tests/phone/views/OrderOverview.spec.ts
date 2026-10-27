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
import { allOnScreen, clickOn, isOnScreen, waitUntil } from '../../support/dom'

enableAutoUnmount(afterEach)

const SEARCH = '/api/open-items/tables/table-12/search'

function itemOfOrder141(
  orderItemId: string,
  itemName: string,
  state: string,
  settledAtUtc: string | null,
  wasHandedOutWhenCancelled = false,
) {
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
    wasHandedOutWhenCancelled,
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
      notCancelledItemCount: 2,
      handedOutItemCount: 1,
      items: [
        itemOfOrder141('bier', 'Bier', 'open', null),
        itemOfOrder141('wasser', 'Wasser', 'handedOut', '2026-09-05T18:40:00Z'),
        itemOfOrder141('cola', 'Cola', 'cancelled', null),
      ],
    },
  ],
}

const WASSER_HISTORY = {
  orderedByStaffMemberName: 'Anna',
  orderedAtUtc: '2026-09-05T18:20:00Z',
  statusChanges: [
    { state: 'open', changedAtUtc: '2026-09-05T18:20:00Z', staffMemberName: 'Anna', stationName: null },
    { state: 'handedOut', changedAtUtc: '2026-09-05T18:25:00Z', staffMemberName: null, stationName: 'Schänke' },
  ],
  settledAtUtc: '2026-09-05T18:40:00Z',
  chargedPriceCents: 400,
  paymentMethod: 'cash',
  paymentNotice: null,
  settledByStaffMemberName: 'Anna',
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

function nameCellOf(screen: Screen, orderItemId: string) {
  return screen.get(`[data-test="order-line"][data-test-id="${orderItemId}"] [data-test="line-name-cell"]`)
}

function markOf(screen: Screen, mark: 'paid-mark' | 'handed-out-mark', orderItemId: string): string[] {
  return screen.get(`[data-test="${mark}"][data-test-id="${orderItemId}"]`).classes()
}

async function tap(screen: Screen, selector: string): Promise<void> {
  await screen.get(selector).trigger('click')
  await flushPromises()
}

async function openTheOptions(screen: Screen, filter: 'order-status' | 'payment-status'): Promise<void> {
  await screen.get(`[data-test="${filter}-filter"] .v-field`).trigger('mousedown')
  await waitUntil(() => expect(isOnScreen(`[data-test="${filter}-option"]`)).toBe(true))
}

async function pick(screen: Screen, filter: 'order-status' | 'payment-status', choice: string): Promise<void> {
  await openTheOptions(screen, filter)
  await clickOn(`[data-test="${filter}-option"][data-test-id="${choice}"]`)
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

    expect(nameCellOf(screen, 'cola').classes()).toContain('cancelled-item')
    expect(nameCellOf(screen, 'bier').classes()).not.toContain('cancelled-item')
  })

  it('shows the done counter the laptop counted for the order', async () => {
    stubLaptop().answers('POST', SEARCH, answer(TISCH_12))

    const screen = await mountTheOrders()

    expect(screen.get('[data-test="done-counter"]').text()).toBe('1 / 2 zubereitet')
  })

  it('counts over the whole order when the filter leaves items out', async () => {
    const onlyThePaidWasser = {
      ...TISCH_12,
      orders: [
        {
          ...TISCH_12.orders[0],
          notCancelledItemCount: 5,
          handedOutItemCount: 3,
          items: [itemOfOrder141('wasser', 'Wasser', 'handedOut', '2026-09-05T18:40:00Z')],
        },
      ],
    }
    stubLaptop().answers('POST', SEARCH, answer(onlyThePaidWasser))

    const screen = await mountTheOrders()

    expect(screen.get('[data-test="done-counter"]').text()).toBe('3 / 5 zubereitet')
  })

  it('puts a check under Ausgegeben for an item cancelled after it was handed out, and keeps it struck through', async () => {
    const cancelledAfterHandOut = {
      ...TISCH_12,
      orders: [{ ...TISCH_12.orders[0], items: [itemOfOrder141('cola', 'Cola', 'cancelled', null, true)] }],
    }
    stubLaptop().answers('POST', SEARCH, answer(cancelledAfterHandOut))

    const screen = await mountTheOrders()

    expect(markOf(screen, 'handed-out-mark', 'cola')).toContain('mdi-check')
    expect(nameCellOf(screen, 'cola').classes()).toContain('cancelled-item')
  })

  it('puts a cross under Ausgegeben for an item cancelled before it was handed out', async () => {
    stubLaptop().answers('POST', SEARCH, answer(TISCH_12))

    const screen = await mountTheOrders()

    expect(markOf(screen, 'handed-out-mark', 'cola')).toContain('mdi-close')
  })

  it('puts a check under Bezahlt for an item cancelled after it was paid', async () => {
    const cancelledAfterPayment = {
      ...TISCH_12,
      orders: [{ ...TISCH_12.orders[0], items: [itemOfOrder141('cola', 'Cola', 'cancelled', '2026-09-05T18:30:00Z')] }],
    }
    stubLaptop().answers('POST', SEARCH, answer(cancelledAfterPayment))

    const screen = await mountTheOrders()

    expect(markOf(screen, 'paid-mark', 'cola')).toContain('mdi-check')
  })

  it('offers no tick box anywhere, because the overview is only read', async () => {
    stubLaptop().answers('POST', SEARCH, answer(TISCH_12))

    const screen = await mountTheOrders()

    expect(screen.findAll('[data-test="tick-cell"], [data-test="whole-order"], [data-test="line-tick"]')).toHaveLength(0)
  })

  it('shows no prices', async () => {
    stubLaptop().answers('POST', SEARCH, answer(TISCH_12))

    const screen = await mountTheOrders()

    expect(screen.findAll('[data-test="line-price"]')).toHaveLength(0)
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
    ['partiallyFulfilled', 'partiallyFulfilled'],
    ['fulfilled', 'fulfilled'],
  ])('asks for the orders in the status the waiter tapped, %s', async (choice, orderStatus) => {
    const laptop = stubLaptop().answers('POST', SEARCH, answer(TISCH_12))
    const screen = await mountTheOrders()

    await pick(screen, 'order-status', choice)

    expect(laptop.bodiesSentTo('POST', SEARCH)[1]).toEqual({ orderStatus, paymentStatus: null, includeCancelled: true })
  })

  it.each([
    ['paid', 'paid'],
    ['notPaid', 'notPaid'],
  ])('asks for the items in the payment status the waiter tapped, %s', async (choice, paymentStatus) => {
    const laptop = stubLaptop().answers('POST', SEARCH, answer(TISCH_12))
    const screen = await mountTheOrders()

    await pick(screen, 'payment-status', choice)

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
    await pick(screen, 'order-status', 'open')

    await pick(screen, 'payment-status', 'notPaid')

    expect(laptop.bodiesSentTo('POST', SEARCH)[2]).toEqual({
      orderStatus: 'open',
      paymentStatus: 'notPaid',
      includeCancelled: true,
    })
  })

  it('goes back to every order when the waiter picks Alle again', async () => {
    const laptop = stubLaptop().answers('POST', SEARCH, answer(TISCH_12))
    const screen = await mountTheOrders()
    await pick(screen, 'order-status', 'open')

    await pick(screen, 'order-status', 'every')

    expect(laptop.bodiesSentTo('POST', SEARCH)[2]).toEqual({ orderStatus: null, paymentStatus: null, includeCancelled: true })
  })

  it('names the two dropdowns Bestellungen and Positionen and starts both on Alle', async () => {
    stubLaptop().answers('POST', SEARCH, answer(TISCH_12))

    const screen = await mountTheOrders()

    expect([
      screen.get('[data-test="order-status-filter"]').text(),
      screen.get('[data-test="payment-status-filter"]').text(),
    ]).toEqual([expect.stringMatching(/^Bestellungen\s*Alle/), expect.stringMatching(/^Positionen\s*Alle/)])
  })

  it('names the order choices in English on an English phone', async () => {
    stubLaptop().answers('POST', SEARCH, answer(TISCH_12))
    const screen = await mountTheOrders('en')

    await openTheOptions(screen, 'order-status')

    expect(allOnScreen('[data-test="order-status-option"]').map((option) => option.textContent?.trim())).toEqual([
      'All',
      'Open',
      'Partly handed out',
      'Handed out',
    ])
  })

  it('names the item choices in English on an English phone', async () => {
    stubLaptop().answers('POST', SEARCH, answer(TISCH_12))
    const screen = await mountTheOrders('en')

    await openTheOptions(screen, 'payment-status')

    expect(allOnScreen('[data-test="payment-status-option"]').map((option) => option.textContent?.trim())).toEqual([
      'All',
      'Paid',
      'Not paid',
    ])
  })
})

describe('tapping an item on the overview', () => {
  it('opens the history the laptop holds for that item', async () => {
    stubLaptop()
      .answers('POST', SEARCH, answer(TISCH_12))
      .answers('GET', '/api/open-items/items/wasser/history', answer(WASSER_HISTORY))
    const screen = await mountTheOrders()

    await tap(screen, '[data-test="order-line"][data-test-id="wasser"] [data-test="line-name"]')

    expect(allOnScreen('[data-test="entry-event"]').map((event) => event.textContent?.trim())).toEqual([
      'Bestellt',
      'Ausgegeben',
      'Bezahlt',
    ])
  })
})
