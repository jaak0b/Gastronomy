import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { fireHubEvent, forgetHubEvents } from '../../support/hubConnection'
import { SEND_TIMEOUT_MS } from '../../../src/phone/core/sendTimeout'
import { useConnectionStore } from '../../../src/shared/stores/connection'
import { useOpenItemsStore } from '../../../src/phone/stores/openItems'
import { TOKEN_STORAGE_KEY, useSessionStore } from '../../../src/shared/stores/session'

vi.mock('@microsoft/signalr', async () => (await import('../../support/hubConnection')).signalrModuleFake())

const OPEN_LIST = {
  tables: [
    {
      tableName: 'Tisch 12',
      openAmountCents: 700,
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
  reappliedOrderItemIds: [],
  alreadySettledByOthersOrderItemIds: [],
  otherPhonesWereTold: true,
}

const TABLE_REPORT = {
  tableName: 'Tisch 12',
  openAmountCents: 400,
  orders: [
    {
      orderId: 'order-9',
      globalOrderNumber: 141,
      createdAtUtc: '2026-09-05T18:20:00Z',
      staffMemberName: 'Anna',
      items: [
        {
          orderItemId: 'lookup-open',
          orderId: 'order-9',
          globalOrderNumber: 141,
          itemName: 'Bier',
          note: null,
          unitPriceCents: 400,
          orderedAtUtc: '2026-09-05T18:20:00Z',
          fulfilledAtUtc: null,
          settledAtUtc: null,
        },
        {
          orderItemId: 'lookup-settled',
          orderId: 'order-9',
          globalOrderNumber: 141,
          itemName: 'Wasser',
          note: null,
          unitPriceCents: 200,
          orderedAtUtc: '2026-09-05T18:20:00Z',
          fulfilledAtUtc: '2026-09-05T18:25:00Z',
          settledAtUtc: '2026-09-05T18:40:00Z',
        },
      ],
    },
  ],
}

interface DeferredResponse {
  promise: Promise<Response>
  answerWith: (payload: unknown) => void
}

function deferredResponse(): DeferredResponse {
  let answer: (response: Response) => void = () => undefined
  const promise = new Promise<Response>((resolve) => {
    answer = resolve
  })
  return {
    promise,
    answerWith: (payload) => answer(new Response(JSON.stringify(payload), { status: 200 })),
  }
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
  localStorage.setItem(TOKEN_STORAGE_KEY, 'lookup.token-here')
  const session = useSessionStore()
  session.deviceToken = 'token-here'
  session.language = 'de'
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
    localStorage.setItem(TOKEN_STORAGE_KEY, 'lookup.token-here')
    useSessionStore().deviceToken = 'token-here'
    const openItems = useOpenItemsStore()

    await openItems.loadTableNames()

    expect(calls[0].url).toBe('/api/open-items/table-names')
    expect(openItems.knownTableNames).toEqual(['Tisch 12', 'Tisch 3'])
  })

  it('says how many items are missing from the list, so nothing disappears in silence', async () => {
    answerWith([jsonOf({ tables: [], itemsWithoutAnOrderCount: 2 })])
    localStorage.setItem(TOKEN_STORAGE_KEY, 'lookup.token-here')
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
    localStorage.setItem(TOKEN_STORAGE_KEY, 'lookup.token-here')
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
      body: {
        lines: [{ orderItemId: 'item-1', paidPriceCents: 350, paymentNotice: null }],
      },
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
        lines: [
          { orderItemId: 'item-1', paidPriceCents: 0, paymentNotice: 'Essen fuer die Kapelle' },
        ],
      },
    })
  })

  it('names the amount to hand back for the items another phone had already taken', async () => {
    const { openItems } = await storeWithTheOpenList([
      jsonOf({
        settledOrderItemIds: ['item-1'],
        reappliedOrderItemIds: [],
        alreadySettledByOthersOrderItemIds: ['item-2'],
        otherPhonesWereTold: true,
      }),
      jsonOf(EMPTY_LIST),
    ])
    openItems.toggleItem('item-1')
    openItems.toggleItem('item-2')

    await openItems.settle(700, null)

    expect(openItems.notice).toEqual({
      key: 'openItems.someWereAlreadySettled',
      parameters: { count: 1, amount: '3,50 €' },
      count: 1,
    })
  })

  it('reports a settle the other phones were not told about as a settle, not as a failure', async () => {
    const { openItems } = await storeWithTheOpenList([
      jsonOf({
        settledOrderItemIds: ['item-1'],
        reappliedOrderItemIds: [],
        alreadySettledByOthersOrderItemIds: [],
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
      localStorage.setItem(TOKEN_STORAGE_KEY, 'lookup.token-here')
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

describe('the open list a phone follows', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    forgetHubEvents()
    useSessionStore().deviceToken = 'token-here'
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('loads again when the festival starts or stops', async () => {
    const urls: string[] = []
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string) => {
        urls.push(url)
        return new Response(JSON.stringify(EMPTY_LIST), { status: 200 })
      }),
    )
    useOpenItemsStore().listen()
    await useConnectionStore().connect({ deviceToken: 'token-here' })
    urls.length = 0

    fireHubEvent('FestivalChanged')

    await vi.waitFor(() => expect(urls).toEqual(['/api/open-items']))
  })

  it('loads the looked-up table again when the station board changes', async () => {
    const urls: string[] = []
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string) => {
        urls.push(url)
        return new Response(
          JSON.stringify(url.startsWith('/api/open-items/table?') ? TABLE_REPORT : EMPTY_LIST),
          { status: 200 },
        )
      }),
    )
    const openItems = useOpenItemsStore()
    openItems.listen()
    openItems.openLookup('Tisch 12')
    await openItems.loadTableReport('Tisch 12')
    await useConnectionStore().connect({ deviceToken: 'token-here' })
    urls.length = 0

    fireHubEvent('StationOrdersChanged')

    await vi.waitFor(() =>
      expect(urls).toEqual([
        '/api/open-items',
        '/api/open-items/table?tableName=Tisch%2012',
      ]),
    )
  })
})

