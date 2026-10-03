import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import Review from '../../../src/phone/views/Review.vue'
import { useCatalogStore } from '../../../src/phone/stores/catalog'
import { useOpenItemsStore } from '../../../src/phone/stores/openItems'
import { useOrderStore } from '../../../src/phone/stores/order'
import { useSessionStore } from '../../../src/shared/stores/session'
import { currentRoute, navigate } from '../../../src/shared/router/router'
import { saveDraft, saveSendProgress } from '../../../src/phone/core/draftCart'
import { testPlugins } from '../../support/plugins'

const WASSER = {
  id: 'item-wasser',
  name: 'Wasser',
  categoryId: 'category-getraenke',
  priceCents: 200,
  sortOrder: 1,
  isAvailable: true,
  stationIds: ['station-bar'],
  productionMinutes: 0,
  isQueueIndependent: false,
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
  })
  order.setTable('Tisch 3')
  return order
}

async function sendAndSettleLater(review: VueWrapper): Promise<void> {
  await review.get('[data-test="send-and-settle-later"]').trigger('click')
  await review.vm.$nextTick()
}

async function sendAndSettle(review: VueWrapper): Promise<void> {
  await review.get('[data-test="send-and-settle"]').trigger('click')
  await review.vm.$nextTick()
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
    })
    return order
  }

  it('offers nothing to remove while every item is still on the menu', () => {
    prepareOrder()
    const review = mount(Review, { global: { plugins: testPlugins() }, attachTo: document.body })

    expect(review.find('[data-test="drop-lines-that-cannot-be-ordered"]').exists()).toBe(false)
  })

  it('takes the vanished line off the order, so the order can be sent at all', async () => {
    const order = orderWithAVanishedItem()
    const review = mount(Review, { global: { plugins: testPlugins() }, attachTo: document.body })

    await review.get('[data-test="drop-lines-that-cannot-be-ordered"]').trigger('click')

    expect(order.basketLines.map((line) => line.catalogItemId)).toEqual(['item-wasser'])
  })

  it('keeps the rest of the order standing, because the guest still ordered it', async () => {
    const order = orderWithAVanishedItem()
    const review = mount(Review, { global: { plugins: testPlugins() }, attachTo: document.body })

    await review.get('[data-test="drop-lines-that-cannot-be-ordered"]').trigger('click')

    expect(order.draft.tableName).toBe('Tisch 3')
    expect(order.totalCents).toBe(200)
  })

  it('stops offering the removal once nothing is missing any more', async () => {
    orderWithAVanishedItem()
    const review = mount(Review, { global: { plugins: testPlugins() }, attachTo: document.body })

    await review.get('[data-test="drop-lines-that-cannot-be-ordered"]').trigger('click')

    expect(review.find('[data-test="drop-lines-that-cannot-be-ordered"]').exists()).toBe(false)
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
              status: 'open',
              totalCents: 200,
              createdAtUtc: '2026-09-05T18:00:00Z',
              stationOrders: [],
            }),
            { status: 200 },
          ),
      ),
    )
  })

  it('lets the next order be sent while the arrival notice of the last one is still up', async () => {
    const order = prepareOrder()
    const review = mount(Review, { global: { plugins: testPlugins() }, attachTo: document.body })
    await sendAndSettleLater(review)
    await vi.waitFor(() => expect(order.sendState).toBe('accepted'))

    prepareOrder()
    const nextReview = mount(Review, {
      global: { plugins: testPlugins() },
      attachTo: document.body,
    })

    expect(nextReview.get('[data-test="send-and-settle-later"]').attributes('disabled')).toBeUndefined()
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

    await sendAndSettleLater(review)
    await vi.waitFor(() => expect(order.sendState).toBe('failed'))

    expect(currentRoute.value).toEqual({ name: 'review' })
    expect(review.find('[data-test="send-failure"]').exists()).toBe(true)
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

    await sendAndSettleLater(review)
    await vi.waitFor(() => expect(order.sendState).toBe('failed'))

    expect(review.find('[data-test="send-and-settle-later"]').exists()).toBe(false)
    expect(review.find('[data-test="send-failure"]').exists()).toBe(true)
  })

  it('keeps the table and the total together in the compact heading', () => {
    prepareOrder()
    const review = mount(Review, { global: { plugins: testPlugins() }, attachTo: document.body })

    expect(review.get('h1.table-name').text()).toBe('Tisch: Tisch 3')
    expect(review.get('[data-test="order-total"]').text()).toContain('2.00')
  })

  it('takes the waiter back to the items from the back button at the bottom of the strip', async () => {
    prepareOrder()
    const review = mount(Review, { global: { plugins: testPlugins() }, attachTo: document.body })

    expect(review.get('[data-test="back"]').text()).toBe('Zurück')
    expect(review.get('[data-test="back"]').element.closest('[data-test="docked-strip"]')).not.toBeNull()

    await review.get('[data-test="back"]').trigger('click')

    expect(currentRoute.value).toEqual({ name: 'home' })
  })

  it('sends the order on the first tap of either button, without asking anything first', async () => {
    const later = prepareOrder()
    const review = mount(Review, { global: { plugins: testPlugins() }, attachTo: document.body })

    await sendAndSettleLater(review)
    await vi.waitFor(() => expect(later.sendState).toBe('accepted'))
    prepareOrder()
    await review.vm.$nextTick()
    await sendAndSettle(review)
    await vi.waitFor(() => expect(vi.mocked(fetch).mock.calls).toHaveLength(2))

    expect(vi.mocked(fetch).mock.calls.map(([url]) => url)).toEqual(['/api/orders', '/api/orders'])
  })

  it('offers neither way to send while the order has no table name', () => {
    const order = prepareOrder()
    order.setTable('')
    const review = mount(Review, { global: { plugins: testPlugins() }, attachTo: document.body })

    expect({
      settle: review.get('[data-test="send-and-settle"]').attributes('disabled'),
      later: review.get('[data-test="send-and-settle-later"]').attributes('disabled'),
    }).toEqual({ settle: '', later: '' })
  })

  it('keeps the total and the send button within reach while the lines scroll', () => {
    prepareOrder()
    const review = mount(Review, { global: { plugins: testPlugins() }, attachTo: document.body })

    const footer = review.get('[data-test="docked-strip"]')

    expect(footer.find('[data-test="send-and-settle-later"]').exists()).toBe(true)
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
    })
    return order
  }

  function mountReview() {
    return mount(Review, { global: { plugins: testPlugins() }, attachTo: document.body })
  }

  it('offers only the way to take the sold-out item off, not a way to send', () => {
    soldOutOrder()
    const review = mountReview()

    expect(review.find('[data-test="send-and-settle-later"]').exists()).toBe(false)
    expect(review.get('[data-test="drop-lines-that-cannot-be-ordered"]').exists()).toBe(true)
  })

  it('offers only the way to take the vanished item off, not a way to send', () => {
    orderWithAVanishedItem()
    const review = mountReview()

    expect(review.find('[data-test="send-and-settle-later"]').exists()).toBe(false)
    expect(review.get('[data-test="drop-lines-that-cannot-be-ordered"]').exists()).toBe(true)
  })

  it('leaves the way to send open while the whole order can be ordered', () => {
    prepareOrder()
    const review = mountReview()

    expect(review.get('[data-test="send-and-settle-later"]').attributes('disabled')).toBeUndefined()
  })

  it('offers one button that takes the sold-out item off as well', async () => {
    const order = soldOutOrder()
    const review = mountReview()

    await review.get('[data-test="drop-lines-that-cannot-be-ordered"]').trigger('click')

    expect(order.basketLines).toHaveLength(0)
  })

  it('names that button for both kinds, because one button clears both', () => {
    soldOutOrder()
    const review = mountReview()

    expect(review.get('[data-test="drop-lines-that-cannot-be-ordered"]').text()).toBe(
      'Nicht bestellbare Artikel entfernen',
    )
  })

  it('lets the order be sent again once the blocking item is off it', async () => {
    orderWithAVanishedItem()
    const review = mountReview()

    await review.get('[data-test="drop-lines-that-cannot-be-ordered"]').trigger('click')

    expect(review.get('[data-test="send-and-settle-later"]').attributes('disabled')).toBeUndefined()
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
    await sendAndSettleLater(review)
    await vi.waitFor(() => expect(order.sendState).toBe('failed'))
    return review
  }

  function sellOutTheWater() {
    const catalog = useCatalogStore()
    catalog.catalog = { ...catalog.catalog, items: [{ ...WASSER, isAvailable: false }] }
  }

  it('shows the failure above the table heading, so the waiter reads it first', async () => {
    const order = prepareOrder()
    const review = await reviewAfterAFailedSend(order)

    const failure = review.get('[data-test="send-failure"]').element
    const heading = review.get('[data-test="review-heading"]').element

    expect(failure.compareDocumentPosition(heading) & Node.DOCUMENT_POSITION_FOLLOWING).toBe(
      Node.DOCUMENT_POSITION_FOLLOWING,
    )
  })

  it('offers no way to take the sold-out line off, because the laptop may hold the order as it was', async () => {
    const order = prepareOrder()
    const review = await reviewAfterAFailedSend(order)

    sellOutTheWater()
    await review.vm.$nextTick()

    expect(review.find('[data-test="drop-lines-that-cannot-be-ordered"]').exists()).toBe(false)
    expect(review.get('[data-test="send-again"]').exists()).toBe(true)
  })

  it('hides the way back to the items, because everything there would change the order', async () => {
    const order = prepareOrder()
    const review = await reviewAfterAFailedSend(order)

    expect(review.find('[data-test="back"]').exists()).toBe(false)
  })

  it('refuses to have the delivery choice changed', async () => {
    const order = prepareOrder()
    const review = await reviewAfterAFailedSend(order)

    expect(review.get('[data-test="delivery-together"]').attributes('disabled')).toBeDefined()
  })

  it('keeps the lines and the total readable, because the waiter may have to copy them onto paper', async () => {
    const order = prepareOrder()
    const review = await reviewAfterAFailedSend(order)

    expect(review.get('[data-test="line-name"]').text()).toBe('1 x Wasser')
    expect(review.get('[data-test="order-total"]').text()).toContain('2.00')
  })

  it('shows no waiting time when the laptop never sent one, instead of a short made-up one', async () => {
    const order = prepareOrder()
    const review = await reviewAfterAFailedSend(order)

    expect(review.get('[data-test="station-name"]').text()).toBe('Geht an Bar')
  })
  it('offers the retry in the docked strip, where the send action stands', async () => {
    const order = prepareOrder()
    const review = await reviewAfterAFailedSend(order)

    expect(review.get('[data-test="docked-strip"] [data-test="send-again"]').text()).toBe('Erneut senden')
  })

  it('leaves the retry alive while a line cannot be ordered, because the laptop may hold the order', async () => {
    const order = prepareOrder()
    const review = await reviewAfterAFailedSend(order)

    sellOutTheWater()
    await review.vm.$nextTick()

    expect(review.get('[data-test="send-again"]').attributes('disabled')).toBeUndefined()
  })

  it('sends the order again when the retry is tapped, sold-out line and all', async () => {
    const order = prepareOrder()
    const review = await reviewAfterAFailedSend(order)
    sellOutTheWater()
    await review.vm.$nextTick()

    await review.get('[data-test="send-again"]').trigger('click')

    await vi.waitFor(() => expect(vi.mocked(fetch).mock.calls).toHaveLength(2))
  })

  it('keeps the paper route away until the second attempt got no answer as well', async () => {
    const order = prepareOrder()
    const review = await reviewAfterAFailedSend(order)

    expect(review.find('[data-test="written-down"]').exists()).toBe(false)
    expect(review.find('[data-test="back"]').exists()).toBe(false)
  })

  it('says the order may have arrived once the second attempt got no answer either', async () => {
    const order = prepareOrder()
    const review = await reviewAfterAFailedSend(order)

    await review.get('[data-test="send-again"]').trigger('click')
    await vi.waitFor(() =>
      expect(review.get('[data-test="write-it-down"]').text()).toContain(
        'Die Bestellung konnte noch nicht bestätigt werden.',
      ),
    )
  })

  it('starts the next order once the waiter has written this one down', async () => {
    const order = prepareOrder()
    const review = await reviewAfterAFailedSend(order)
    await review.get('[data-test="send-again"]').trigger('click')
    await vi.waitFor(() => expect(review.find('[data-test="written-down"]').exists()).toBe(true))

    await review.get('[data-test="written-down"]').trigger('click')

    expect(order.basketLines).toEqual([])
    expect(currentRoute.value).toEqual({ name: 'home' })
  })

  it('sends again when the waiter says the WiFi is back', async () => {
    const order = prepareOrder()
    const review = await reviewAfterAFailedSend(order)
    await review.get('[data-test="send-again"]').trigger('click')
    await vi.waitFor(() => expect(vi.mocked(fetch).mock.calls).toHaveLength(2))

    await review.get('[data-test="send-again"]').trigger('click')

    await vi.waitFor(() => expect(vi.mocked(fetch).mock.calls).toHaveLength(3))
  })

  it('comes back to the same screen, lines and paper route, when that attempt fails too', async () => {
    const order = prepareOrder()
    const review = await reviewAfterAFailedSend(order)
    await review.get('[data-test="send-again"]').trigger('click')
    await vi.waitFor(() => expect(vi.mocked(fetch).mock.calls).toHaveLength(2))

    await review.get('[data-test="send-again"]').trigger('click')
    await vi.waitFor(() => expect(vi.mocked(fetch).mock.calls).toHaveLength(3))

    await vi.waitFor(() =>
      expect(review.get('[data-test="write-it-down"]').text()).toContain(
        'Schreiben Sie die Bestellung auf einen Zettel',
      ),
    )
    expect(review.get('[data-test="written-down"]').exists()).toBe(true)
    expect(review.get('[data-test="line-name"]').text()).toBe('1 x Wasser')
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
    await sendAndSettleLater(review)
    return review
  }

  it('hides the way back to the items, because everything there would change the order', async () => {
    const review = await reviewOfAnOrderOnItsWay()

    expect(review.find('[data-test="back"]').exists()).toBe(false)
  })

  it('refuses to have the delivery choice changed', async () => {
    const review = await reviewOfAnOrderOnItsWay()

    expect(review.get('[data-test="delivery-together"]').attributes('disabled')).toBeDefined()
  })

  it('leaves one way to send on the screen and says that the order is going out', async () => {
    const review = await reviewOfAnOrderOnItsWay()

    expect(review.get('[data-test="send-again"]').text()).toBe('Wird gesendet')
    expect(review.find('[data-test="send-and-settle-later"]').exists()).toBe(false)
  })
})

