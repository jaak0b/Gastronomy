import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { nextTick } from 'vue'
import { useCatalogStore } from '../../../src/phone/stores/catalog'
import { useOrderStore } from '../../../src/phone/stores/order'
import { TOKEN_STORAGE_KEY } from '../../../src/shared/stores/session'
import { menuWithWaterAtTheBar, answerWith } from './orderFixture'
import { stubLaptop, refusal, neverAnswers } from '../../support/laptop'

describe('an order the laptop answered no to', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    useCatalogStore().catalog = menuWithWaterAtTheBar()
    localStorage.clear()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  function refusedBecauseAnItemIsGone() {
    stubLaptop().answersEverythingElse(
      refusal('errors.order.unknownItem', { status: 400, code: 'UnknownItem' }),
    )
  }

  async function aRefusedOrder() {
    refusedBecauseAnItemIsGone()
    const order = useOrderStore()
    order.addItem({
      catalogItemId: 'item-wasser',
      note: null,
      stationId: 'station-bar',
      name: 'Wasser',
    })
    order.setTable('Tisch 6')
    await order.send('leaveOpen')
    return order
  }

  it('takes changes again, because the laptop said that no order was created', async () => {
    const order = await aRefusedOrder()

    expect(order.changesAreRefused).toBe(false)
  })

  it('keeps the reason the laptop gave, so the waiter can put it right', async () => {
    const order = await aRefusedOrder()

    expect(order.failure?.key).toBe('errors.order.unknownItem')
  })

  it('keeps the order on the screen, because it was never taken', async () => {
    const order = await aRefusedOrder()

    expect(order.draft.lines).toHaveLength(1)
  })

  it('does not send the waiter to paper, however often the laptop names the same reason', async () => {
    const order = await aRefusedOrder()

    await order.sendAgain()

    expect(order.onlyWritingItDownIsLeft).toBe(false)
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
    expect(afterTheReload.failure?.key).toBe('errors.order.unknownItem')
  })
})

describe('an order the laptop refused because an item sold out', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    useCatalogStore().catalog = menuWithWaterAtTheBar()
    localStorage.clear()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('asks the catalogue for a fresh line without holding up the refusal', async () => {
    localStorage.setItem(TOKEN_STORAGE_KEY, 'lookup.token-here')
    setActivePinia(createPinia())
    useCatalogStore().catalog = menuWithWaterAtTheBar()
    const laptop = stubLaptop()
      .answersEverythingElse(
        refusal('errors.order.itemSoldOut', {
          status: 422,
          code: 'UnprocessableEntity',
          parameters: { name: 'Wasser', catalogItemId: 'item-wasser' },
        }),
      )
      .answers('GET', '/api/catalog', neverAnswers())
    const order = useOrderStore()
    order.addItem({
      catalogItemId: 'item-wasser',
      note: null,
      stationId: 'station-bar',
      name: 'Wasser',
    })
    order.setTable('Tisch 6')

    await order.send('leaveOpen')

    expect(order.failure?.key).toBe('errors.order.itemSoldOut')
    expect(order.failure?.parameters).toEqual({ name: 'Wasser', catalogItemId: 'item-wasser' })
    expect(laptop.urls()).toContain('/api/catalog')
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
          isQueueIndependent: false,
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
    await order.send('leaveOpen')

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
