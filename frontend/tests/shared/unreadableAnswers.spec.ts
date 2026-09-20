import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import type { OpenTable } from '../../src/shared/api/apiTypes'

const { useOpenItemsStore } = await import('../../src/phone/stores/openItems')
const { useOrderStore } = await import('../../src/phone/stores/order')
const { TOKEN_STORAGE_KEY, useSessionStore } = await import('../../src/shared/stores/session')
const { useStationStore } = await import('../../src/station/stores/station')

function aLaptopThatAnswersWith(aBody: unknown): void {
  vi.stubGlobal(
    'fetch',
    vi.fn(async () => new Response(JSON.stringify(aBody), { status: 200 })),
  )
}

const A_TABLE: OpenTable = {
  tableName: 'Tisch 5',
  openAmountCents: 200,
  items: [
    {
      orderItemId: 'item-of-the-order',
      orderId: 'order-1',
      globalOrderNumber: 1,
      itemName: 'Wasser',
      note: null,
      unitPriceCents: 200,
      orderedAtUtc: '2026-09-20T18:00:00Z',
    },
  ],
}

describe('an answer the phone cannot read', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('leaves a send failed and frozen instead of looking accepted', async () => {
    aLaptopThatAnswersWith({ orderId: 'order-1' })
    const order = useOrderStore()

    await order.send(null)

    expect(order.sendState).toBe('failed')
    expect(order.failure?.key).toBe('review.sendFailed')
    expect(order.changesAreRefused).toBe(true)
    expect(order.acceptedOrderNumber).toBeNull()
  })

  it('says the laptop could not answer instead of saying whose device this is', async () => {
    localStorage.setItem(TOKEN_STORAGE_KEY, 'lookup.the-laptop-issued')
    aLaptopThatAnswersWith({ deviceId: 'device-1', language: 'de' })
    const session = useSessionStore()

    await session.loadSession()

    expect(session.startingUpFailure).toBe('theLaptopCouldNotAnswer')
    expect(session.deviceKind).toBeNull()
  })

  it('marks the station board as unloaded instead of showing a queue from a broken answer', async () => {
    useSessionStore().deviceToken = 'lookup.of-the-tablet'
    aLaptopThatAnswersWith({ station: { id: 'station-kueche', name: 'Küche' }, orders: [] })
    const station = useStationStore()

    await station.load()

    expect(station.loadFailed).toBe(true)
    expect(station.orders).toEqual([])
  })

  it('says the settlement answer never came instead of settling the table on screen', async () => {
    aLaptopThatAnswersWith({ settledOrderItemIds: [] })
    const openItems = useOpenItemsStore()
    openItems.tables = [A_TABLE]
    openItems.toggleItem('item-of-the-order')

    const outcome = await openItems.settle(200, null)

    expect(outcome).toBe('answerNeverCame')
    expect(openItems.notice?.key).toBe('openItems.settleAnswerNeverCame')
  })
})
