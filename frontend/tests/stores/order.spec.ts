import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { nextTick } from 'vue'
import { useCatalogStore } from '../../src/stores/catalog'
import { useOrderStore, ARRIVAL_NOTICE_MS, SEND_TIMEOUT_MS } from '../../src/stores/order'
import {
  DRAFT_STORAGE_KEY,
  SEND_PROGRESS_STORAGE_KEY,
  saveSendProgress,
} from '../../src/core/draftCart'

function answerWith(totalCents: number) {
  vi.stubGlobal(
    'fetch',
    vi.fn(
      async () =>
        new Response(
          JSON.stringify({
            orderId: 'order-1',
            globalOrderNumber: 1,
            totalCents,
            stationOrders: [],
          }),
          { status: 200 },
        ),
    ),
  )
}

describe('the notice that an order has arrived', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    vi.useFakeTimers()
  })

  afterEach(() => {
    vi.useRealTimers()
    vi.unstubAllGlobals()
  })

  it('takes itself off the screen so the server is not left tapping it away', async () => {
    answerWith(0)
    const order = useOrderStore()

    await order.send(false)
    expect(order.sendState).toBe('accepted')

    vi.advanceTimersByTime(ARRIVAL_NOTICE_MS)

    expect(order.sendState).toBe('idle')
  })

})

describe('an order in progress that could not be read back', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('is reported to the server instead of disappearing in silence', () => {
    localStorage.setItem(DRAFT_STORAGE_KEY, 'not json')

    const order = useOrderStore()

    expect(order.draftWasLost).toBe(true)
  })

  it('leaves the basket empty, because there is nothing left to put back', () => {
    localStorage.setItem(DRAFT_STORAGE_KEY, 'not json')

    const order = useOrderStore()

    expect(order.draft.lines).toEqual([])
  })

  it('is not reported when the phone simply had nothing stored', () => {
    const order = useOrderStore()

    expect(order.draftWasLost).toBe(false)
  })

  it('is not reported when the order in progress came back in full', () => {
    localStorage.setItem(
      DRAFT_STORAGE_KEY,
      '{"tableName":"Tisch 12","note":null,"clientOrderId":null,"lines":[]}',
    )

    const order = useOrderStore()

    expect(order.draftWasLost).toBe(false)
  })

  it('stops being reported once the server has tapped the notice away', () => {
    localStorage.setItem(DRAFT_STORAGE_KEY, 'not json')
    const order = useOrderStore()

    order.dismissDraftLoss()

    expect(order.draftWasLost).toBe(false)
  })

  it('stops being reported once the server starts entering the order again', () => {
    localStorage.setItem(DRAFT_STORAGE_KEY, 'not json')
    const order = useOrderStore()

    order.addItem({
      catalogItemId: 'item-1',
      note: null,
      stationId: null,
      name: 'Bratwurst',
    })

    expect(order.draftWasLost).toBe(false)
  })

  it('is not raised by the empty draft that follows a sent order', async () => {
    answerWith(0)
    const order = useOrderStore()

    await order.send(false)

    expect(order.draftWasLost).toBe(false)
  })
})

describe('an order whose send failed', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  function unreachableLaptop() {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => {
        throw new TypeError('the laptop cannot be reached')
      }),
    )
  }

  it('takes no further change, because the laptop may already hold the order as it was sent', async () => {
    unreachableLaptop()
    const order = useOrderStore()

    await order.send(false)

    expect(order.changesAreRefused).toBe(true)
  })

  it('stays closed for changes while the retry is on its way', async () => {
    unreachableLaptop()
    const order = useOrderStore()
    await order.send(false)

    const retry = order.sendAgain()
    const refusedWhileSending = order.changesAreRefused
    await retry

    expect(refusedWhileSending).toBe(true)
  })

  it('takes changes again as long as nothing has been sent', () => {
    const order = useOrderStore()

    expect(order.changesAreRefused).toBe(false)
  })

  it('opens the next order for changes once the waiter has written this one down', async () => {
    unreachableLaptop()
    const order = useOrderStore()
    await order.send(false)

    order.startNextOrderAfterWritingItDown()

    expect(order.changesAreRefused).toBe(false)
  })

  it('empties the basket when the waiter has written the order down on paper', async () => {
    unreachableLaptop()
    const order = useOrderStore()
    order.addItem({
      catalogItemId: 'item-wasser',
      note: null,
      stationId: 'station-bar',
      name: 'Wasser',
    })
    await order.send(false)

    order.startNextOrderAfterWritingItDown()

    expect(order.draft.lines).toEqual([])
  })

  it('says nothing about paper after the first failure', async () => {
    unreachableLaptop()
    const order = useOrderStore()

    await order.send(false)

    expect(order.onlyPaperIsLeft).toBe(false)
  })

  it('says paper is the way out once the second attempt has failed too', async () => {
    unreachableLaptop()
    const order = useOrderStore()
    await order.send(false)

    await order.sendAgain()

    expect(order.onlyPaperIsLeft).toBe(true)
  })
})