describe('an order the laptop refused with a reason', () => {
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
              code: 'UnknownItem',
              messageKey: 'errors.order.unknownItem',
              parameters: {},
              details: null,
            }),
            { status: 400 },
          ),
      ),
    )
  })

  function orderWithAVanishedItem() {
    const order = prepareOrder()
    order.addItem({
      catalogItemId: 'item-gone',
      note: null,
      stationId: null,
      name: 'Currywurst',
    })
    return order
  }

  async function reviewAfterARefusal(order: ReturnType<typeof useOrderStore>) {
    await order.send('leaveOpen')
    return mount(Review, { global: { plugins: testPlugins() }, attachTo: document.body })
  }

  it('says what the laptop refused, in words the waiter can act on', async () => {
    const review = await reviewAfterARefusal(prepareOrder())

    expect(review.get('[data-test="send-failure"] [data-test="failure-message"]').text()).toBe(
      'Ein Artikel steht nicht mehr auf der Karte. Nehmen Sie ihn von der Bestellung.',
    )
  })

  it('names the sold-out item the refusal carries in its parameters', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(
        async () =>
          new Response(
            JSON.stringify({
              code: 'UnprocessableEntity',
              messageKey: 'errors.order.itemSoldOut',
              parameters: { name: 'Wasser', catalogItemId: 'item-wasser' },
              details: null,
            }),
            { status: 422 },
          ),
      ),
    )

    const review = await reviewAfterARefusal(prepareOrder())

    expect(review.get('[data-test="send-failure"] [data-test="failure-message"]').text()).toBe(
      'Wasser ist gerade ausverkauft.',
    )
  })

  it('opens the way back to the items again, because no order was created', async () => {
    const review = await reviewAfterARefusal(prepareOrder())

    expect(review.get('[data-test="back"]').attributes('disabled')).toBeUndefined()
  })

  it('lets the delivery choice be changed again', async () => {
    const review = await reviewAfterARefusal(prepareOrder())

    expect(review.get('[data-test="delivery-together"]').attributes('disabled')).toBeUndefined()
  })

  it('lets the waiter take off the item the laptop named', async () => {
    const review = await reviewAfterARefusal(orderWithAVanishedItem())

    expect(
      review.get('[data-test="drop-lines-that-cannot-be-ordered"]').attributes('disabled'),
    ).toBeUndefined()
  })

  it('leaves the way to send in the strip, because this is not a retry into the dark', async () => {
    const review = await reviewAfterARefusal(prepareOrder())

    expect(review.get('[data-test="docked-strip"] [data-test="send-and-settle-later"]').exists()).toBe(true)
    expect(review.find('[data-test="send-again"]').exists()).toBe(false)
  })

})

