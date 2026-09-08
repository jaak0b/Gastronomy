import { beforeEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import Review from '../../../src/views/Review.vue'
import { useCatalogStore } from '../../../src/stores/catalog'
import { useOrderStore } from '../../../src/stores/order'
import { currentRoute, navigate } from '../../../src/router'
import { testPlugins } from '../../support/plugins'

const WASSER = {
  id: 'item-wasser',
  name: 'Wasser',
  categoryId: 'category-getraenke',
  priceCents: 200,
  sortOrder: 1,
  isAvailable: true,
  stationIds: ['station-bar'],
}

function prepareOrder() {
  const catalog = useCatalogStore()
  catalog.catalog = {
    categories: [
      { categoryId: 'category-getraenke', name: 'Getränke', colourHex: '#C62828', sortOrder: 1 },
    ],
    items: [WASSER],
    stations: [{ id: 'station-bar', name: 'Bar', sortOrder: 1 }],
  }
  const order = useOrderStore()
  order.addItem({
    catalogItemId: WASSER.id,
    note: null,
    stationId: 'station-bar',
    name: WASSER.name,
    unitPriceCents: WASSER.priceCents,
  })
  order.setTable('Tisch 3')
  return order
}

describe('an order holding an item the laptop no longer has', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
    navigate('/review')
  })

  function orderWithAVanishedItem() {
    const order = prepareOrder()
    order.addItem({
      catalogItemId: 'item-gone',
      note: null,
      stationId: null,
      name: 'Currywurst',
      unitPriceCents: 400,
    })
    return order
  }

  it('offers nothing to remove while every item is still on the menu', () => {
    prepareOrder()
    const review = mount(Review, { global: { plugins: testPlugins() }, attachTo: document.body })

    expect(review.find('.drop-lines-that-cannot-be-ordered').exists()).toBe(false)
  })

  it('takes the vanished line off the order, so the order can be sent at all', async () => {
    const order = orderWithAVanishedItem()
    const review = mount(Review, { global: { plugins: testPlugins() }, attachTo: document.body })

    await review.get('.drop-lines-that-cannot-be-ordered').trigger('click')

    expect(order.basketLines.map((line) => line.catalogItemId)).toEqual(['item-wasser'])
  })

  it('keeps the rest of the order standing, because the guest still ordered it', async () => {
    const order = orderWithAVanishedItem()
    const review = mount(Review, { global: { plugins: testPlugins() }, attachTo: document.body })

    await review.get('.drop-lines-that-cannot-be-ordered').trigger('click')

    expect(order.draft.tableName).toBe('Tisch 3')
    expect(order.totalCents).toBe(200)
  })

  it('stops offering the removal once nothing is missing any more', async () => {
    orderWithAVanishedItem()
    const review = mount(Review, { global: { plugins: testPlugins() }, attachTo: document.body })

    await review.get('.drop-lines-that-cannot-be-ordered').trigger('click')

    expect(review.find('.drop-lines-that-cannot-be-ordered').exists()).toBe(false)
  })
})

