import { beforeEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import Review from '../../../src/phone/views/Review.vue'
import { useCatalogStore } from '../../../src/phone/stores/catalog'
import { useOrderStore } from '../../../src/phone/stores/order'
import { currentRoute, navigate } from '../../../src/shared/router/router'
import { testPlugins } from '../../support/plugins'
import { WASSER, prepareOrder, sendAndSettleLater } from './reviewFixture'
import { stubLaptop, neverAnswers, noConnection } from '../../support/laptop'
import { nextTick } from 'vue'

describe('an order the laptop did not confirm', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
    navigate('/review')
    stubLaptop().answersEverythingElse(noConnection())
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
    await nextTick()

    expect(review.find('[data-test="drop-lines-that-cannot-be-ordered"]').exists()).toBe(false)
    expect(review.find('[data-test="send-again"]').exists()).toBe(true)
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
    await nextTick()

    expect(review.get('[data-test="send-again"]').attributes('disabled')).toBeUndefined()
  })

  it('sends the order again when the retry is tapped, sold-out line and all', async () => {
    const order = prepareOrder()
    const review = await reviewAfterAFailedSend(order)
    sellOutTheWater()
    await nextTick()

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
    expect(review.find('[data-test="written-down"]').exists()).toBe(true)
    expect(review.get('[data-test="line-name"]').text()).toBe('1 x Wasser')
  })
})

describe('an order that is still on its way to the laptop', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
    navigate('/review')
    stubLaptop().answersEverythingElse(neverAnswers())
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