describe('an order the laptop answered but could not save', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
    navigate('/review')
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => new Response('{}', { status: 500 })),
    )
  })

  async function reviewAfterTheAnswer() {
    const order = prepareOrder()
    await order.send('leaveOpen')
    return mount(Review, { global: { plugins: testPlugins() }, attachTo: document.body })
  }

  it('says the laptop could not save it, without naming a button that is not there', async () => {
    const review = await reviewAfterTheAnswer()

    expect(review.get('[data-test="send-failure"] [data-test="failure-message"]').text()).toBe(
      'Der Rechner konnte die Bestellung nicht speichern. Senden Sie sie noch einmal.',
    )
  })

  it('leaves the way to send in the strip, because the answer said no order was created', async () => {
    const review = await reviewAfterTheAnswer()

    expect(review.get('[data-test="docked-strip"] [data-test="send-and-settle-later"]').exists()).toBe(true)
    expect(review.find('[data-test="send-again"]').exists()).toBe(false)
  })

  it('opens the way back to the items again', async () => {
    const review = await reviewAfterTheAnswer()

    expect(review.get('[data-test="back"]').attributes('disabled')).toBeUndefined()
  })

})

describe('an order holding a line the admin moved to another station', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
    navigate('/review')
    vi.stubGlobal('fetch', vi.fn(async () => new Response('{}', { status: 200 })))
  })

  function orderWhoseStationWasTakenOff() {
    const order = prepareOrder()
    const catalog = useCatalogStore()
    catalog.catalog = {
      ...catalog.catalog,
      items: [{ ...WASSER, stationIds: ['station-terrasse'] }],
      stations: [
        { id: 'station-bar', name: 'Bar', sortOrder: 1 },
        { id: 'station-terrasse', name: 'Terrasse', sortOrder: 2 },
      ],
    }
    return order
  }

  function mountReview() {
    return mount(Review, { global: { plugins: testPlugins() }, attachTo: document.body })
  }

  it('offers only the way to take the moved line off, so the laptop is never asked to refuse it', () => {
    orderWhoseStationWasTakenOff()
    const review = mountReview()

    expect(review.find('[data-test="send-and-settle-later"]').exists()).toBe(false)
    expect(review.get('[data-test="drop-lines-that-cannot-be-ordered"]').exists()).toBe(true)
  })

  it('marks the line as sold out on the card of the station that no longer prepares it', () => {
    orderWhoseStationWasTakenOff()
    const review = mountReview()

    expect(review.get('[data-test="sold-out"]').text()).toBe('Wasser ist gerade ausverkauft.')
  })

  it('lets the one button that clears such lines take it off', async () => {
    const order = orderWhoseStationWasTakenOff()
    const review = mountReview()

    await review.get('[data-test="drop-lines-that-cannot-be-ordered"]').trigger('click')

    expect(order.basketLines).toEqual([])
  })
})


