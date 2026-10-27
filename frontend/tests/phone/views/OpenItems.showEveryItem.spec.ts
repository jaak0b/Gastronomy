import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { enableAutoUnmount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { navigate } from '../../../src/shared/router/router'
import {
  TABLE_REPORT,
  lineNamed,
  mountScreen,
  mountScreenToCancelItems,
  openItemsLaptop,
  orderCard,
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

type Screen = Awaited<ReturnType<typeof mountScreen>>

async function showEveryItem(screen: Screen): Promise<void> {
  await screen.get('[data-test="show-every-item"] input').trigger('click')
  await flushPromises()
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

describe('what the settle screen asks the laptop for', () => {
  it('asks only for the items that are neither paid nor cancelled', async () => {
    const laptop = openItemsLaptop(TABLE_REPORT)

    await mountScreen()

    expect(laptop.bodiesSentTo('POST', SEARCH)).toEqual([{ paymentStatus: 'notPaid', includeCancelled: false }])
  })

  it('asks for every item once the waiter ticks "Alle Positionen anzeigen"', async () => {
    const laptop = openItemsLaptop(TABLE_REPORT)
    const screen = await mountScreen()

    await showEveryItem(screen)

    expect(laptop.bodiesSentTo('POST', SEARCH)).toEqual([
      { paymentStatus: 'notPaid', includeCancelled: false },
      {},
    ])
  })

  it('names the box in English on an English phone', async () => {
    openItemsLaptop(TABLE_REPORT)

    const screen = await mountScreen('en')

    expect(screen.get('[data-test="show-every-item"]').text()).toBe('Show all items')
  })
})

describe('every item on the settle screen', () => {
  it('puts a paid item on a green background and offers no tick box for it', async () => {
    openItemsLaptop(WITH_CANCELLED_COLA)

    const screen = await mountScreen()

    const wasser = lineNamed(orderCard(screen, 138), 'Wasser')
    expect(wasser.classes()).toContain('paid-line')
    expect(wasser.find('[data-test="line-tick"]').exists()).toBe(false)
  })

  it('greys a cancelled item out, strikes it through and offers no tick box for it', async () => {
    openItemsLaptop(WITH_CANCELLED_COLA)

    const screen = await mountScreen()

    const cola = lineNamed(orderCard(screen, 138), 'Cola')
    expect(cola.classes()).toEqual(expect.arrayContaining(['cancelled-item', 'v-list-item--disabled']))
    expect(cola.find('[data-test="line-tick"]').exists()).toBe(false)
  })
})

describe('what the cancel screen asks the laptop for', () => {
  it('asks only for the items that are not cancelled', async () => {
    const laptop = openItemsLaptop(TABLE_REPORT)

    await mountScreenToCancelItems()

    expect(laptop.bodiesSentTo('POST', SEARCH)).toEqual([{ includeCancelled: false }])
  })

  it('asks for every item once the waiter ticks "Alle Positionen anzeigen"', async () => {
    const laptop = openItemsLaptop(TABLE_REPORT)
    const screen = await mountScreenToCancelItems()

    await showEveryItem(screen)

    expect(laptop.bodiesSentTo('POST', SEARCH)).toEqual([{ includeCancelled: false }, {}])
  })

  it('says that nothing is left to cancel when the laptop finds no order', async () => {
    openItemsLaptop({ ...TABLE_REPORT, orders: [] })

    const screen = await mountScreenToCancelItems()

    expect(screen.get('[data-test="no-orders"]').text()).toBe('An diesem Tisch gibt es nichts mehr zu stornieren.')
  })
})

describe('every item on the cancel screen', () => {
  it('shows a paid item like any other and lets the waiter tick it', async () => {
    openItemsLaptop(WITH_CANCELLED_COLA)

    const screen = await mountScreenToCancelItems()

    const wasser = lineNamed(orderCard(screen, 138), 'Wasser')
    expect(wasser.classes()).not.toContain('paid-line')
    expect(wasser.find('[data-test="line-paid"]').exists()).toBe(false)
    expect(wasser.find('[data-test="line-tick"]').exists()).toBe(true)
  })

  it('greys a cancelled item out, strikes it through and offers no tick box for it', async () => {
    openItemsLaptop(WITH_CANCELLED_COLA)

    const screen = await mountScreenToCancelItems()

    const cola = lineNamed(orderCard(screen, 138), 'Cola')
    expect(cola.classes()).toEqual(expect.arrayContaining(['cancelled-item', 'v-list-item--disabled']))
    expect(cola.find('[data-test="line-tick"]').exists()).toBe(false)
  })
})
