import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useOpenItemsStore } from '../../../src/phone/stores/openItems'
import { TOKEN_STORAGE_KEY, useSessionStore } from '../../../src/shared/stores/session'
import { stubLaptop, answer, inTurn, noConnection, type LaptopReply, type StubbedLaptop } from '../../support/laptop'

vi.mock('@microsoft/signalr', async () => (await import('../../support/hubConnection')).signalrModuleFake())

const OPEN_LIST = {
  tables: [
    {
      tableName: 'Tisch 5',
      openAmountCents: 1200,
      items: [
        {
          orderItemId: 'item-1',
          orderId: 'order-1',
          globalOrderNumber: 204,
          itemName: 'Schnitzel',
          note: null,
          unitPriceCents: 900,
          orderedAtUtc: '2026-09-05T18:00:00Z',
        },
        {
          orderItemId: 'item-2',
          orderId: 'order-1',
          globalOrderNumber: 204,
          itemName: 'Radler',
          note: null,
          unitPriceCents: 300,
          orderedAtUtc: '2026-09-05T18:00:00Z',
        },
      ],
    },
  ],
  itemsWithoutAnOrderCount: 0,
}

const LIST_AFTER_THE_SETTLEMENT = { tables: [], itemsWithoutAnOrderCount: 0 }

const OWN_SETTLEMENT_APPLIED_AGAIN = {
  settledOrderItemIds: [],
  reappliedOrderItemIds: ['item-1', 'item-2'],
  alreadySettledByOthersOrderItemIds: [],
}

const SOMEBODY_ELSE_HAD_ITEM_ONE = {
  settledOrderItemIds: ['item-2'],
  reappliedOrderItemIds: [],
  alreadySettledByOthersOrderItemIds: ['item-1'],
}

function laptopAnsweringInTurn(replies: LaptopReply[]): StubbedLaptop {
  return stubLaptop().answersEverythingElse(inTurn(...replies))
}

async function theTableWithTwoItems() {
  localStorage.setItem(TOKEN_STORAGE_KEY, 'lookup.token-here')
  const session = useSessionStore()
  session.deviceToken = 'token-here'
  session.language = 'de'
  const openItems = useOpenItemsStore()
  await openItems.load()
  openItems.toggleItem('item-1')
  openItems.toggleItem('item-2')
  return openItems
}

describe('settling the same items again after the answer never came', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('sends the same lines and the same way of paying again, so the laptop can take it as this phone settling twice', async () => {
    const laptop = laptopAnsweringInTurn([
      answer(OPEN_LIST),
      noConnection(),
      answer(OWN_SETTLEMENT_APPLIED_AGAIN),
      answer(LIST_AFTER_THE_SETTLEMENT),
    ])
    const openItems = await theTableWithTwoItems()

    await openItems.settle(1200, null, 'card')
    await openItems.settle(1200, null, 'card')

    expect(laptop.calls[1].body).toEqual(laptop.calls[2].body)
    expect(laptop.calls[2].body).toEqual({
      lines: [
        { orderItemId: 'item-1', paidPriceCents: 900, paymentNotice: null },
        { orderItemId: 'item-2', paidPriceCents: 300, paymentNotice: null },
      ],
      paymentMethod: 'card',
    })
  })

  it('does not tell the waiter to hand back the twelve euros they correctly collected', async () => {
    const laptop = laptopAnsweringInTurn([
      answer(OPEN_LIST),
      noConnection(),
      answer(OWN_SETTLEMENT_APPLIED_AGAIN),
      answer(LIST_AFTER_THE_SETTLEMENT),
    ])
    const openItems = await theTableWithTwoItems()

    await openItems.settle(1200, null, 'card')
    await openItems.settle(1200, null, 'card')

    expect(openItems.notice).toBeNull()
  })

  it('names the amount this phone sent for the lines somebody else had already taken', async () => {
    const laptop = laptopAnsweringInTurn([
      answer(OPEN_LIST),
      answer(SOMEBODY_ELSE_HAD_ITEM_ONE),
      answer(LIST_AFTER_THE_SETTLEMENT),
    ])
    const openItems = await theTableWithTwoItems()

    await openItems.settle(1200, null, 'card')

    expect(openItems.notice).toEqual({
      key: 'phone.openItems.messages.someWereAlreadySettled',
      parameters: { count: 1, amount: '9,00 €' },
      count: 1,
    })
  })
})