describe('an order the laptop refused after an attempt it never answered', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
    navigate('/review')
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => new Response('{}', { status: 503 })),
    )
  })

  function anOrderTheLaptopMayAlreadyHold() {
    saveDraft({
      festivalId: null,
      tableName: 'Tisch 3',
      lines: [
        {
          catalogItemId: WASSER.id,
          note: null,
          stationId: 'station-bar',
          name: WASSER.name,
          stationName: 'Bar',
        },
      ],
      clientOrderId: 'c0ffee00-1111-4111-8111-111111111111',
      deliveryModes: {},
    })
    saveSendProgress({
      state: 'failed',
      attempts: 1,
      failure: { key: 'phone.review.errors.sendFailed' },
      unresolvedAttempt: {
        clientOrderId: 'c0ffee00-1111-4111-8111-111111111111',
        tableName: 'Tisch 3',
        items: [
          {
            catalogItemId: WASSER.id,
            unitPriceCents: WASSER.priceCents,
            note: null,
            stationId: 'station-bar',
          },
        ],
        deliveryModes: [],
      },
    })
    const catalog = useCatalogStore()
    catalog.catalog = {
      categories: [
        { categoryId: 'category-getraenke', name: 'Getränke', colourHex: '#C62828', sortOrder: 1 },
      ],
      items: [WASSER],
      stations: [{ id: 'station-bar', name: 'Bar', sortOrder: 1 }],
    }
    return useOrderStore()
  }

  async function reviewAfterTheRefusedRetry(order: ReturnType<typeof useOrderStore>) {
    const review = mount(Review, { global: { plugins: testPlugins() }, attachTo: document.body })
    await review.get('[data-test="send-again"]').trigger('click')
    await vi.waitFor(() => expect(order.sendState).toBe('rejected'))
    return review
  }

  it('offers the paper route at once, because nothing on the phone can put the refusal right', async () => {
    const order = anOrderTheLaptopMayAlreadyHold()

    const review = await reviewAfterTheRefusedRetry(order)

    expect(review.find('[data-test="written-down"]').exists()).toBe(true)
  })

  it('keeps the order closed for changes, because the laptop may hold it as it stands', async () => {
    const order = anOrderTheLaptopMayAlreadyHold()

    const review = await reviewAfterTheRefusedRetry(order)

    expect(order.changesAreRefused).toBe(true)
    expect(review.find('[data-test="back"]').exists()).toBe(false)
  })

  it('keeps every line on the screen, so the waiter can copy the order onto paper', async () => {
    const order = anOrderTheLaptopMayAlreadyHold()

    const review = await reviewAfterTheRefusedRetry(order)

    expect(order.draft.lines).toHaveLength(1)
    expect(review.get('[data-test="line-name"]').text()).toBe('1 x Wasser')
  })

  function aLaptopThatRefusesWithAReason(): void {
    vi.stubGlobal(
      'fetch',
      vi.fn(
        async () =>
          new Response(
            JSON.stringify({
              code: 'UnprocessableEntity',
              messageKey: 'errors.order.itemSoldOut',
              parameters: { name: 'Wasser', catalogItemId: 'item-wasser' },
              details: null,
            }),
            { status: 422 },
          ),
      ),
    )
  }

  it('hands the order back when the reason names something the waiter can fix', async () => {
    aLaptopThatRefusesWithAReason()
    const order = anOrderTheLaptopMayAlreadyHold()

    const review = await reviewAfterTheRefusedRetry(order)

    expect(order.changesAreRefused).toBe(false)
    expect(review.find('[data-test="written-down"]').exists()).toBe(false)
    expect(review.find('[data-test="back"]').exists()).toBe(true)
  })
})

