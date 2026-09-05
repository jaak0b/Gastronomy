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
  categoryName: 'Getränke',
  priceCents: 200,
  sortOrder: 1,
  isAvailable: true,
  stationIds: ['station-bar'],
}

function prepareOrder() {
  const catalog = useCatalogStore()
  catalog.catalog = {
    version: '1',
    categories: [{ name: 'Getränke', sortOrder: 1 }],
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

    expect(review.find('.drop-lines-no-longer-on-the-menu').exists()).toBe(false)
  })

  it('takes the vanished line off the order, so the order can be sent at all', async () => {
    const order = orderWithAVanishedItem()
    const review = mount(Review, { global: { plugins: testPlugins() }, attachTo: document.body })

    await review.get('.drop-lines-no-longer-on-the-menu').trigger('click')

    expect(order.basketLines.map((line) => line.catalogItemId)).toEqual(['item-wasser'])
  })

  it('keeps the rest of the order standing, because the guest still ordered it', async () => {
    const order = orderWithAVanishedItem()
    const review = mount(Review, { global: { plugins: testPlugins() }, attachTo: document.body })

    await review.get('.drop-lines-no-longer-on-the-menu').trigger('click')

    expect(order.draft.tableName).toBe('Tisch 3')
    expect(order.totalCents).toBe(200)
  })

  it('stops offering the removal once nothing is missing any more', async () => {
    orderWithAVanishedItem()
    const review = mount(Review, { global: { plugins: testPlugins() }, attachTo: document.body })

    await review.get('.drop-lines-no-longer-on-the-menu').trigger('click')

    expect(review.find('.drop-lines-no-longer-on-the-menu').exists()).toBe(false)
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

    await review.get('.send-failure .retry').trigger('click')
    await vi.waitFor(() => expect(vi.mocked(fetch).mock.calls).toHaveLength(2))

    const retried = JSON.parse((vi.mocked(fetch).mock.calls[1][1] as RequestInit).body as string)
    expect(retried.settleOnSend).toBe(true)
  })

  it('keeps the total and the send button within reach while the lines scroll', () => {
    prepareOrder()
    const review = mount(Review, { global: { plugins: testPlugins() }, attachTo: document.body })

    const footer = review.get('.review-footer')

    expect(footer.find('.total-display').exists()).toBe(true)
    expect(footer.find('.send').exists()).toBe(true)
  })
})