describe('sending the order from the review screen', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
    navigate('/review')
    vi.stubGlobal(
      'fetch',
      vi.fn(
        async () =>
          new Response(
            JSON.stringify({
              orderId: 'order-1',
              globalOrderNumber: 1,
              totalCents: 200,
              stationOrders: [],
            }),
            { status: 200 },
          ),
      ),
    )
  })

  it('takes the server back to the items, so the sent order cannot be typed into any more', async () => {
    const order = prepareOrder()
    const review = mount(Review, { global: { plugins: testPlugins() }, attachTo: document.body })

    await review.get('.send').trigger('click')
    await vi.waitFor(() => expect(order.sendState).toBe('accepted'))

    expect(currentRoute.value).toEqual({ name: 'home' })
  })

  it('lets the next order be sent while the arrival notice of the last one is still up', async () => {
    const order = prepareOrder()
    const review = mount(Review, { global: { plugins: testPlugins() }, attachTo: document.body })
    await review.get('.send').trigger('click')
    await vi.waitFor(() => expect(order.sendState).toBe('accepted'))

    prepareOrder()
    const nextReview = mount(Review, {
      global: { plugins: testPlugins() },
      attachTo: document.body,
    })

    expect(nextReview.get('.send').attributes('disabled')).toBeUndefined()
  })

  it('keeps a refused order on the summary, with everything the server typed still there', async () => {
    const order = prepareOrder()
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => {
        throw new TypeError('the laptop cannot be reached')
      }),
    )
    const review = mount(Review, { global: { plugins: testPlugins() }, attachTo: document.body })

    await review.get('.send').trigger('click')
    await vi.waitFor(() => expect(order.sendState).toBe('failed'))

    expect(currentRoute.value).toEqual({ name: 'review' })
    expect(review.find('.send-failure').exists()).toBe(true)
    expect(order.draft.tableName).toBe('Tisch 3')
    expect(order.basketLines).toHaveLength(1)
  })

  it('leaves only the retry after a refused order, so the payment choice cannot be swapped behind the server', async () => {
    const order = prepareOrder()
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => {
        throw new TypeError('the laptop cannot be reached')
      }),
    )
    const review = mount(Review, { global: { plugins: testPlugins() }, attachTo: document.body })

    await review.get('.send').trigger('click')
    await vi.waitFor(() => expect(order.sendState).toBe('failed'))

    expect(review.find('.send').exists()).toBe(false)
    expect(review.find('.send-and-settle').exists()).toBe(false)
    expect(review.find('.send-failure').exists()).toBe(true)
  })

  it('names the table under the heading rather than in a box of its own', () => {
    prepareOrder()
    const review = mount(Review, { global: { plugins: testPlugins() }, attachTo: document.body })

    expect(review.get('.table-name').text()).toBe('Tisch: Tisch 3')
    expect(review.find('.table-shown').exists()).toBe(false)
  })

  it('leaves the items open when the table wants to pay later', async () => {
    const order = prepareOrder()
    const review = mount(Review, { global: { plugins: testPlugins() }, attachTo: document.body })

    await review.get('.send').trigger('click')
    await vi.waitFor(() => expect(order.sendState).toBe('accepted'))

    const sent = JSON.parse((vi.mocked(fetch).mock.calls[0][1] as RequestInit).body as string)
    expect(sent.settleOnSend).toBe(false)
  })

  it('settles every item at once when the guest pays on the spot', async () => {
    const order = prepareOrder()
    const review = mount(Review, { global: { plugins: testPlugins() }, attachTo: document.body })

    await review.get('.send-and-settle').trigger('click')
    await vi.waitFor(() => expect(order.sendState).toBe('accepted'))

    const sent = JSON.parse((vi.mocked(fetch).mock.calls[0][1] as RequestInit).body as string)
    expect(sent.settleOnSend).toBe(true)
  })

  it('retries with the same choice the server made, so a retry cannot change who paid', async () => {
    const order = prepareOrder()
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => {
        throw new TypeError('the laptop cannot be reached')
      }),
    )
    const review = mount(Review, { global: { plugins: testPlugins() }, attachTo: document.body })
    await review.get('.send-and-settle').trigger('click')
    await vi.waitFor(() => expect(order.sendState).toBe('failed'))

    await review.get('.send-again').trigger('click')
    await vi.waitFor(() => expect(vi.mocked(fetch).mock.calls).toHaveLength(2))

    const retried = JSON.parse((vi.mocked(fetch).mock.calls[1][1] as RequestInit).body as string)
    expect(retried.settleOnSend).toBe(true)
  })

  it('keeps the total and the send button within reach while the lines scroll', () => {
    prepareOrder()
    const review = mount(Review, { global: { plugins: testPlugins() }, attachTo: document.body })

    const footer = review.get('.review-footer')

    expect(footer.classes()).toContain('docked-strip')
    expect(footer.find('.total-display').exists()).toBe(true)
    expect(footer.find('.send').exists()).toBe(true)
    expect(footer.find('.send-and-settle').exists()).toBe(true)
  })
})