describe('the waiting time on the review screen', () => {
  const quoteBodies: unknown[] = []

  function answerTheQuoteWith(readyInMinutes: number | null): void {
    quoteBodies.length = 0
    vi.stubGlobal(
      'fetch',
      vi.fn(async (_url: string, init: RequestInit) => {
        quoteBodies.push(JSON.parse(String(init.body)))
        return new Response(
          JSON.stringify({ stations: [{ stationId: 'station-bar', readyInMinutes }] }),
          { status: 200 },
        )
      }),
    )
  }

  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
    navigate('/review')
    useSessionStore().deviceToken = 'token-here'
  })

  it('shows the time the laptop calculated for the station', async () => {
    answerTheQuoteWith(14)
    prepareOrder()

    const review = mount(Review, { global: { plugins: testPlugins() } })
    await flushPromises()

    expect(review.get('[data-test="station-name"]').text()).toBe('Geht an Bar (~14 Min.)')
  })

  it('says above the buttons how each station hands its part out and when, in German', async () => {
    answerTheQuoteWith(14)
    prepareOrder()

    const review = mount(Review, { global: { plugins: testPlugins() } })
    await flushPromises()

    expect({
      station: review.get('[data-test="docked-strip"] [data-test="station-delivery-name"]').text(),
      delivery: review.get('[data-test="docked-strip"] [data-test="station-delivery-mode"]').text(),
    }).toEqual({ station: 'Bar:', delivery: 'Gemeinsam (~14 Min.)' })
  })

  it('says above the buttons how each station hands its part out and when, in English', async () => {
    answerTheQuoteWith(14)
    prepareOrder()

    const review = mount(Review, { global: { plugins: testPlugins('en') } })
    await flushPromises()

    expect({
      station: review.get('[data-test="docked-strip"] [data-test="station-delivery-name"]').text(),
      delivery: review.get('[data-test="docked-strip"] [data-test="station-delivery-mode"]').text(),
    }).toEqual({ station: 'Bar:', delivery: 'Combined (~14 min)' })
  })

  it('shows no time for a station the laptop could not calculate', async () => {
    answerTheQuoteWith(null)
    prepareOrder()

    const review = mount(Review, { global: { plugins: testPlugins() } })
    await flushPromises()

    expect(review.get('[data-test="station-name"]').text()).toBe('Geht an Bar')
  })

  it('asks again with the new count when the order changes', async () => {
    answerTheQuoteWith(14)
    const order = prepareOrder()
    mount(Review, { global: { plugins: testPlugins() } })
    await flushPromises()

    order.addItem({ catalogItemId: WASSER.id, note: 'ohne Eis', stationId: 'station-bar', name: WASSER.name })
    await flushPromises()

    expect(quoteBodies.at(-1)).toEqual({
      lines: [{ catalogItemId: 'item-wasser', stationId: 'station-bar', units: 2 }],
    })
  })
})

