import { beforeEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import Review from '../../../src/phone/views/Review.vue'
import { useOpenItemsStore } from '../../../src/phone/stores/openItems'
import { useSessionStore } from '../../../src/shared/stores/session'
import { currentRoute, navigate } from '../../../src/shared/router/router'
import { testPlugins } from '../../support/plugins'
import { prepareOrder, sendAndSettleLater, sendAndSettle } from './reviewFixture'
import { stubLaptop, answer, noConnection } from '../../support/laptop'
import { nextTick } from 'vue'

describe('sending the order from the review screen', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
    navigate('/review')
    stubLaptop().answersEverythingElse(
      answer({
        orderId: 'order-1',
        globalOrderNumber: 1,
        status: 'open',
        totalCents: 200,
        createdAtUtc: '2026-09-05T18:00:00Z',
        stationOrders: [],
      }),
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
    stubLaptop().answersEverythingElse(noConnection())
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
    stubLaptop().answersEverythingElse(noConnection())
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
    await nextTick()
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

describe('sending an order the guest pays on the spot', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
    navigate('/review')
    useSessionStore().deviceToken = 'token-here'
    stubLaptop()
      .answersEverythingElse(answer({ stations: [] }))
      .answers(
        'GET',
        '/api/open-items/table',
        answer({
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
      )
      .answers(
        'ANY',
        (call) => call.url === '/api/orders',
        answer({
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