describe('the identity an order carries', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('is there from the moment the order exists, long before anybody presses send', () => {
    const order = useOrderStore()

    expect(order.draft.clientOrderId).not.toBeNull()
  })

  it('is written down at once, so a page that dies mid-send comes back as the same order', () => {
    const order = useOrderStore()

    const stored = JSON.parse(localStorage.getItem(DRAFT_STORAGE_KEY) ?? '{}')

    expect(stored.clientOrderId).toBe(order.draft.clientOrderId)
  })

  it('is a new one for the order that follows an accepted one', async () => {
    answerWith(0)
    const order = useOrderStore()
    const sentOrder = order.draft.clientOrderId

    await order.send(false)

    expect(order.draft.clientOrderId).not.toBe(sentOrder)
  })

  it('is a new one for the order that follows one written down on paper', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => {
        throw new TypeError('the laptop cannot be reached')
      }),
    )
    const order = useOrderStore()
    const writtenDownOrder = order.draft.clientOrderId
    await order.send(false)

    order.startNextOrderAfterWritingItDown()

    expect(order.draft.clientOrderId).not.toBe(writtenDownOrder)
  })
})

describe('an order the waiter has already pressed send on', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  function waterLine() {
    return {
      catalogItemId: 'item-wasser',
      note: null,
      stationId: 'station-bar',
      name: 'Wasser',
    }
  }

  function orderOnItsWayToTheLaptop() {
    vi.stubGlobal(
      'fetch',
      vi.fn(() => new Promise<Response>(() => undefined)),
    )
    const order = useOrderStore()
    order.addItem(waterLine())
    order.setTable('Tisch 7')
    void order.send(false)
    return order
  }

  it('is closed for changes from the press onwards, not only once the send has failed', () => {
    const order = orderOnItsWayToTheLaptop()

    expect(order.changesAreRefused).toBe(true)
  })

  it('takes no further item, because the laptop may already hold the order as it was sent', () => {
    const order = orderOnItsWayToTheLaptop()

    order.addItem({ ...waterLine(), catalogItemId: 'item-bier', name: 'Bier' })

    expect(order.draft.lines).toHaveLength(1)
  })

  it('takes no line off the order', () => {
    const order = orderOnItsWayToTheLaptop()

    order.dropLine(0)

    expect(order.draft.lines).toHaveLength(1)
  })

  it('takes no note on one of its lines', () => {
    const order = orderOnItsWayToTheLaptop()

    order.noteLine(0, 'ohne Eis')

    expect(order.draft.lines[0].note).toBeNull()
  })

  it('takes no other station for one of its lines', () => {
    const order = orderOnItsWayToTheLaptop()

    order.chooseStation(0, 'station-kueche')

    expect(order.draft.lines[0].stationId).toBe('station-bar')
  })

  it('takes no other table name', () => {
    const order = orderOnItsWayToTheLaptop()

    order.setTable('Tisch 9')

    expect(order.draft.tableName).toBe('Tisch 7')
  })

  it('takes no note on the order as a whole', () => {
    const order = orderOnItsWayToTheLaptop()

    order.setNote('bitte schnell')

    expect(order.draft.note).toBeNull()
  })

  it('takes no other delivery choice for a station', () => {
    const order = orderOnItsWayToTheLaptop()

    order.chooseDeliveryMode('station-bar', 'asItComes')

    expect(order.deliveryModeAt('station-bar')).toBe('together')
  })

  it('does not clear the lines that cannot be ordered', () => {
    const order = orderOnItsWayToTheLaptop()

    order.dropLinesThatCannotBeOrdered()

    expect(order.draft.lines).toHaveLength(1)
  })

  it('counts the attempt as it starts, so a tab that dies mid-send is not a free try', () => {
    const order = orderOnItsWayToTheLaptop()

    expect(order.attemptsMade).toBe(1)
  })

  it('stays on its way when the arrival notice of an earlier order is tapped away', () => {
    const order = orderOnItsWayToTheLaptop()

    order.dismissConfirmation()

    expect(order.sendState).toBe('sending')
  })
})

