import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { fireHubEvent, forgetHubEvents } from '../../support/hubConnection'
import { useConnectionStore } from '../../../src/shared/stores/connection'
import { useOpenItemsStore } from '../../../src/phone/stores/openItems'
import { TOKEN_STORAGE_KEY, useSessionStore } from '../../../src/shared/stores/session'
import { answer, inTurn, stubLaptop } from '../../support/laptop'

vi.mock('@microsoft/signalr', async () => (await import('../../support/hubConnection')).signalrModuleFake())

function bratwurstOf(orderItemId: string, orderId: string, globalOrderNumber: number, unitPriceCents: number) {
  return {
    orderItemId,
    orderId,
    globalOrderNumber,
    itemName: 'Bratwurst',
    note: null,
    unitPriceCents,
    orderedAtUtc: '2026-09-05T18:00:00Z',
    fulfilledAtUtc: null,
    settledAtUtc: null,
  }
}

const FIRST_ORDER = {
  orderId: 'order-1',
  globalOrderNumber: 41,
  createdAtUtc: '2026-09-05T18:00:00Z',
  staffMemberName: 'Anna',
  items: [bratwurstOf('item-1', 'order-1', 41, 350)],
}

const TISCH_SIEBEN_BEFORE = {
  tableId: 'table-7',
  tableName: 'Tisch 7',
  openAmountCents: 350,
  orders: [FIRST_ORDER],
}

const TISCH_SIEBEN_AFTER_THE_SECOND_WAITER_SENT_TWO_BRATWURST = {
  tableId: 'table-7',
  tableName: 'Tisch 7',
  openAmountCents: 950,
  orders: [
    {
      orderId: 'order-2',
      globalOrderNumber: 42,
      createdAtUtc: '2026-09-05T18:04:00Z',
      staffMemberName: 'Bernd',
      items: [bratwurstOf('item-2', 'order-2', 42, 300), bratwurstOf('item-3', 'order-2', 42, 300)],
    },
    FIRST_ORDER,
  ],
}

describe('the open table on a phone while another phone places an order', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    forgetHubEvents()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('shows the two Bratwurst the other waiter sent for Tisch 7', async () => {
    stubLaptop().answers(
      'GET',
      '/api/open-items/tables/table-7',
      inTurn(answer(TISCH_SIEBEN_BEFORE), answer(TISCH_SIEBEN_AFTER_THE_SECOND_WAITER_SENT_TWO_BRATWURST)),
    )
    localStorage.setItem(TOKEN_STORAGE_KEY, 'lookup.token-here')
    useSessionStore().deviceToken = 'token-here'
    const openItems = useOpenItemsStore()
    openItems.listen()
    openItems.openTable('table-7')
    await useConnectionStore().connect({ deviceToken: 'token-here' })
    await vi.waitFor(() => expect(openItems.report?.openAmountCents).toBe(350))

    fireHubEvent('OrdersChanged')

    await vi.waitFor(() => expect(openItems.report?.openAmountCents).toBe(950))
  })
})