describe('an order holding something that cannot be ordered', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
    navigate('/review')
    vi.stubGlobal('fetch', vi.fn(async () => new Response('{}', { status: 200 })))
  })

  function soldOutOrder() {
    const order = prepareOrder()
    const catalog = useCatalogStore()
    catalog.catalog = { ...catalog.catalog, items: [{ ...WASSER, isAvailable: false }] }
    return order
  }

  function orderWithAVanishedItem() {
    const order = prepareOrder()
    order.addItem({
      catalogItemId: 'item-gone',
      note: null,
      stationId: null,
      name: 'Currywurst',
      unitPriceCents: 400,
    })
    return order
  }

  function mountReview() {
    return mount(Review, { global: { plugins: testPlugins() }, attachTo: document.body })
  }

  it('holds both ways of sending back while an item has sold out', () => {
    soldOutOrder()
    const review = mountReview()

    expect(review.get('.send').attributes('disabled')).toBeDefined()
    expect(review.get('.send-and-settle').attributes('disabled')).toBeDefined()
  })

  it('holds both ways of sending back while an item has left the menu', () => {
    orderWithAVanishedItem()
    const review = mountReview()

    expect(review.get('.send').attributes('disabled')).toBeDefined()
    expect(review.get('.send-and-settle').attributes('disabled')).toBeDefined()
  })

  it('says under the buttons what has to be taken off the order first', () => {
    soldOutOrder()
    const review = mountReview()

    expect(review.get('.remove-before-sending').text()).toBe(
      'Entfernen Sie zuerst die Artikel, die nicht bestellbar sind.',
    )
  })

  it('says nothing of the sort while the whole order can be ordered', () => {
    prepareOrder()
    const review = mountReview()

    expect(review.find('.remove-before-sending').exists()).toBe(false)
    expect(review.get('.send').attributes('disabled')).toBeUndefined()
  })

  it('offers one button that takes the sold-out item off as well', async () => {
    const order = soldOutOrder()
    const review = mountReview()

    await review.get('.drop-lines-that-cannot-be-ordered').trigger('click')

    expect(order.basketLines).toHaveLength(0)
  })

  it('names that button for both kinds, because one button clears both', () => {
    soldOutOrder()
    const review = mountReview()

    expect(review.get('.drop-lines-that-cannot-be-ordered').text()).toBe(
      'Nicht bestellbare Artikel entfernen',
    )
  })

  it('lets the order be sent again once the blocking item is off it', async () => {
    orderWithAVanishedItem()
    const review = mountReview()

    await review.get('.drop-lines-that-cannot-be-ordered').trigger('click')

    expect(review.get('.send').attributes('disabled')).toBeUndefined()
    expect(review.get('.send-and-settle').attributes('disabled')).toBeUndefined()
    expect(review.find('.remove-before-sending').exists()).toBe(false)
  })
})