describe('the table a waiter looks up', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('asks the laptop for one table under the exact name the waiter typed', async () => {
    const calls = answerWith([jsonOf(OPEN_LIST), jsonOf(TABLE_REPORT)])
    localStorage.setItem(TOKEN_STORAGE_KEY, 'lookup.token-here')
    useSessionStore().deviceToken = 'token-here'
    const openItems = useOpenItemsStore()
    await openItems.load()

    openItems.openLookup('Tisch 12')
    await openItems.loadTableReport('Tisch 12')

    expect(calls[1].url).toBe('/api/open-items/table?tableName=Tisch%2012')
    expect(openItems.lookupReport).toEqual(TABLE_REPORT)
  })

  it('keeps the newest answer when an older lookup answers after it', async () => {
    const older = deferredResponse()
    const newer = deferredResponse()
    let lookups = 0
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string) => {
        if (url.startsWith('/api/open-items/table?')) {
          lookups += 1
          return lookups === 1 ? older.promise : newer.promise
        }
        return new Response(JSON.stringify(OPEN_LIST), { status: 200 })
      }),
    )
    localStorage.setItem(TOKEN_STORAGE_KEY, 'lookup.token-here')
    useSessionStore().deviceToken = 'token-here'
    const openItems = useOpenItemsStore()
    await openItems.load()

    openItems.openLookup('Tisch 12')
    const firstLookup = openItems.loadTableReport('Tisch 12')
    const newestLookup = openItems.loadTableReport('Tisch 12')
    newer.answerWith({ ...TABLE_REPORT, tableName: 'Tisch 3' })
    await newestLookup
    older.answerWith(TABLE_REPORT)
    await firstLookup

    expect(openItems.lookupReport?.tableName).toBe('Tisch 3')
  })

  it('clears the shown orders when the lookup fails, so a stale table is never trusted', async () => {
    const { openItems } = await storeWithTheOpenList([
      jsonOf(TABLE_REPORT),
      () => {
        throw new TypeError('the laptop cannot be reached')
      },
    ])
    openItems.openLookup('Tisch 12')
    await openItems.loadTableReport('Tisch 12')

    await openItems.loadTableReport('Tisch 12')

    expect(openItems.lookupReport).toBeNull()
    expect(openItems.lookupFailed).toBe(true)
  })

  it('keeps a closed lookup closed when a slow answer arrives afterwards', async () => {
    const slow = deferredResponse()
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string) => {
        if (url.startsWith('/api/open-items/table?')) {
          return slow.promise
        }
        return new Response(JSON.stringify(OPEN_LIST), { status: 200 })
      }),
    )
    localStorage.setItem(TOKEN_STORAGE_KEY, 'lookup.token-here')
    useSessionStore().deviceToken = 'token-here'
    const openItems = useOpenItemsStore()
    await openItems.load()

    openItems.openLookup('Tisch 12')
    const lookup = openItems.loadTableReport('Tisch 12')
    openItems.closeLookup()
    slow.answerWith(TABLE_REPORT)
    await lookup

    expect(openItems.lookupReport).toBeNull()
    expect(openItems.isLookingUp).toBe(false)
  })

  it('reads the ticked positions from the lookup while one is shown', async () => {
    const { openItems } = await storeWithTheOpenList([])
    openItems.openLookup('Tisch 12')
    openItems.lookupReport = TABLE_REPORT

    openItems.toggleItem('lookup-open')

    expect(openItems.selectedItemIds).toEqual(['lookup-open'])
    expect(openItems.selectedTotalCents).toBe(400)
  })

  it('takes no tick from a settled position in the lookup', async () => {
    const { openItems } = await storeWithTheOpenList([])
    openItems.openLookup('Tisch 12')
    openItems.lookupReport = TABLE_REPORT

    openItems.toggleItem('lookup-settled')

    expect(openItems.selectedItemIds).toEqual([])
  })

  it('empties the selection when the lookup opens and when it closes', async () => {
    const { openItems } = await storeWithTheOpenList([])
    openItems.toggleItem('item-1')

    openItems.openLookup('Tisch 12')

    expect(openItems.selectedItemIds).toEqual([])

    openItems.lookupReport = TABLE_REPORT
    openItems.toggleItem('lookup-open')

    expect(openItems.selectedItemIds).toEqual(['lookup-open'])

    openItems.closeLookup()

    expect(openItems.selectedItemIds).toEqual([])
  })

  it('asks the laptop for the looked-up table again after settling, so the colours stay true', async () => {
    const { openItems, calls } = await storeWithTheOpenList([
      jsonOf(TABLE_REPORT),
      jsonOf(SETTLED),
      jsonOf(OPEN_LIST),
      jsonOf(TABLE_REPORT),
    ])
    openItems.openLookup('Tisch 12')
    await openItems.loadTableReport('Tisch 12')
    openItems.toggleItem('lookup-open')

    await openItems.settle(400, null)

    expect(calls.map((call) => call.url)).toEqual([
      '/api/open-items',
      '/api/open-items/table?tableName=Tisch%2012',
      '/api/open-items/settle',
      '/api/open-items',
      '/api/open-items/table?tableName=Tisch%2012',
    ])
  })
})
