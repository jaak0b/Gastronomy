import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useOrderStore, ARRIVAL_NOTICE_MS } from '../../src/stores/order'
import { DRAFT_STORAGE_KEY } from '../../src/core/draftCart'

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

    await order.send()
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
      unitPriceCents: 350,
    })

    expect(order.draftWasLost).toBe(false)
  })

  it('is not raised by the empty draft that follows a sent order', async () => {
    answerWith(0)
    const order = useOrderStore()

    await order.send()

    expect(order.draftWasLost).toBe(false)
  })
})
