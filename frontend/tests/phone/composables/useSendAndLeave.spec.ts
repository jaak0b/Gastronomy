import { beforeEach, describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { defineComponent, h } from 'vue'
import { useSendAndLeave } from '../../../src/phone/composables/useSendAndLeave'
import { useCatalogStore } from '../../../src/phone/stores/catalog'
import { useOpenItemsStore } from '../../../src/phone/stores/openItems'
import { useOrderStore } from '../../../src/phone/stores/order'
import { useSessionStore } from '../../../src/shared/stores/session'
import { currentRoute, navigate } from '../../../src/shared/router/router'
import { testPlugins } from '../../support/plugins'
import { answer, inTurn, noConnection, stubLaptop } from '../../support/laptop'
import { TISCH_3 } from '../../support/waiterPhone'

const WASSER = {
  id: 'item-wasser',
  name: 'Wasser',
  categoryId: 'category-getraenke',
  priceCents: 200,
  sortOrder: 1,
  isAvailable: true,
  stationIds: ['station-bar'],
  productionMinutes: 0,
  isQueueIndependent: false,
}

const PLACED_ORDER_TO_SETTLE = {
  orderId: 'order-1',
  globalOrderNumber: 1,
  status: 'open',
  totalCents: 200,
  createdAtUtc: '2026-09-05T18:00:00Z',
  stationOrders: [
    {
      stationOrderId: 'station-order-1',
      stationId: 'station-bar',
      stationName: 'Bar',
      stationOrderNumber: 1,
      deliveryMode: 'together',
      itemIds: ['new-wasser'],
    },
  ],
}

const TABLE_THREE_WITH_THE_SENT_ITEM = {
  tableId: 'table-3',
  tableName: 'Tisch 3',
  orders: [
    {
      orderId: 'order-1',
      globalOrderNumber: 1,
      createdAtUtc: '2026-09-05T18:00:00Z',
      staffMemberName: 'Anna',
      notCancelledItemCount: 1,
      handedOutItemCount: 0,
      items: [
        {
          orderItemId: 'new-wasser',
          orderId: 'order-1',
          globalOrderNumber: 1,
          itemName: 'Wasser',
          note: null,
          unitPriceCents: 200,
          state: 'open',
          stateSetAtUtc: '2026-09-05T18:00:00Z',
          wasHandedOutWhenCancelled: false,
          settledAtUtc: null,
          chargedPriceCents: null,
        },
      ],
    },
  ],
}

function prepareOrder() {
  useCatalogStore().catalog = {
    festival: null,
    categories: [
      { categoryId: 'category-getraenke', name: 'Getränke', colourHex: '#C62828', sortOrder: 1 },
    ],
    items: [WASSER],
    stations: [{ id: 'station-bar', name: 'Bar', sortOrder: 1 }],
  }
  const order = useOrderStore()
  order.addItem({ catalogItemId: WASSER.id, note: null, stationId: 'station-bar', name: WASSER.name })
  order.chooseTable(TISCH_3)
  return order
}

function theLaptopPlacesTheOrderToSettleAfter(failedAttempts: number): void {
  useSessionStore().deviceToken = 'token-here'
  const failedAnswers = Array.from({ length: failedAttempts }, () => noConnection())
  stubLaptop()
    .answersEverythingElse(answer({ stations: [] }))
    .answers('POST', '/api/open-items/tables/table-3/search', answer(TABLE_THREE_WITH_THE_SENT_ITEM))
    .answers('ANY', (call) => call.url === '/api/orders', inTurn(...failedAnswers, answer(PLACED_ORDER_TO_SETTLE)))
}

function mountTheSendingScreen() {
  let sender: ReturnType<typeof useSendAndLeave> | null = null
  const host = mount(
    defineComponent({
      setup() {
        sender = useSendAndLeave()
        return () => h('div')
      },
    }),
    { global: { plugins: testPlugins() } },
  )
  if (sender === null) {
    throw new Error('The sending screen did not set up.')
  }
  return { host, sender: sender as ReturnType<typeof useSendAndLeave> }
}

async function whatOpenItemsShows() {
  const openItems = useOpenItemsStore()
  await openItems.load()
  return { route: currentRoute.value, table: openItems.tableId, ticked: openItems.selectedItemIds }
}

describe('leaving the review once the laptop accepted the order', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    navigate('/review')
  })

  it('takes the waiter home when the table settles later, so the sent order cannot be typed into any more', async () => {
    theLaptopPlacesTheOrderToSettleAfter(0)
    prepareOrder()
    const { sender } = mountTheSendingScreen()

    await sender.send('leaveOpen')

    expect(currentRoute.value).toEqual({ name: 'home' })
  })

  it('opens the open items of the table with the items just sent ticked when the guest pays on the spot', async () => {
    theLaptopPlacesTheOrderToSettleAfter(0)
    prepareOrder()
    const { sender } = mountTheSendingScreen()

    await sender.send('settleRightAway')

    expect(await whatOpenItemsShows()).toEqual({
      route: { name: 'openItems' },
      table: 'table-3',
      ticked: ['new-wasser'],
    })
  })

  it('still opens the open items with the ticks when the order to settle only got through on the retry', async () => {
    theLaptopPlacesTheOrderToSettleAfter(1)
    const order = prepareOrder()
    const { sender } = mountTheSendingScreen()
    await sender.send('settleRightAway')
    expect(order.sendState).toBe('failed')

    await sender.sendAgain()

    expect(await whatOpenItemsShows()).toEqual({
      route: { name: 'openItems' },
      table: 'table-3',
      ticked: ['new-wasser'],
    })
  })

  it('stays on the review while the laptop has not accepted the order', async () => {
    theLaptopPlacesTheOrderToSettleAfter(1)
    prepareOrder()
    const { sender } = mountTheSendingScreen()

    await sender.send('settleRightAway')

    expect({ route: currentRoute.value, table: useOpenItemsStore().tableId }).toEqual({
      route: { name: 'review' },
      table: null,
    })
  })

  it('still opens the table with the items just sent ticked when the answer arrives after the review was left', async () => {
    theLaptopPlacesTheOrderToSettleAfter(0)
    prepareOrder()
    const { host, sender } = mountTheSendingScreen()
    const sending = sender.send('settleRightAway')
    navigate('/catalog')
    host.unmount()

    await sending

    expect(await whatOpenItemsShows()).toEqual({
      route: { name: 'openItems' },
      table: 'table-3',
      ticked: ['new-wasser'],
    })
  })
})
