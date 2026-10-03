import { beforeEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import Review from '../../../src/phone/views/Review.vue'
import { useCatalogStore } from '../../../src/phone/stores/catalog'
import { navigate } from '../../../src/shared/router/router'
import { testPlugins } from '../../support/plugins'
import { WASSER, prepareOrder } from './reviewFixture'
import { stubLaptop, answer } from '../../support/laptop'

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

describe('an order holding something that cannot be ordered', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
    navigate('/review')
    stubLaptop().answersEverythingElse(answer({}))
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

describe('an order holding a line the admin moved to another station', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
    navigate('/review')
    stubLaptop().answersEverythingElse(answer({}))
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
