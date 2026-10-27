import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { noConnection, refusal } from '../../support/laptop'
import { TOGETHER_STATION_ORDER, queue, stationLaptop, mountPage, cardInTheQueue, itemOnCard } from './stationPageFixture'

const WITH_CANCELLED_POMMES = {
  ...TOGETHER_STATION_ORDER,
  itemCount: 2,
  items: [
    TOGETHER_STATION_ORDER.items[0],
    { orderItemId: 'b', itemName: 'Pommes', note: null, state: 'cancelled', stateSetAtUtc: '2026-09-05T16:42:00Z' },
    TOGETHER_STATION_ORDER.items[2],
  ],
}

const AFTER_THE_CONFIRMATION = {
  ...TOGETHER_STATION_ORDER,
  itemCount: 2,
  items: [TOGETHER_STATION_ORDER.items[0], TOGETHER_STATION_ORDER.items[2]],
}

describe('a card with an item a waiter cancelled while it was open', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('shows the item struck through with the time it was cancelled', async () => {
    stationLaptop({ orders: queue([WITH_CANCELLED_POMMES]) })
    const page = await mountPage()

    const pommes = itemOnCard(cardInTheQueue(page, 'station-order-1'), 'b')

    expect(pommes.get('[data-test="item-name"]').classes()).toContain('cancelled-item')
    expect(pommes.get('[data-test="cancelled-at"]').text()).toBe('Storniert um 18:42')
  })

  it('sends the confirmation for the selected item and takes it off the card', async () => {
    const laptop = stationLaptop({
      orders: queue([WITH_CANCELLED_POMMES]),
      acknowledgeCancellation: queue([AFTER_THE_CONFIRMATION]),
    })
    const page = await mountPage()
    await itemOnCard(cardInTheQueue(page, 'station-order-1'), 'b').trigger('click')

    await cardInTheQueue(page, 'station-order-1').get('[data-test="acknowledge"]').trigger('click')
    await flushPromises()

    expect(laptop.callsTo('POST', '/api/station/items/acknowledge-cancellation').map((call) => call.body)).toEqual([
      { orderItemIds: ['b'] },
    ])
    expect(cardInTheQueue(page, 'station-order-1').find('[data-test="station-item"][data-test-id="b"]').exists()).toBe(false)
  })

  it('keeps the item struck through and asks for another tap when the laptop was not reached', async () => {
    stationLaptop({ orders: queue([WITH_CANCELLED_POMMES]), acknowledgeCancellation: noConnection() })
    const page = await mountPage()
    await itemOnCard(cardInTheQueue(page, 'station-order-1'), 'b').trigger('click')

    await cardInTheQueue(page, 'station-order-1').get('[data-test="acknowledge"]').trigger('click')
    await flushPromises()

    expect(page.get('[data-test="action-failed"]').text()).toBe(
      'Tippen Sie noch einmal. Der Rechner war nicht erreichbar, deshalb wurde nichts geändert.',
    )
    expect(itemOnCard(cardInTheQueue(page, 'station-order-1'), 'b').classes()).toContain('selected')
  })

  it('shows the refusal when the item no longer waits for a confirmation', async () => {
    stationLaptop({
      orders: queue([WITH_CANCELLED_POMMES]),
      acknowledgeCancellation: refusal('errors.station.changeNotSaved', { code: 'ItemNotAwaitingCancellationAcknowledgement' }),
    })
    const page = await mountPage()
    await itemOnCard(cardInTheQueue(page, 'station-order-1'), 'b').trigger('click')

    await cardInTheQueue(page, 'station-order-1').get('[data-test="acknowledge"]').trigger('click')
    await flushPromises()

    expect(page.get('[data-test="action-failed"]').text()).toBe(
      'Die Bestellung konnte nicht aktualisiert werden. Laden Sie die Seite neu und versuchen Sie es erneut.',
    )
  })
})
