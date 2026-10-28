import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { enableAutoUnmount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { navigate } from '../../../src/shared/router/router'
import { useOpenItemsStore } from '../../../src/phone/stores/openItems'
import { answer, inTurn } from '../../support/laptop'
import { clickOn, isOnScreen, waitUntil } from '../../support/dom'
import {
  TABLE_REPORT,
  lineNamed,
  mountScreen,
  mountScreenToCancelItems,
  openItemsLaptop,
  orderCard,
  tickLine,
} from './openItemsFixture'

enableAutoUnmount(afterEach)

const SEARCH = '/api/open-items/tables/table-12/search'

const WITH_CANCELLED_COLA = {
  ...TABLE_REPORT,
  orders: TABLE_REPORT.orders.map((order) => ({
    ...order,
    items: order.items.map((item) => (item.itemName === 'Cola' ? { ...item, state: 'cancelled' } : item)),
  })),
}

const ONLY_ORDER_137 = { ...TABLE_REPORT, orders: [TABLE_REPORT.orders[0]] }

type Screen = Awaited<ReturnType<typeof mountScreen>>

async function pick(screen: Screen, filter: 'order-status' | 'payment-status', choice: string): Promise<void> {
  await screen.get(`[data-test="${filter}-filter"] .v-field`).trigger('mousedown')
  await waitUntil(() => expect(isOnScreen(`[data-test="${filter}-option"]`)).toBe(true))
  await clickOn(`[data-test="${filter}-option"][data-test-id="${choice}"]`)
}

async function tickIncludeCancelled(screen: Screen): Promise<void> {
  await screen.get('[data-test="include-cancelled"] input').trigger('click')
  await flushPromises()
}

function shownChoices(screen: Screen) {
  return {
    orders: screen.get('[data-test="order-status-filter"]').text(),
    items: screen.get('[data-test="payment-status-filter"]').text(),
    cancelled: (screen.get('[data-test="include-cancelled"] input').element as HTMLInputElement).checked,
  }
}

beforeEach(() => {
  setActivePinia(createPinia())
  localStorage.clear()
  document.body.innerHTML = ''
  navigate('/open-items')
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('the filter on the settle screen', () => {
  it('opens on the items that are neither paid nor cancelled and asks the laptop for exactly those', async () => {
    const laptop = openItemsLaptop(TABLE_REPORT)

    const screen = await mountScreen()

    expect({ sent: laptop.bodiesSentTo('POST', SEARCH), shown: shownChoices(screen) }).toEqual({
      sent: [{ orderStatus: null, paymentStatus: 'notPaid', includeCancelled: false }],
      shown: {
        orders: expect.stringMatching(/^Bestellungen\s*Alle/),
        items: expect.stringMatching(/^Positionen\s*Nicht bezahlt/),
        cancelled: false,
      },
    })
  })

  it('asks the laptop for what the waiter picked', async () => {
    const laptop = openItemsLaptop(TABLE_REPORT)
    const screen = await mountScreen()

    await pick(screen, 'payment-status', 'every')
    await tickIncludeCancelled(screen)
    await pick(screen, 'order-status', 'open')

    expect(laptop.bodiesSentTo('POST', SEARCH).slice(1)).toEqual([
      { orderStatus: null, paymentStatus: null, includeCancelled: false },
      { orderStatus: null, paymentStatus: null, includeCancelled: true },
      { orderStatus: 'open', paymentStatus: null, includeCancelled: true },
    ])
  })

  it('starts again on its own choices every time the screen opens', async () => {
    const laptop = openItemsLaptop(TABLE_REPORT)
    const first = await mountScreen()
    await pick(first, 'payment-status', 'paid')
    first.unmount()
    laptop.forgetCalls()

    const second = await mountScreen()

    expect({ sent: laptop.bodiesSentTo('POST', SEARCH), items: shownChoices(second).items }).toEqual({
      sent: [{ orderStatus: null, paymentStatus: 'notPaid', includeCancelled: false }],
      items: expect.stringMatching(/^Positionen\s*Nicht bezahlt/),
    })
  })

  it('unticks an item the new pick no longer shows, and keeps the one still shown', async () => {
    openItemsLaptop(TABLE_REPORT).answers('POST', SEARCH, inTurn(answer(TABLE_REPORT), answer(ONLY_ORDER_137)))
    const screen = await mountScreen()
    await tickLine(screen, 'Bratwurst')
    await tickLine(screen, 'Cola')

    await pick(screen, 'order-status', 'open')

    expect(useOpenItemsStore().selectedItemIds).toEqual(['item-plain'])
  })

  it('puts a check under Bezahlt for a paid item and offers no tick box for it', async () => {
    openItemsLaptop(WITH_CANCELLED_COLA)

    const screen = await mountScreen()

    const wasser = lineNamed(orderCard(screen, 138), 'Wasser')
    expect(wasser.get('[data-test="paid-mark"]').classes()).toContain('mdi-check')
    expect(wasser.find('[data-test="line-tick"]').exists()).toBe(false)
  })

  it('strikes a cancelled item through and offers no tick box for it', async () => {
    openItemsLaptop(WITH_CANCELLED_COLA)

    const screen = await mountScreen()

    const cola = lineNamed(orderCard(screen, 138), 'Cola')
    expect(cola.get('[data-test="line-name-cell"]').classes()).toContain('cancelled-item')
    expect(cola.find('[data-test="line-tick"]').exists()).toBe(false)
  })
})

const NO_ORDER_MATCHES = 'Keine passenden Bestellungen an diesem Tisch'

describe('the empty answer under a filter the waiter picked', () => {
  it('says that no order matches on the settle screen, not that nothing is left to settle', async () => {
    openItemsLaptop(TABLE_REPORT).answers('POST', SEARCH, inTurn(answer(TABLE_REPORT), answer({ ...TABLE_REPORT, orders: [] })))
    const screen = await mountScreen()

    await pick(screen, 'order-status', 'fulfilled')

    expect(screen.get('[data-test="no-orders"]').text()).toBe(NO_ORDER_MATCHES)
  })

  it('says that no order matches on the cancel screen, not that nothing is left to cancel', async () => {
    openItemsLaptop(TABLE_REPORT).answers('POST', SEARCH, inTurn(answer(TABLE_REPORT), answer({ ...TABLE_REPORT, orders: [] })))
    const screen = await mountScreenToCancelItems()

    await pick(screen, 'order-status', 'fulfilled')

    expect(screen.get('[data-test="no-orders"]').text()).toBe(NO_ORDER_MATCHES)
  })
})

describe('the filter on the cancel screen', () => {
  it('opens on every item that is not cancelled, paid or not, and asks the laptop for exactly those', async () => {
    const laptop = openItemsLaptop(TABLE_REPORT)

    const screen = await mountScreenToCancelItems()

    expect({ sent: laptop.bodiesSentTo('POST', SEARCH), shown: shownChoices(screen) }).toEqual({
      sent: [{ orderStatus: null, paymentStatus: null, includeCancelled: false }],
      shown: {
        orders: expect.stringMatching(/^Bestellungen\s*Alle/),
        items: expect.stringMatching(/^Positionen\s*Alle/),
        cancelled: false,
      },
    })
  })

  it('asks the laptop for what the waiter picked', async () => {
    const laptop = openItemsLaptop(TABLE_REPORT)
    const screen = await mountScreenToCancelItems()

    await pick(screen, 'payment-status', 'paid')

    expect(laptop.bodiesSentTo('POST', SEARCH)[1]).toEqual({ orderStatus: null, paymentStatus: 'paid', includeCancelled: false })
  })

  it('starts again on its own choices every time the screen opens', async () => {
    const laptop = openItemsLaptop(TABLE_REPORT)
    const first = await mountScreenToCancelItems()
    await tickIncludeCancelled(first)
    first.unmount()
    laptop.forgetCalls()

    const second = await mountScreenToCancelItems()

    expect({ sent: laptop.bodiesSentTo('POST', SEARCH), cancelled: shownChoices(second).cancelled }).toEqual({
      sent: [{ orderStatus: null, paymentStatus: null, includeCancelled: false }],
      cancelled: false,
    })
  })

  it('says that nothing is left to cancel when the laptop finds no order', async () => {
    openItemsLaptop({ ...TABLE_REPORT, orders: [] })

    const screen = await mountScreenToCancelItems()

    expect(screen.get('[data-test="no-orders"]').text()).toBe('An diesem Tisch ist nichts mehr zu stornieren.')
  })

  it('lets the waiter tick a paid item', async () => {
    openItemsLaptop(WITH_CANCELLED_COLA)

    const screen = await mountScreenToCancelItems()

    expect(lineNamed(orderCard(screen, 138), 'Wasser').find('[data-test="line-tick"]').exists()).toBe(true)
  })

  it('strikes a cancelled item through and offers no tick box for it', async () => {
    openItemsLaptop(WITH_CANCELLED_COLA)

    const screen = await mountScreenToCancelItems()

    const cola = lineNamed(orderCard(screen, 138), 'Cola')
    expect(cola.get('[data-test="line-name-cell"]').classes()).toContain('cancelled-item')
    expect(cola.find('[data-test="line-tick"]').exists()).toBe(false)
  })
})
