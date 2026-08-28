import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useOrderStore, ARRIVAL_NOTICE_MS } from '../../src/stores/order'

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