describe('an attempt the laptop never answers', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    vi.useFakeTimers()
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
  })

  afterEach(() => {
    vi.useRealTimers()
    vi.unstubAllGlobals()
  })

  it('is still shown as on its way nine seconds in', async () => {
    const order = useOrderStore()

    void order.send(false)
    await vi.advanceTimersByTimeAsync(9_000)

    expect(order.sendState).toBe('sending')
  })

  it('gives up after ten seconds rather than leaving the waiter watching the sending line', async () => {
    const order = useOrderStore()

    const attempt = order.send(false)
    await vi.advanceTimersByTimeAsync(10_000)
    await attempt

    expect(order.sendState).toBe('failed')
  })

  it('says the laptop could not be reached, which is what giving up means here', async () => {
    const order = useOrderStore()

    const attempt = order.send(false)
    await vi.advanceTimersByTimeAsync(10_000)
    await attempt

    expect(order.failure?.key).toBe('review.sendFailed')
  })
})

describe('an order the page was still sending when it was loaded again', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  function anOrderLeftOnItsWay() {
    localStorage.setItem(
      DRAFT_STORAGE_KEY,
      JSON.stringify({
        tableName: 'Tisch 4',
        note: null,
        lines: [
          {
            catalogItemId: 'item-wasser',
            note: null,
            stationId: 'station-bar',
            name: 'Wasser',
          },
        ],
        clientOrderId: 'c0ffee00-1111-4111-8111-111111111111',
        deliveryModes: {},
      }),
    )
    saveSendProgress({
      state: 'sending',
      attempts: 1,
      settleOnSend: true,
      anAttemptWentUnanswered: false,
      failure: null,
    })
  }

  it('is reported as failed, because the answer to that attempt died with the page', () => {
    anOrderLeftOnItsWay()

    const order = useOrderStore()

    expect(order.sendState).toBe('failed')
  })

  it('stays closed for changes, so the next table is not built into it', () => {
    anOrderLeftOnItsWay()

    const order = useOrderStore()

    expect(order.changesAreRefused).toBe(true)
  })

  it('says the attempt was cut off instead of blaming the WiFi', () => {
    anOrderLeftOnItsWay()

    const order = useOrderStore()

    expect(order.failure).toEqual({ key: 'review.sendInterrupted' })
  })

  it('comes back with the identity of the attempt, so a retry cannot create a second order', () => {
    anOrderLeftOnItsWay()

    const order = useOrderStore()

    expect(order.draft.clientOrderId).toBe('c0ffee00-1111-4111-8111-111111111111')
  })

  it('retries with the choice about paying that the waiter had already made', async () => {
    anOrderLeftOnItsWay()
    answerWith(200)
    const order = useOrderStore()

    await order.sendAgain()

    const sent = JSON.parse((vi.mocked(fetch).mock.calls[0][1] as RequestInit).body as string)
    expect(sent.settleOnSend).toBe(true)
  })
})

describe('an order whose send failed, after the page is loaded again', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  async function aFailedSend() {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => {
        throw new TypeError('the laptop cannot be reached')
      }),
    )
    const order = useOrderStore()
    order.setTable('Tisch 2')
    await order.send(false)
  }

  it('is still refused every change, because the freeze has to survive a reload', async () => {
    await aFailedSend()

    setActivePinia(createPinia())
    const afterTheReload = useOrderStore()

    expect(afterTheReload.changesAreRefused).toBe(true)
  })

  it('keeps the count of attempts, so the second failure is still the second one', async () => {
    await aFailedSend()

    setActivePinia(createPinia())
    const afterTheReload = useOrderStore()
    await afterTheReload.sendAgain()

    expect(afterTheReload.onlyPaperIsLeft).toBe(true)
  })
})