describe('sending an order the guest pays on the spot', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
    navigate('/review')
    useSessionStore().deviceToken = 'token-here'
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string) => {
        if (url.startsWith('/api/open-items/table?')) {
          return new Response(
            JSON.stringify({
              tableName: 'Tisch 3',
              openAmountCents: 200,
              orders: [
                {
                  orderId: 'order-1',
                  globalOrderNumber: 1,
                  createdAtUtc: '2026-09-05T18:00:00Z',
                  staffMemberName: 'Anna',
                  items: [
                    {
                      orderItemId: 'new-wasser',
                      orderId: 'order-1',
                      globalOrderNumber: 1,
                      itemName: 'Wasser',
                      note: null,
                      unitPriceCents: 200,
                      orderedAtUtc: '2026-09-05T18:00:00Z',
                      fulfilledAtUtc: null,
                      settledAtUtc: null,
                    },
                  ],
                },
              ],
            }),
            { status: 200 },
          )
        }
        if (url !== '/api/orders') {
          return new Response(JSON.stringify({ stations: [] }), { status: 200 })
        }
        return new Response(
          JSON.stringify({
            orderId: 'order-1',
            globalOrderNumber: 1,
            status: 'open',
            totalCents: 200,
            createdAtUtc: '2026-09-05T18:00:00Z',
            stationOrders: [
              {
                stationOrderId: 'station-order-1',
                stationId: 'station-bar',
                stationName: 'Bar',
                stationOrderNumber: 1,
                deliveryMode: 'together',
                itemIds: ['new-wasser'],
              },
            ],
          }),
          { status: 200 },
        )
      }),
    )
  })

  it('opens the open items of the table with the items just sent ticked', async () => {
    prepareOrder()
    const review = mount(Review, { global: { plugins: testPlugins() }, attachTo: document.body })

    await sendAndSettle(review)
    await vi.waitFor(() => expect(currentRoute.value).toEqual({ name: 'openItems' }))
    const openItems = useOpenItemsStore()
    await openItems.loadTableReport('Tisch 3')

    expect({ table: openItems.lookupName, ticked: openItems.selectedItemIds }).toEqual({
      table: 'Tisch 3',
      ticked: ['new-wasser'],
    })
  })
})
