import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { SEND_TIMEOUT_MS } from '../../src/core/sendTimeout'
import { useOpenItemsStore } from '../../src/stores/openItems'
import { TOKEN_STORAGE_KEY, useSessionStore } from '../../src/stores/session'

const OPEN_LIST = {
  tables: [
    {
      tableName: 'Tisch 12',
      openAmountCents: 700,
      givenAwayAmountCents: 0,
      givenAwayItems: [],
      items: [
        {
          orderItemId: 'item-1',
          orderId: 'order-1',
          globalOrderNumber: 137,
          itemName: 'Bratwurst',
          note: null,
          unitPriceCents: 350,
          orderedAtUtc: '2026-09-05T18:00:00Z',
        },
        {
          orderItemId: 'item-2',
          orderId: 'order-1',
          globalOrderNumber: 137,
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

const EMPTY_LIST = { tables: [], itemsWithoutAnOrderCount: 0 }

const SETTLED = {
  settledOrderItemIds: ['item-1'],
  alreadySettledOrderItemIds: [],
  otherPhonesWereTold: true,
}

interface RecordedCall {
  url: string
  method: string
  body: unknown
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

async function storeWithTheOpenList(replies: (() => Response)[]) {
  const calls = answerWith([jsonOf(OPEN_LIST), ...replies])
  localStorage.setItem(TOKEN_STORAGE_KEY, 'token-here')
  useSessionStore().deviceToken = 'token-here'
  const openItems = useOpenItemsStore()
  await openItems.load()
  return { openItems, calls }
}

describe('the list of what the tables still owe', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('holds every table with something open', async () => {
    const { openItems } = await storeWithTheOpenList([])

    expect(openItems.tables).toHaveLength(1)
    expect(openItems.hasLoaded).toBe(true)
  })

  it('knows nothing about the tables until the first list has arrived', () => {
    const openItems = useOpenItemsStore()

    expect(openItems.hasLoaded).toBe(false)
  })

  it('fetches the table names for the ordering screen on their own address', async () => {
    const calls = answerWith([jsonOf({ tableNames: ['Tisch 12', 'Tisch 3'] })])
    localStorage.setItem(TOKEN_STORAGE_KEY, 'token-here')
    useSessionStore().deviceToken = 'token-here'
    const openItems = useOpenItemsStore()

    await openItems.loadTableNames()

    expect(calls[0].url).toBe('/api/open-items/table-names')
    expect(openItems.knownTableNames).toEqual(['Tisch 12', 'Tisch 3'])
  })

  it('says how many items are missing from the list, so nothing disappears in silence', async () => {
    answerWith([jsonOf({ tables: [], itemsWithoutAnOrderCount: 2 })])
    localStorage.setItem(TOKEN_STORAGE_KEY, 'token-here')
    useSessionStore().deviceToken = 'token-here'
    const openItems = useOpenItemsStore()

    await openItems.load()

    expect(openItems.itemsWithoutAnOrderCount).toBe(2)
  })

  it('says so when the laptop could not be reached, so nobody trusts a stale list', async () => {
    answerWith([
      () => {
        throw new TypeError('the laptop cannot be reached')
      },
    ])
    localStorage.setItem(TOKEN_STORAGE_KEY, 'token-here')
    useSessionStore().deviceToken = 'token-here'
    const openItems = useOpenItemsStore()

    await openItems.load()

    expect(openItems.loadFailed).toBe(true)
  })

  it('adds up what the waiter ticked, so the cash can be counted out', async () => {
    const { openItems } = await storeWithTheOpenList([])

    openItems.toggleItem('item-1')

    expect(openItems.selectedTotalCents).toBe(350)
  })
})

describe('settling what the waiter ticked', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('sends the ticked items with the amount paid and clears the selection', async () => {
    const { openItems, calls } = await storeWithTheOpenList([jsonOf(SETTLED), jsonOf(EMPTY_LIST)])
    openItems.toggleItem('item-1')

    await openItems.settle(350, null)

    expect(calls[1]).toEqual({
      url: '/api/open-items/settle',
      method: 'POST',
      body: { orderItemIds: ['item-1'], amountPaidCents: 350, paymentNotice: null },
    })
    expect(openItems.selectedItemIds).toEqual([])
    expect(openItems.notice).toBeNull()
  })

  it('carries the typed reason when the table pays less than it owes', async () => {
    const { openItems, calls } = await storeWithTheOpenList([jsonOf(SETTLED), jsonOf(EMPTY_LIST)])
    openItems.toggleItem('item-1')

    await openItems.settle(0, 'Essen fuer die Kapelle')

    expect(calls[1]).toEqual({
      url: '/api/open-items/settle',
      method: 'POST',
      body: {
        orderItemIds: ['item-1'],
        amountPaidCents: 0,
        paymentNotice: 'Essen fuer die Kapelle',
      },
    })
  })

  it('names how many items another phone had already taken, even when the rest settled', async () => {
    const { openItems } = await storeWithTheOpenList([
      jsonOf({
        settledOrderItemIds: ['item-1'],
        alreadySettledOrderItemIds: ['item-2', 'item-3'],
        otherPhonesWereTold: true,
      }),
      jsonOf(EMPTY_LIST),
    ])
    openItems.toggleItem('item-1')

    await openItems.settle(350, null)

    expect(openItems.notice).toEqual({
      key: 'openItems.someWereAlreadySettled',
      parameters: { count: 2 },
      count: 2,
    })
  })

  it('reports a settle the other phones were not told about as a settle, not as a failure', async () => {
    const { openItems } = await storeWithTheOpenList([
      jsonOf({
        settledOrderItemIds: ['item-1'],
        alreadySettledOrderItemIds: [],
        otherPhonesWereTold: false,
      }),
      jsonOf(EMPTY_LIST),
    ])
    openItems.toggleItem('item-1')

    const outcome = await openItems.settle(350, null)

    expect(outcome).toBe('accepted')
    expect(openItems.notice?.key).toBe('openItems.otherPhonesWereNotTold')
  })

  it('shows the laptop wording when the laptop refuses, so no raw key reaches the phone', async () => {
    const { openItems } = await storeWithTheOpenList([
      jsonOf(
        {
          code: 'ValidationFailed',
          messageKey: 'order.settlementUnknownItem',
          parameters: {},
          details: null,
        },
        400,
      ),
      jsonOf(OPEN_LIST),
    ])
    openItems.toggleItem('item-1')

    const outcome = await openItems.settle(200, '   ')

    expect(outcome).toBe('refused')
    expect(openItems.notice?.key).toBe('order.settlementUnknownItem')
  })

  it('stops waiting after ten seconds and claims nothing about what was settled', async () => {
    vi.useFakeTimers()
    try {
      vi.stubGlobal(
        'fetch',
        vi.fn(
          (_path: string, init: RequestInit) =>
            new Promise<Response>((_resolve, reject) => {
              init.signal?.addEventListener('abort', () => {
                reject(new DOMException('The request was aborted', 'AbortError'))
              })
            }),
        ),
      )
      localStorage.setItem(TOKEN_STORAGE_KEY, 'token-here')
      useSessionStore().deviceToken = 'token-here'
      const openItems = useOpenItemsStore()
      openItems.tables = OPEN_LIST.tables
      openItems.toggleItem('item-1')

      const settling = openItems.settle(350, null)
      await vi.advanceTimersByTimeAsync(SEND_TIMEOUT_MS)

      expect(await settling).toBe('answerNeverCame')
      expect(openItems.notice?.key).toBe('openItems.settleAnswerNeverCame')
      expect(openItems.selectedItemIds).toEqual(['item-1'])
      expect(openItems.isSettling).toBe(false)
    } finally {
      vi.useRealTimers()
    }
  })

  it('claims nothing about what was settled when the laptop was not reached at all', async () => {
    const { openItems } = await storeWithTheOpenList([
      () => {
        throw new TypeError('the laptop cannot be reached')
      },
    ])
    openItems.toggleItem('item-1')

    await openItems.settle(350, null)

    expect(openItems.notice?.key).toBe('openItems.settleAnswerNeverCame')
    expect(openItems.selectedItemIds).toEqual(['item-1'])
  })

  it('keeps saying what a refusal from the laptop said, because a refusal is knowledge', async () => {
    const { openItems } = await storeWithTheOpenList([
      jsonOf(
        {
          code: 'ValidationFailed',
          messageKey: 'order.settlementCannotBeProcessed',
          parameters: {},
          details: null,
        },
        400,
      ),
      jsonOf(OPEN_LIST),
    ])
    openItems.toggleItem('item-1')

    await openItems.settle(200, null)

    expect(openItems.notice?.key).toBe('order.settlementCannotBeProcessed')
  })
})