describe('an order the laptop accepted', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('leaves the next order open for changes', async () => {
    answerWith(0)
    const order = useOrderStore()

    await order.send(false)

    expect(order.changesAreRefused).toBe(false)
  })

  it('leaves nothing in storage that would lock the next order after a reload', async () => {
    answerWith(0)
    const order = useOrderStore()

    await order.send(false)

    expect(localStorage.getItem(SEND_PROGRESS_STORAGE_KEY)).toBeNull()
  })

  it('lets the next table be typed in straight away', async () => {
    answerWith(0)
    const order = useOrderStore()
    await order.send(false)

    order.setTable('Tisch 8')

    expect(order.draft.tableName).toBe('Tisch 8')
  })
})

describe('what an order costs while the item list changes underneath it', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  function waterOnTheMenu() {
    const catalog = useCatalogStore()
    catalog.catalog = {
      categories: [],
      items: [
        {
          id: 'item-wasser',
          name: 'Wasser',
          categoryId: 'category-getraenke',
          priceCents: 200,
          sortOrder: 1,
          isAvailable: true,
          stationIds: ['station-bar'],
          productionMinutes: null,
        },
      ],
      stations: [{ id: 'station-bar', name: 'Bar', sortOrder: 1 }],
    }
    return catalog
  }

  function waterLine() {
    return {
      catalogItemId: 'item-wasser',
      note: null,
      stationId: 'station-bar',
      name: 'Wasser',
    }
  }

  it('sends the price the laptop pushed out, because that is the only price there is', async () => {
    const catalog = waterOnTheMenu()
    answerWith(250)
    const order = useOrderStore()
    order.addItem(waterLine())

    catalog.catalog = {
      ...catalog.catalog,
      items: [{ ...catalog.catalog.items[0], priceCents: 250 }],
    }
    await nextTick()
    await order.send(false)

    const sent = JSON.parse((vi.mocked(fetch).mock.calls[0][1] as RequestInit).body as string)
    expect(sent.items[0].unitPriceCents).toBe(250)
  })

  it('leaves a line whose item left the menu out of the total the waiter reads out loud', () => {
    waterOnTheMenu()
    const order = useOrderStore()
    order.addItem(waterLine())
    order.addItem({
      catalogItemId: 'item-gone',
      note: null,
      stationId: null,
      name: 'Currywurst',
    })

    expect(order.totalCents).toBe(200)
  })
})

describe('an order the laptop answered no to', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  function refusedBecauseAnItemIsGone() {
    vi.stubGlobal(
      'fetch',
      vi.fn(
        async () =>
          new Response(
            JSON.stringify({
              code: 'UnknownItem',
              messageKey: 'order.unknownItem',
              parameters: {},
              details: null,
            }),
            { status: 400 },
          ),
      ),
    )
  }

  async function aRefusedOrder() {
    refusedBecauseAnItemIsGone()
    const order = useOrderStore()
    order.addItem({
      catalogItemId: 'item-gone',
      note: null,
      stationId: 'station-bar',
      name: 'Currywurst',
    })
    order.setTable('Tisch 6')
    await order.send(false)
    return order
  }

  it('takes changes again, because the laptop said that no order was created', async () => {
    const order = await aRefusedOrder()

    expect(order.changesAreRefused).toBe(false)
  })

  it('keeps the reason the laptop gave, so the waiter can put it right', async () => {
    const order = await aRefusedOrder()

    expect(order.failure?.key).toBe('order.unknownItem')
  })

  it('keeps the order on the screen, because it was never taken', async () => {
    const order = await aRefusedOrder()

    expect(order.draft.lines).toHaveLength(1)
  })

  it('does not send the waiter to paper, however often the laptop names the same reason', async () => {
    const order = await aRefusedOrder()

    await order.sendAgain()

    expect(order.onlyPaperIsLeft).toBe(false)
  })

  it('counts no unanswered attempt, because the laptop answered', async () => {
    const order = await aRefusedOrder()

    expect(order.attemptsMade).toBe(0)
  })

  it('sends the next attempt under the same identity, so a silent one cannot double the order', async () => {
    const order = await aRefusedOrder()
    const identity = order.draft.clientOrderId

    order.dropLine(0)

    expect(order.draft.clientOrderId).toBe(identity)
  })

  it('takes the reason off the screen as soon as the waiter changes the order', async () => {
    const order = await aRefusedOrder()

    order.dropLine(0)

    expect(order.failure).toBeNull()
  })

  it('stays open for changes after a reload, because the reason survives but the order was not taken', async () => {
    await aRefusedOrder()

    setActivePinia(createPinia())
    const afterTheReload = useOrderStore()

    expect(afterTheReload.changesAreRefused).toBe(false)
    expect(afterTheReload.failure?.key).toBe('order.unknownItem')
  })
})

