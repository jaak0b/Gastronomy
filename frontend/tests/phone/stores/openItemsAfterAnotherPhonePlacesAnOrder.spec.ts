import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { fireHubEvent, forgetHubEvents } from '../../support/hubConnection'
import { useConnectionStore } from '../../../src/shared/stores/connection'
import { useOpenItemsStore } from '../../../src/phone/stores/openItems'
import { TOKEN_STORAGE_KEY, useSessionStore } from '../../../src/shared/stores/session'

vi.mock('@microsoft/signalr', async () => (await import('../../support/hubConnection')).signalrModuleFake())

const STATION_ID = '0f6f2c3a-1c3e-4a0b-9f5a-2c9d1b7e4a11'

const TISCH_SIEBEN_BEFORE = {
  tables: [
    {
      tableName: 'Tisch 7',
      openAmountCents: 350,
      items: [
        {
          orderItemId: 'item-1',
          orderId: 'order-1',
          globalOrderNumber: 41,
          itemName: 'Bratwurst',
          note: null,
          unitPriceCents: 350,
          orderedAtUtc: '2026-09-05T18:00:00Z',
        },
      ],
    },
  ],
  itemsWithoutAnOrderCount: 0,
}

const TISCH_SIEBEN_AFTER_THE_SECOND_WAITER_SENT_TWO_BRATWURST = {
  tables: [
    {
      tableName: 'Tisch 7',
      openAmountCents: 950,
      items: [
        ...TISCH_SIEBEN_BEFORE.tables[0].items,
        {
          orderItemId: 'item-2',
          orderId: 'order-2',
          globalOrderNumber: 42,
          itemName: 'Bratwurst',
          note: null,
          unitPriceCents: 300,
          orderedAtUtc: '2026-09-05T18:04:00Z',
        },
        {
          orderItemId: 'item-3',
          orderId: 'order-2',
          globalOrderNumber: 42,
          itemName: 'Bratwurst',
          note: null,
          unitPriceCents: 300,
          orderedAtUtc: '2026-09-05T18:04:00Z',
        },
      ],
    },
  ],
  itemsWithoutAnOrderCount: 0,
}

describe('the open list on a phone while another phone places an order', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    forgetHubEvents()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('shows the two Bratwurst the other waiter sent for Tisch 7', async () => {
    let answered = 0
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => {
        answered += 1
        return new Response(
          JSON.stringify(
            answered === 1
              ? TISCH_SIEBEN_BEFORE
              : TISCH_SIEBEN_AFTER_THE_SECOND_WAITER_SENT_TWO_BRATWURST,
          ),
          { status: 200 },
        )
      }),
    )
    localStorage.setItem(TOKEN_STORAGE_KEY, 'lookup.token-here')
    useSessionStore().deviceToken = 'token-here'
    const openItems = useOpenItemsStore()
    openItems.listen()
    await useConnectionStore().connect({ deviceToken: 'token-here' })
    expect(openItems.tables[0].openAmountCents).toBe(350)

    fireHubEvent('OrdersChanged')

    await vi.waitFor(() => expect(openItems.tables[0].openAmountCents).toBe(950))
  })
})
