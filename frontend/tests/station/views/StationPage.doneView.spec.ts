import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { answer, refusal } from '../../support/laptop'
import { DONE_STATION_ORDER, DONE_NOTED_STATION_ORDER, queue, stationLaptop, mountPage, itemOnCard } from './stationPageFixture'

describe('the done view', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('opens from the button and lists every order with at least one done item', async () => {
    stationLaptop({ fulfilled: answer({ stationOrders: [DONE_STATION_ORDER] }) })
    const page = await mountPage()

    await page.get('[data-test="show-done"]').trigger('click')
    await flushPromises()

    expect(page.get('[data-test="done-heading"]').text()).toBe('Zubereitete Bestellungen')
    expect(page.findAll('[data-test="station-fulfilled"]')).toHaveLength(1)
    expect(page.get('[data-test="back-to-orders"]').text()).toBe('Zurück zu den Bestellungen')
  })

  it('shows on the done card when the order was taken and who took it', async () => {
    stationLaptop({ fulfilled: answer({ stationOrders: [DONE_STATION_ORDER] }) })
    const page = await mountPage()
    await page.get('[data-test="show-done"]').trigger('click')
    await flushPromises()

    expect(page.get('[data-test="station-fulfilled"] [data-test="taken-by"]').text()).toBe('05.09.2026, 20:00 · Anna')
  })

  it('shows every item and ticks the done ones', async () => {
    stationLaptop({ fulfilled: answer({ stationOrders: [DONE_STATION_ORDER] }) })
    const page = await mountPage()
    await page.get('[data-test="show-done"]').trigger('click')
    await flushPromises()

    const card = page.get('[data-test="station-fulfilled"]')

    expect(card.findAll('[data-test="station-item"]')).toHaveLength(3)
    expect(itemOnCard(card, 'a').find('[data-test="item-tick"]').exists()).toBe(true)
    expect(itemOnCard(card, 'b').find('[data-test="item-tick"]').exists()).toBe(false)
    expect(itemOnCard(card, 'c').find('[data-test="item-tick"]').exists()).toBe(true)
  })

  it('summarises on one line what the order contained', async () => {
    stationLaptop({ fulfilled: answer({ stationOrders: [DONE_STATION_ORDER] }) })
    const page = await mountPage()
    await page.get('[data-test="show-done"]').trigger('click')
    await flushPromises()

    expect(page.get('[data-test="station-fulfilled"] [data-test="unit-summary"]').text()).toBe('2 x Bratwurst · 1 x Pommes')
  })

  it('keeps two different notes apart in the summary line', async () => {
    stationLaptop({ fulfilled: answer({ stationOrders: [DONE_NOTED_STATION_ORDER] }) })
    const page = await mountPage()
    await page.get('[data-test="show-done"]').trigger('click')
    await flushPromises()

    expect(page.get('[data-test="station-fulfilled"] [data-test="unit-summary"]').text()).toBe(
      '1 x Frankfurter · Hinweis: Mit Ketchup · 1 x Frankfurter · Hinweis: Ohne Ketchup',
    )
  })

  it('offers a put back control on a done item only and posts it', async () => {
    const laptop = stationLaptop({
      fulfilled: answer({ stationOrders: [DONE_STATION_ORDER] }),
      unfulfill: queue([{ ...DONE_STATION_ORDER, fulfilledItemCount: 1 }]),
    })
    const page = await mountPage()
    await page.get('[data-test="show-done"]').trigger('click')
    await flushPromises()

    expect(page.findAll('[data-test="station-fulfilled"] [data-test="put-back"]')).toHaveLength(2)
    expect(page.get('[data-test="station-fulfilled"] [data-test="put-back"]').text()).toBe('Zurücklegen')

    await page.get('[data-test="station-fulfilled"] [data-test="put-back"]').trigger('click')
    await flushPromises()

    expect(laptop.writes()).toMatchObject([{ url: '/api/station/items/unfulfill', body: { orderItemIds: ['a'] } }])
  })

  it('states the reason when the laptop did not put an item back', async () => {
    stationLaptop({
      fulfilled: answer({ stationOrders: [DONE_STATION_ORDER] }),
      unfulfill: refusal('errors.station.changeNotSaved', { code: 'ItemNotFulfilled' }),
    })
    const page = await mountPage()
    await page.get('[data-test="show-done"]').trigger('click')
    await flushPromises()

    await page.get('[data-test="station-fulfilled"] [data-test="put-back"]').trigger('click')
    await flushPromises()

    expect(page.get('[data-test="action-failed"]').text()).toBe(
      'Die Bestellung konnte nicht aktualisiert werden. Laden Sie die Seite neu und versuchen Sie es erneut.',
    )
  })

  it('goes back to the queue', async () => {
    stationLaptop({ fulfilled: answer({ stationOrders: [DONE_STATION_ORDER] }) })
    const page = await mountPage()
    await page.get('[data-test="show-done"]').trigger('click')
    await flushPromises()

    await page.get('[data-test="back-to-orders"]').trigger('click')
    await flushPromises()

    expect(page.findAll('[data-test="orders-column"] [data-test="station-order"]')).toHaveLength(2)
    expect(page.find('[data-test="station-fulfilled"]').exists()).toBe(false)
  })

  it('says so when nothing is done yet', async () => {
    stationLaptop({ fulfilled: answer({ stationOrders: [] }) })
    const page = await mountPage()

    await page.get('[data-test="show-done"]').trigger('click')
    await flushPromises()

    expect(page.get('[data-test="nothing-done"]').text()).toBe('Es ist noch keine Bestellung zubereitet.')
  })
})