describe('an order the laptop could not save', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  function aLaptopThatCannotSave() {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => new Response('{}', { status: 500 })),
    )
  }

  it('takes changes again, because an answer arrived and no order was created', async () => {
    aLaptopThatCannotSave()
    const order = useOrderStore()

    await order.send(false)

    expect(order.changesAreRefused).toBe(false)
  })

  it('says the laptop could not save it, rather than blaming the WiFi', async () => {
    aLaptopThatCannotSave()
    const order = useOrderStore()

    await order.send(false)

    expect(order.failure?.key).toBe('review.sendFailedDatabase')
  })

  it('counts no unanswered attempt, because the laptop answered', async () => {
    aLaptopThatCannotSave()
    const order = useOrderStore()

    await order.send(false)

    expect(order.attemptsMade).toBe(0)
  })

  it('never sends the waiter to paper, however often the laptop answers that way', async () => {
    aLaptopThatCannotSave()
    const order = useOrderStore()
    await order.send(false)

    await order.sendAgain()

    expect(order.onlyPaperIsLeft).toBe(false)
  })

  it('stays open for changes after a reload, because no order was created', async () => {
    aLaptopThatCannotSave()
    const order = useOrderStore()
    await order.send(false)

    setActivePinia(createPinia())
    const afterTheReload = useOrderStore()

    expect(afterTheReload.changesAreRefused).toBe(false)
  })
})

describe('an order the laptop answered only after it had already stayed silent once', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    vi.useFakeTimers()
  })

  afterEach(() => {
    vi.useRealTimers()
    vi.unstubAllGlobals()
  })

  function waterLine() {
    return {
      catalogItemId: 'item-wasser',
      note: null,
      stationId: 'station-bar',
      name: 'Wasser',
    }
  }

  function aLaptopThatSaysNothingAndThenCannotSave() {
    let attemptsSeen = 0
    vi.stubGlobal(
      'fetch',
      vi.fn((_path: string, init: RequestInit) => {
        attemptsSeen += 1
        if (attemptsSeen === 1) {
          return new Promise<Response>((_resolve, reject) => {
            init.signal?.addEventListener('abort', () => {
              reject(new DOMException('The request was aborted', 'AbortError'))
            })
          })
        }
        return Promise.resolve(new Response('{}', { status: 503 }))
      }),
    )
  }

  async function anOrderTheLaptopNeverAnsweredAndThenRefused() {
    aLaptopThatSaysNothingAndThenCannotSave()
    const order = useOrderStore()
    order.addItem(waterLine())
    order.setTable('Tisch 3')
    const firstAttempt = order.send(false)
    await vi.advanceTimersByTimeAsync(SEND_TIMEOUT_MS)
    await firstAttempt

    await order.sendAgain()
    return order
  }

  it('takes no change, because the laptop may still hold the order from the attempt it never answered', async () => {
    const order = await anOrderTheLaptopNeverAnsweredAndThenRefused()

    expect(order.changesAreRefused).toBe(true)
  })

  it('offers the paper route at once, because the refusal names nothing the waiter may change', async () => {
    const order = await anOrderTheLaptopNeverAnsweredAndThenRefused()

    expect(order.onlyPaperIsLeft).toBe(true)
  })

  it('keeps every line of the order the silent attempt may have carried away', async () => {
    const order = await anOrderTheLaptopNeverAnsweredAndThenRefused()

    order.dropLine(0)

    expect(order.draft.lines).toHaveLength(1)
  })

  it('stays closed for changes after a reload, because the silence is written down with the order', async () => {
    await anOrderTheLaptopNeverAnsweredAndThenRefused()

    setActivePinia(createPinia())
    const afterTheReload = useOrderStore()

    expect(afterTheReload.changesAreRefused).toBe(true)
  })

  it('opens the next order for changes once the waiter has written this one down', async () => {
    const order = await anOrderTheLaptopNeverAnsweredAndThenRefused()

    order.startNextOrderAfterWritingItDown()

    expect(order.changesAreRefused).toBe(false)
  })
})
