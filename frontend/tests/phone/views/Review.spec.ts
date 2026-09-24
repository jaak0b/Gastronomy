import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import Review from '../../../src/phone/views/Review.vue'
import { useCatalogStore } from '../../../src/phone/stores/catalog'
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

async function openTheSendSheet(review: VueWrapper): Promise<void> {
  await review.get('.continue').trigger('click')
  await vi.waitFor(() =>
    expect(document.querySelector('.confirm-send-dialog .confirm')).not.toBeNull(),
  )
}

async function chooseToSettleNow(): Promise<void> {
  await vi.waitFor(() =>
    expect(document.querySelector('.confirm-send-dialog .settle-now')).not.toBeNull(),
  )
  ;(document.querySelector('.confirm-send-dialog .settle-now') as HTMLElement).click()
  await flushPromises()
}

async function confirmTheSend(review: VueWrapper): Promise<void> {
  await vi.waitFor(() =>
    expect(document.querySelector('.confirm-send-dialog .confirm')).not.toBeNull(),
  )
  ;(document.querySelector('.confirm-send-dialog .confirm') as HTMLElement).click()
  await review.vm.$nextTick()
}

async function sendFromTheStrip(review: VueWrapper, settleNow = false): Promise<void> {
  await openTheSendSheet(review)
  if (settleNow) {
    await chooseToSettleNow()
  }
  await confirmTheSend(review)
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

  it('takes the server back to the items, so the sent order cannot be typed into any more', async () => {
    const order = prepareOrder()
    const review = mount(Review, { global: { plugins: testPlugins() }, attachTo: document.body })

    await sendFromTheStrip(review)
    await vi.waitFor(() => expect(order.sendState).toBe('accepted'))

    expect(currentRoute.value).toEqual({ name: 'home' })
  })

  it('lets the next order be sent while the arrival notice of the last one is still up', async () => {
    const order = prepareOrder()
    const review = mount(Review, { global: { plugins: testPlugins() }, attachTo: document.body })
    await sendFromTheStrip(review)
    await vi.waitFor(() => expect(order.sendState).toBe('accepted'))

    prepareOrder()
    const nextReview = mount(Review, {
      global: { plugins: testPlugins() },
      attachTo: document.body,
    })

    expect(nextReview.get('.continue').attributes('disabled')).toBeUndefined()
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

    await sendFromTheStrip(review)
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

    await sendFromTheStrip(review)
    await vi.waitFor(() => expect(order.sendState).toBe('failed'))

    expect(review.find('.continue').exists()).toBe(false)
    expect(review.find('.send-failure').exists()).toBe(true)
  })

  it('keeps the table and the total together in the compact heading', () => {
    prepareOrder()
    const review = mount(Review, { global: { plugins: testPlugins() }, attachTo: document.body })

    expect(review.get('h1.table-name').text()).toBe('Tisch: Tisch 3')
    expect(review.get('.order-total').text()).toContain('2.00')
  })

  it('takes the waiter back to the items from the back button at the bottom of the strip', async () => {
    prepareOrder()
    const review = mount(Review, { global: { plugins: testPlugins() }, attachTo: document.body })

    expect(review.get('.back').text()).toBe('Zurück')
    expect(review.get('.back').element.closest('.docked-strip')).not.toBeNull()

    await review.get('.back').trigger('click')

    expect(currentRoute.value).toEqual({ name: 'home' })
  })

  it('leaves the items open when the table wants to pay later', async () => {
    const order = prepareOrder()
    const review = mount(Review, { global: { plugins: testPlugins() }, attachTo: document.body })

    await sendFromTheStrip(review)
    await vi.waitFor(() => expect(order.sendState).toBe('accepted'))

    const sent = JSON.parse((vi.mocked(fetch).mock.calls[0][1] as RequestInit).body as string)
    expect(sent.items[0].settlement).toBeNull()
  })

  it('settles every item at once when the guest pays on the spot', async () => {
    const order = prepareOrder()
    const review = mount(Review, { global: { plugins: testPlugins() }, attachTo: document.body })

    await sendFromTheStrip(review, true)
    await vi.waitFor(() => expect(order.sendState).toBe('accepted'))

    const sent = JSON.parse((vi.mocked(fetch).mock.calls[0][1] as RequestInit).body as string)
    expect(sent.items[0].settlement).toEqual({ paidPriceCents: 200, paymentNotice: null })
  })

  it('retries with the same settlement the server confirmed, so a retry cannot change who paid', async () => {
    const order = prepareOrder()
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => {
        throw new TypeError('the laptop cannot be reached')
      }),
    )
    const review = mount(Review, { global: { plugins: testPlugins() }, attachTo: document.body })
    await sendFromTheStrip(review, true)
    await vi.waitFor(() => expect(order.sendState).toBe('failed'))

    await review.get('.send-again').trigger('click')
    await vi.waitFor(() => expect(vi.mocked(fetch).mock.calls).toHaveLength(2))

    const retried = JSON.parse((vi.mocked(fetch).mock.calls[1][1] as RequestInit).body as string)
    expect(retried.items[0].settlement).toEqual({ paidPriceCents: 200, paymentNotice: null })
  })

  it('keeps the total and the send button within reach while the lines scroll', () => {
    prepareOrder()
    const review = mount(Review, { global: { plugins: testPlugins() }, attachTo: document.body })

    const footer = review.get('.review-footer')

    expect(footer.classes()).toContain('docked-strip')
    expect(footer.find('.continue').exists()).toBe(true)
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

    expect(review.find('.continue').exists()).toBe(false)
    expect(review.get('.drop-lines-that-cannot-be-ordered').exists()).toBe(true)
  })

  it('offers only the way to take the vanished item off, not a way to send', () => {
    orderWithAVanishedItem()
    const review = mountReview()

    expect(review.find('.continue').exists()).toBe(false)
    expect(review.get('.drop-lines-that-cannot-be-ordered').exists()).toBe(true)
  })

  it('leaves the way to send open while the whole order can be ordered', () => {
    prepareOrder()
    const review = mountReview()

    expect(review.get('.continue').attributes('disabled')).toBeUndefined()
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

    expect(review.get('.continue').attributes('disabled')).toBeUndefined()
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
    await sendFromTheStrip(review)
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

    const failure = review.get('.send-failure').element
    const heading = review.get('.review-heading').element

    expect(failure.compareDocumentPosition(heading) & Node.DOCUMENT_POSITION_FOLLOWING).toBe(
      Node.DOCUMENT_POSITION_FOLLOWING,
    )
  })

  it('offers no way to take the sold-out line off, because the laptop may hold the order as it was', async () => {
    const order = prepareOrder()
    const review = await reviewAfterAFailedSend(order)

    sellOutTheWater()
    await review.vm.$nextTick()

    expect(review.find('.drop-lines-that-cannot-be-ordered').exists()).toBe(false)
    expect(review.get('.send-again').exists()).toBe(true)
  })

  it('hides the way back to the items, because everything there would change the order', async () => {
    const order = prepareOrder()
    const review = await reviewAfterAFailedSend(order)

    expect(review.find('.back').exists()).toBe(false)
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
    expect(review.get('.order-total').text()).toContain('2.00')
  })

  it('shows no waiting time when the laptop never sent one, instead of a short made-up one', async () => {
    const order = prepareOrder()
    const review = await reviewAfterAFailedSend(order)

    expect(review.get('.station-name').text()).toBe('Geht an Bar')
  })
  it('offers the retry in the docked strip, where the send action stands', async () => {
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

  it('keeps the paper route away until the second attempt got no answer as well', async () => {
    const order = prepareOrder()
    const review = await reviewAfterAFailedSend(order)

    expect(review.find('.written-down').exists()).toBe(false)
    expect(review.find('.back').exists()).toBe(false)
  })

  it('says the order may have arrived once the second attempt got no answer either', async () => {
    const order = prepareOrder()
    const review = await reviewAfterAFailedSend(order)

    await review.get('.send-again').trigger('click')
    await vi.waitFor(() =>
      expect(review.get('.write-it-down').text()).toContain(
        'Die Bestellung konnte noch nicht bestätigt werden.',
      ),
    )
  })

  it('starts the next order once the waiter has written this one down', async () => {
    const order = prepareOrder()
    const review = await reviewAfterAFailedSend(order)
    await review.get('.send-again').trigger('click')
    await vi.waitFor(() => expect(review.find('.written-down').exists()).toBe(true))

    await review.get('.written-down').trigger('click')

    expect(order.basketLines).toEqual([])
    expect(currentRoute.value).toEqual({ name: 'home' })
  })

  it('sends again when the waiter says the WiFi is back', async () => {
    const order = prepareOrder()
    const review = await reviewAfterAFailedSend(order)
    await review.get('.send-again').trigger('click')
    await vi.waitFor(() => expect(vi.mocked(fetch).mock.calls).toHaveLength(2))

    await review.get('.send-again').trigger('click')

    await vi.waitFor(() => expect(vi.mocked(fetch).mock.calls).toHaveLength(3))
  })

  it('comes back to the same screen, lines and paper route, when that attempt fails too', async () => {
    const order = prepareOrder()
    const review = await reviewAfterAFailedSend(order)
    await review.get('.send-again').trigger('click')
    await vi.waitFor(() => expect(vi.mocked(fetch).mock.calls).toHaveLength(2))

    await review.get('.send-again').trigger('click')
    await vi.waitFor(() => expect(vi.mocked(fetch).mock.calls).toHaveLength(3))

    await vi.waitFor(() =>
      expect(review.get('.write-it-down').text()).toContain(
        'Schreiben Sie die Bestellung auf einen Zettel',
      ),
    )
    expect(review.get('.written-down').exists()).toBe(true)
    expect(review.get('.line-name').text()).toBe('1 x Wasser')
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
    await sendFromTheStrip(review)
    return review
  }

  it('hides the way back to the items, because everything there would change the order', async () => {
    const review = await reviewOfAnOrderOnItsWay()

    expect(review.find('.back').exists()).toBe(false)
  })

  it('refuses to have the delivery choice changed', async () => {
    const review = await reviewOfAnOrderOnItsWay()

    expect(review.get('.delivery-together').attributes('disabled')).toBeDefined()
  })

  it('leaves one way to send on the screen and says that the order is going out', async () => {
    const review = await reviewOfAnOrderOnItsWay()

    expect(review.get('.send-again').text()).toBe('Wird gesendet')
    expect(review.find('.continue').exists()).toBe(false)
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
              messageKey: 'order.unknownItem',
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
    await order.send(null)
    return mount(Review, { global: { plugins: testPlugins() }, attachTo: document.body })
  }

  it('says what the laptop refused, in words the waiter can act on', async () => {
    const review = await reviewAfterARefusal(prepareOrder())

    expect(review.get('.send-failure .failure-message').text()).toBe(
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
              messageKey: 'catalog.itemSoldOut',
              parameters: { name: 'Wasser', catalogItemId: 'item-wasser' },
              details: null,
            }),
            { status: 422 },
          ),
      ),
    )

    const review = await reviewAfterARefusal(prepareOrder())

    expect(review.get('.send-failure .failure-message').text()).toBe(
      'Wasser ist gerade ausverkauft.',
    )
  })

  it('opens the way back to the items again, because no order was created', async () => {
    const review = await reviewAfterARefusal(prepareOrder())

    expect(review.get('.back').attributes('disabled')).toBeUndefined()
  })

  it('lets the delivery choice be changed again', async () => {
    const review = await reviewAfterARefusal(prepareOrder())

    expect(review.get('.delivery-together').attributes('disabled')).toBeUndefined()
  })

  it('lets the waiter take off the item the laptop named', async () => {
    const review = await reviewAfterARefusal(orderWithAVanishedItem())

    expect(
      review.get('.drop-lines-that-cannot-be-ordered').attributes('disabled'),
    ).toBeUndefined()
  })

  it('leaves the way to send in the strip, because this is not a retry into the dark', async () => {
    const review = await reviewAfterARefusal(prepareOrder())

    expect(review.get('.review-footer .continue').exists()).toBe(true)
    expect(review.find('.send-again').exists()).toBe(false)
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
    await order.send(null)
    return mount(Review, { global: { plugins: testPlugins() }, attachTo: document.body })
  }

  it('says the laptop could not save it, without naming a button that is not there', async () => {
    const review = await reviewAfterTheAnswer()

    expect(review.get('.send-failure .failure-message').text()).toBe(
      'Der Rechner konnte die Bestellung nicht speichern. Senden Sie sie noch einmal.',
    )
  })

  it('leaves the way to send in the strip, because the answer said no order was created', async () => {
    const review = await reviewAfterTheAnswer()

    expect(review.get('.review-footer .continue').exists()).toBe(true)
    expect(review.find('.send-again').exists()).toBe(false)
  })

  it('opens the way back to the items again', async () => {
    const review = await reviewAfterTheAnswer()

    expect(review.get('.back').attributes('disabled')).toBeUndefined()
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

    expect(review.find('.continue').exists()).toBe(false)
    expect(review.get('.drop-lines-that-cannot-be-ordered').exists()).toBe(true)
  })

  it('marks the line as sold out on the card of the station that no longer prepares it', () => {
    orderWhoseStationWasTakenOff()
    const review = mountReview()

    expect(review.get('.sold-out').text()).toBe('Wasser ist gerade ausverkauft.')
  })

  it('lets the one button that clears such lines take it off', async () => {
    const order = orderWhoseStationWasTakenOff()
    const review = mountReview()

    await review.get('.drop-lines-that-cannot-be-ordered').trigger('click')

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
      failure: { key: 'review.sendFailed' },
      unresolvedAttempt: {
        clientOrderId: 'c0ffee00-1111-4111-8111-111111111111',
        tableName: 'Tisch 3',
        items: [
          {
            catalogItemId: WASSER.id,
            unitPriceCents: WASSER.priceCents,
            note: null,
            stationId: 'station-bar',
            settlement: null,
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
    await review.get('.send-again').trigger('click')
    await vi.waitFor(() => expect(order.sendState).toBe('rejected'))
    return review
  }

  it('offers the paper route at once, because nothing on the phone can put the refusal right', async () => {
    const order = anOrderTheLaptopMayAlreadyHold()

    const review = await reviewAfterTheRefusedRetry(order)

    expect(review.find('.written-down').exists()).toBe(true)
  })

  it('keeps the order closed for changes, because the laptop may hold it as it stands', async () => {
    const order = anOrderTheLaptopMayAlreadyHold()

    const review = await reviewAfterTheRefusedRetry(order)

    expect(order.changesAreRefused).toBe(true)
    expect(review.find('.back').exists()).toBe(false)
  })

  it('keeps every line on the screen, so the waiter can copy the order onto paper', async () => {
    const order = anOrderTheLaptopMayAlreadyHold()

    const review = await reviewAfterTheRefusedRetry(order)

    expect(order.draft.lines).toHaveLength(1)
    expect(review.get('.line-name').text()).toBe('1 x Wasser')
  })

  function aLaptopThatRefusesWithAReason(): void {
    vi.stubGlobal(
      'fetch',
      vi.fn(
        async () =>
          new Response(
            JSON.stringify({
              code: 'UnprocessableEntity',
              messageKey: 'catalog.itemSoldOut',
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
    expect(review.find('.written-down').exists()).toBe(false)
    expect(review.find('.back').exists()).toBe(true)
  })
})

describe('the question the waiter answers before an order goes out', () => {
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

  function mountReview() {
    return mount(Review, { global: { plugins: testPlugins() }, attachTo: document.body })
  }

  it('sends nothing on the tap itself, so a pocket cannot place an order', async () => {
    const order = prepareOrder()
    const review = mountReview()

    await review.get('.continue').trigger('click')

    expect(vi.mocked(fetch).mock.calls).toHaveLength(0)
    expect(order.sendState).toBe('idle')
    expect(document.querySelector('.confirm-send-dialog')).not.toBeNull()
  })

  it('settles the table with the amount the waiter confirmed', async () => {
    const order = prepareOrder()
    const review = mountReview()

    await sendFromTheStrip(review, true)
    await vi.waitFor(() => expect(order.sendState).toBe('accepted'))

    const sent = JSON.parse((vi.mocked(fetch).mock.calls[0][1] as RequestInit).body as string)
    expect(sent.items[0].settlement).toEqual({ paidPriceCents: 200, paymentNotice: null })
  })

  it('leaves the table open only once the waiter has confirmed it', async () => {
    const order = prepareOrder()
    const review = mountReview()

    await sendFromTheStrip(review)
    await vi.waitFor(() => expect(order.sendState).toBe('accepted'))

    const sent = JSON.parse((vi.mocked(fetch).mock.calls[0][1] as RequestInit).body as string)
    expect(sent.items[0].settlement).toBeNull()
  })

  it('names the table, the amount and what the station will do in the question', async () => {
    prepareOrder()
    const review = mountReview()

    await review.get('.continue').trigger('click')
    await vi.waitFor(() =>
      expect(document.querySelector('.confirm-send-dialog .row-table')).not.toBeNull(),
    )

    const rowText = (selector: string) =>
      document.querySelector(`.confirm-send-dialog ${selector}`)?.textContent?.trim() ?? ''
    expect(rowText('.row-table .value')).toBe('Tisch 3')
    expect(rowText('.row-amount .value')).toContain('2.00')
    expect(rowText('.row-station .label')).toBe('Bar:')
    expect(rowText('.row-station .value')).toBe('Gemeinsam')
  })

  it('hands the order back untouched when the waiter backs out', async () => {
    const order = prepareOrder()
    const review = mountReview()
    await review.get('.continue').trigger('click')
    await vi.waitFor(() => expect(document.querySelector('.confirm-send-dialog')).not.toBeNull())

    ;(document.querySelector('.confirm-send-dialog .cancel') as HTMLElement).click()
    await review.vm.$nextTick()

    expect(vi.mocked(fetch).mock.calls).toHaveLength(0)
    expect(order.sendState).toBe('idle')
    expect(order.basketLines).toHaveLength(1)
    expect(review.get('.continue').attributes('disabled')).toBeUndefined()
  })

  it('asks again after a cancelled send, with the choice made afresh', async () => {
    const order = prepareOrder()
    const review = mountReview()
    await review.get('.continue').trigger('click')
    await vi.waitFor(() => expect(document.querySelector('.confirm-send-dialog')).not.toBeNull())
    await chooseToSettleNow()
    ;(document.querySelector('.confirm-send-dialog .cancel') as HTMLElement).click()
    await review.vm.$nextTick()

    await sendFromTheStrip(review)
    await vi.waitFor(() => expect(order.sendState).toBe('accepted'))

    const sent = JSON.parse((vi.mocked(fetch).mock.calls[0][1] as RequestInit).body as string)
    expect(sent.items[0].settlement).toBeNull()
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

    expect(review.get('.station-name').text()).toBe('Geht an Bar (~14 Min.)')
  })

  it('shows no time for a station the laptop could not calculate', async () => {
    answerTheQuoteWith(null)
    prepareOrder()

    const review = mount(Review, { global: { plugins: testPlugins() } })
    await flushPromises()

    expect(review.get('.station-name').text()).toBe('Geht an Bar')
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