describe('an order the laptop did not confirm', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
    navigate('/review')
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => {
        throw new TypeError('the laptop cannot be reached')
      }),
    )
  })

  function mountReview() {
    return mount(Review, { global: { plugins: testPlugins() }, attachTo: document.body })
  }

  async function reviewAfterAFailedSend(order: ReturnType<typeof useOrderStore>) {
    const review = mountReview()
    await review.get('.send').trigger('click')
    await vi.waitFor(() => expect(order.sendState).toBe('failed'))
    return review
  }

  function sellOutTheWater() {
    const catalog = useCatalogStore()
    catalog.catalog = { ...catalog.catalog, items: [{ ...WASSER, isAvailable: false }] }
  }

  it('refuses to take the sold-out line off, because the laptop may hold the order as it was', async () => {
    const order = prepareOrder()
    const review = await reviewAfterAFailedSend(order)

    sellOutTheWater()
    await review.vm.$nextTick()

    expect(review.get('.drop-lines-that-cannot-be-ordered').attributes('disabled')).toBeDefined()
  })

  it('refuses the way back to the items, because everything there would change the order', async () => {
    const order = prepareOrder()
    const review = await reviewAfterAFailedSend(order)

    expect(review.get('.back').attributes('disabled')).toBeDefined()
  })

  it('refuses to have the delivery choice changed', async () => {
    const order = prepareOrder()
    const review = await reviewAfterAFailedSend(order)

    expect(review.get('.delivery-together').attributes('disabled')).toBeDefined()
  })

  it('keeps the lines and the total readable, because the waiter may have to copy them onto paper', async () => {
    const order = prepareOrder()
    const review = await reviewAfterAFailedSend(order)

    expect(review.get('.line-name').text()).toBe('1 x Wasser')
    expect(review.get('.total-display').text()).toContain('2.00')
  })
  it('offers the retry in the docked strip, where the send buttons stood', async () => {
    const order = prepareOrder()
    const review = await reviewAfterAFailedSend(order)

    expect(review.get('.review-footer .send-again').text()).toBe('Erneut senden')
  })

  it('leaves the retry alive while a line cannot be ordered, because the laptop may hold the order', async () => {
    const order = prepareOrder()
    const review = await reviewAfterAFailedSend(order)

    sellOutTheWater()
    await review.vm.$nextTick()

    expect(review.get('.send-again').attributes('disabled')).toBeUndefined()
  })

  it('sends the order again when the retry is tapped, sold-out line and all', async () => {
    const order = prepareOrder()
    const review = await reviewAfterAFailedSend(order)
    sellOutTheWater()
    await review.vm.$nextTick()

    await review.get('.send-again').trigger('click')

    await vi.waitFor(() => expect(vi.mocked(fetch).mock.calls).toHaveLength(2))
  })

  it('leaves the waiter with the retry alone after the first failure', async () => {
    const order = prepareOrder()
    await reviewAfterAFailedSend(order)

    expect(document.querySelector('.send-failed-twice-dialog')).toBeNull()
  })

  it('takes over the screen once the second attempt has failed as well', async () => {
    const order = prepareOrder()
    const review = await reviewAfterAFailedSend(order)

    await review.get('.send-again').trigger('click')
    await vi.waitFor(() =>
      expect(document.querySelector('.send-failed-twice-dialog')).not.toBeNull(),
    )
  })

  it('starts the next order once the waiter has written this one down', async () => {
    const order = prepareOrder()
    const review = await reviewAfterAFailedSend(order)
    await review.get('.send-again').trigger('click')
    await vi.waitFor(() =>
      expect(document.querySelector('.send-failed-twice-dialog')).not.toBeNull(),
    )

    ;(document.querySelector('.send-failed-twice-dialog .written-down') as HTMLElement).click()
    await review.vm.$nextTick()

    expect(order.basketLines).toEqual([])
    expect(currentRoute.value).toEqual({ name: 'home' })
  })

  it('sends again when the waiter says the WiFi is back', async () => {
    const order = prepareOrder()
    const review = await reviewAfterAFailedSend(order)
    await review.get('.send-again').trigger('click')
    await vi.waitFor(() =>
      expect(document.querySelector('.send-failed-twice-dialog')).not.toBeNull(),
    )

    ;(document.querySelector('.send-failed-twice-dialog .try-again') as HTMLElement).click()

    await vi.waitFor(() => expect(vi.mocked(fetch).mock.calls).toHaveLength(3))
  })

  it('comes back when that attempt fails too', async () => {
    const order = prepareOrder()
    const review = await reviewAfterAFailedSend(order)
    await review.get('.send-again').trigger('click')
    await vi.waitFor(() =>
      expect(document.querySelector('.send-failed-twice-dialog')).not.toBeNull(),
    )

    ;(document.querySelector('.send-failed-twice-dialog .try-again') as HTMLElement).click()
    await vi.waitFor(() => expect(vi.mocked(fetch).mock.calls).toHaveLength(3))

    await vi.waitFor(() =>
      expect(document.querySelector('.send-failed-twice-dialog .written-down')).not.toBeNull(),
    )
  })
})

describe('an order that is still on its way to the laptop', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
    navigate('/review')
    vi.stubGlobal(
      'fetch',
      vi.fn(() => new Promise<Response>(() => undefined)),
    )
  })

  async function reviewOfAnOrderOnItsWay() {
    prepareOrder()
    const review = mount(Review, { global: { plugins: testPlugins() }, attachTo: document.body })
    await review.get('.send').trigger('click')
    return review
  }

  it('refuses the way back to the items, because everything there would change the order', async () => {
    const review = await reviewOfAnOrderOnItsWay()

    expect(review.get('.back').attributes('disabled')).toBeDefined()
  })

  it('refuses to have the delivery choice changed', async () => {
    const review = await reviewOfAnOrderOnItsWay()

    expect(review.get('.delivery-together').attributes('disabled')).toBeDefined()
  })

  it('leaves one way to send on the screen and says that the order is going out', async () => {
    const review = await reviewOfAnOrderOnItsWay()

    expect(review.get('.send-again').text()).toBe('Wird gesendet')
    expect(review.find('.send').exists()).toBe(false)
  })
})
