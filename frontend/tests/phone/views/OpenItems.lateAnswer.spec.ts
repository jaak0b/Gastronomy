import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import OpenItems from '../../../src/phone/views/OpenItems.vue'
import { TABLE_LOOKUP_DEBOUNCE_MS } from '../../../src/phone/core/openItems'
import { useOpenItemsStore } from '../../../src/phone/stores/openItems'
import { TOKEN_STORAGE_KEY, useSessionStore } from '../../../src/shared/stores/session'
import { useCatalogStore } from '../../../src/phone/stores/catalog'
import { useOrderStore } from '../../../src/phone/stores/order'
import { testPlugins } from '../../support/plugins'
import { OPEN_LIST, TABLE_REPORT, lineNamed, lookupCard } from './openItemsFixture'
import { answer, stubLaptop, heldUntil } from '../../support/laptop'

describe('an order sent to be settled whose answer arrives while the waiter already looks up another table', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
  })

  afterEach(() => {
    vi.useRealTimers()
    vi.unstubAllGlobals()
    document.body.innerHTML = ''
  })

  function stubTheLaptopAnsweringTheOrderLate(): { answerTheOrder: () => void } {
    let answerTheOrder: () => void = () => {}
    const orderAnswered = new Promise<void>((resolve) => {
      answerTheOrder = resolve
    })
    stubLaptop()
      .answersEverythingElse(answer(OPEN_LIST))
      .answers('GET', '/api/open-items/table-names', answer({ tableNames: ['Tisch 3'] }))
      .answers('GET', '/api/open-items/table', answer(TABLE_REPORT))
      .answers(
        'ANY',
        (call) => call.url === '/api/orders',
        heldUntil(
          orderAnswered,
          answer({
            orderId: 'order-12',
            globalOrderNumber: 150,
            status: 'open',
            totalCents: 200,
            createdAtUtc: '2026-09-05T18:50:00Z',
            stationOrders: [
              {
                stationOrderId: 'station-order-12',
                stationId: 'station-bar',
                stationName: 'Bar',
                stationOrderNumber: 9,
                deliveryMode: 'together',
                itemIds: ['new-wasser'],
              },
            ],
          }),
        ),
      )
    return { answerTheOrder }
  }

  function anOrderForTableTwelve() {
    useCatalogStore().catalog = {
      categories: [
        { categoryId: 'category-getraenke', name: 'Getränke', colourHex: '#C62828', sortOrder: 1 },
      ],
      items: [
        {
          id: 'item-wasser',
          name: 'Wasser',
          categoryId: 'category-getraenke',
          priceCents: 200,
          sortOrder: 1,
          isAvailable: true,
          stationIds: ['station-bar'],
          productionMinutes: 0,
          isQueueIndependent: false,
        },
      ],
      stations: [{ id: 'station-bar', name: 'Bar', sortOrder: 1 }],
    }
    const order = useOrderStore()
    order.addItem({ catalogItemId: 'item-wasser', note: null, stationId: 'station-bar', name: 'Wasser' })
    order.setTable('Tisch 12')
    return order
  }

  it('keeps the table the waiter typed and the item the waiter ticked', async () => {
    const { answerTheOrder } = stubTheLaptopAnsweringTheOrderLate()
    localStorage.setItem(TOKEN_STORAGE_KEY, 'lookup.token-here')
    const session = useSessionStore()
    session.deviceToken = 'token-here'
    session.language = 'de'
    const order = anOrderForTableTwelve()
    const sending = order.send('settleRightAway')
    const screen = mount(OpenItems, { global: { plugins: testPlugins() }, attachTo: document.body })
    await flushPromises()
    await screen.get('[data-test="table-field"] input').setValue('Tisch 3')
    await new Promise((resolve) => setTimeout(resolve, TABLE_LOOKUP_DEBOUNCE_MS))
    await flushPromises()
    await lineNamed(lookupCard(screen, 137), 'Bratwurst').trigger('click')

    answerTheOrder()
    await sending
    await flushPromises()

    const openItems = useOpenItemsStore()
    expect({
      lookup: openItems.lookupName,
      field: (screen.get('[data-test="table-field"] input').element as HTMLInputElement).value,
      ticked: openItems.selectedItemIds,
    }).toEqual({ lookup: 'Tisch 3', field: 'Tisch 3', ticked: ['item-plain'] })
  })
})
