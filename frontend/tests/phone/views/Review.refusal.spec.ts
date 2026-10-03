import { beforeEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import Review from '../../../src/phone/views/Review.vue'
import { useCatalogStore } from '../../../src/phone/stores/catalog'
import { useOrderStore } from '../../../src/phone/stores/order'
import { navigate } from '../../../src/shared/router/router'
import { saveDraft, saveSendProgress } from '../../../src/phone/core/draftCart'
import { testPlugins } from '../../support/plugins'
import { WASSER, prepareOrder } from './reviewFixture'
import { stubLaptop, answer, refusal } from '../../support/laptop'

describe('an order the laptop refused with a reason', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
    navigate('/review')
    stubLaptop().answersEverythingElse(
      refusal('errors.order.unknownItem', {
        status: 400,
        code: 'UnknownItem',
      }),
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
    stubLaptop().answersEverythingElse(
      refusal('errors.order.itemSoldOut', {
        status: 422,
        code: 'UnprocessableEntity',
        parameters: { name: 'Wasser', catalogItemId: 'item-wasser' },
      }),
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

    expect(review.find('[data-test="docked-strip"] [data-test="send-and-settle-later"]').exists()).toBe(true)
    expect(review.find('[data-test="send-again"]').exists()).toBe(false)
  })

})

describe('an order the laptop answered but could not save', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
    navigate('/review')
    stubLaptop().answersEverythingElse(answer({}, 500))
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

    expect(review.find('[data-test="docked-strip"] [data-test="send-and-settle-later"]').exists()).toBe(true)
    expect(review.find('[data-test="send-again"]').exists()).toBe(false)
  })

  it('opens the way back to the items again', async () => {
    const review = await reviewAfterTheAnswer()

    expect(review.get('[data-test="back"]').attributes('disabled')).toBeUndefined()
  })

})

describe('an order the laptop refused after an attempt it never answered', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
    navigate('/review')
    stubLaptop().answersEverythingElse(answer({}, 503))
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
      festival: null,
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
    stubLaptop().answersEverythingElse(
      refusal('errors.order.itemSoldOut', {
        status: 422,
        code: 'UnprocessableEntity',
        parameters: { name: 'Wasser', catalogItemId: 'item-wasser' },
      }),
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
