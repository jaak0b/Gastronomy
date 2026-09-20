import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useOpenItemsStore } from '../../src/stores/openItems'
import { TOKEN_STORAGE_KEY, useSessionStore } from '../../src/stores/session'

vi.mock('@microsoft/signalr', async () => (await import('../support/hubConnection')).signalrModuleFake())

const OPEN_LIST = {
  tables: [
    {
      tableName: 'Tisch 5',
      openAmountCents: 1200,
      givenAwayAmountCents: 0,
      givenAwayItems: [],
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
  otherPhonesWereTold: true,
}

const SOMEBODY_ELSE_HAD_ITEM_ONE = {
  settledOrderItemIds: ['item-2'],
  reappliedOrderItemIds: [],
  alreadySettledByOthersOrderItemIds: ['item-1'],
  otherPhonesWereTold: true,
}

interface RecordedCall {
  url: string
  method: string
  body: Record<string, unknown> | null
}

function answerWith(replies: (() => Response)[]): RecordedCall[] {
  const calls: RecordedCall[] = []
  let position = 0
  vi.stubGlobal(
    'fetch',
    vi.fn(async (url: string, options: { method?: string; body?: string }) => {
      calls.push({
        url,
        method: options?.method ?? 'GET',
        body: options?.body === undefined ? null : JSON.parse(options.body),
      })
      const reply = replies[Math.min(position, replies.length - 1)]
      position += 1
      return reply()
    }),
  )
  return calls
}

function jsonOf(payload: unknown, status = 200): () => Response {
  return () => new Response(JSON.stringify(payload), { status })
}

function theWifiDrops(): () => Response {
  return () => {
    throw new TypeError('the laptop cannot be reached')
  }
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

  it('sends the same lines again, so the laptop can take it as this phone settling twice', async () => {
    const calls = answerWith([
      jsonOf(OPEN_LIST),
      theWifiDrops(),
      jsonOf(OWN_SETTLEMENT_APPLIED_AGAIN),
      jsonOf(LIST_AFTER_THE_SETTLEMENT),
    ])
    const openItems = await theTableWithTwoItems()

    await openItems.settle(1200, null)
    await openItems.settle(1200, null)

    expect(calls[1].body).toEqual(calls[2].body)
    expect(calls[2].body).toEqual({
      lines: [
        { orderItemId: 'item-1', paidPriceCents: 900, paymentNotice: null },
        { orderItemId: 'item-2', paidPriceCents: 300, paymentNotice: null },
      ],
    })
  })

  it('does not tell the waiter to hand back the twelve euros they correctly collected', async () => {
    const calls = answerWith([
      jsonOf(OPEN_LIST),
      theWifiDrops(),
      jsonOf(OWN_SETTLEMENT_APPLIED_AGAIN),
      jsonOf(LIST_AFTER_THE_SETTLEMENT),
    ])
    const openItems = await theTableWithTwoItems()

    await openItems.settle(1200, null)
    await openItems.settle(1200, null)

    expect(openItems.notice).toBeNull()
  })

  it('names the amount this phone sent for the lines somebody else had already taken', async () => {
    const calls = answerWith([
      jsonOf(OPEN_LIST),
      jsonOf(SOMEBODY_ELSE_HAD_ITEM_ONE),
      jsonOf(LIST_AFTER_THE_SETTLEMENT),
    ])
    const openItems = await theTableWithTwoItems()

    await openItems.settle(1200, null)

    expect(openItems.notice).toEqual({
      key: 'openItems.someWereAlreadySettled',
      parameters: { count: 1, amount: '9,00 €' },
      count: 1,
    })
  })
})
