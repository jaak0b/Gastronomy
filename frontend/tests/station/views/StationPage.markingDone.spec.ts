import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { answer, refusal } from '../../support/laptop'
import { TOGETHER_STATION_ORDER, AS_IT_COMES_STATION_ORDER, NOTED_STATION_ORDER, queue, stationLaptop, mountPage, cardInTheQueue, itemOnCard, textsOf } from './stationPageFixture'

describe('marking selected items as done from a card', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('keeps the done control of a card off until one of its lines is selected', async () => {
    stationLaptop()
    const page = await mountPage()
    const done = cardInTheQueue(page, 'station-order-1').get('[data-test="fulfill"]')

    expect(done.attributes('disabled')).toBeDefined()

    await itemOnCard(cardInTheQueue(page, 'station-order-1'), 'a').trigger('click')

    expect(cardInTheQueue(page, 'station-order-1').get('[data-test="fulfill"]').attributes('disabled')).toBeUndefined()
  })

  it('selects and deselects a line on tap', async () => {
    stationLaptop()
    const page = await mountPage()
    const line = itemOnCard(cardInTheQueue(page, 'station-order-1'), 'a')

    await line.trigger('click')
    expect(line.classes()).toContain('selected')
    expect(line.find('[data-test="selected-tick"]').exists()).toBe(true)

    await line.trigger('click')
    expect(line.classes()).not.toContain('selected')
    expect(line.find('[data-test="selected-tick"]').exists()).toBe(false)
  })

  it('asks again on a full screen before anything is done', async () => {
    stationLaptop()
    const page = await mountPage()
    const card = cardInTheQueue(page, 'station-order-1')

    expect(card.get('[data-test="fulfill"]').text()).toBe('Zubereiten')
    await itemOnCard(card, 'a').trigger('click')
    await itemOnCard(card, 'b').trigger('click')
    await card.get('[data-test="fulfill"]').trigger('click')
    await flushPromises()

    expect(textsOf('[data-test="station-done-dialog"] [data-test="row-table"]')).toEqual(['Tisch 3'])
    expect(textsOf('[data-test="station-done-dialog"] [data-test="unit"]')).toEqual([
      '1 x Bratwurst · Hinweis: ohne Senf',
      '1 x Pommes',
    ])
    expect(document.querySelector('[data-test="station-done-dialog"] [data-test="confirm"]')?.textContent?.trim()).toBe(
      'Zubereitet',
    )
    expect(document.querySelector('[data-test="station-done-dialog"] [data-test="cancel"]')?.textContent?.trim()).toBe(
      'Abbrechen',
    )
  })

  it('asks again with one line per note, so two different frankfurters are not merged', async () => {
    stationLaptop({ orders: queue([NOTED_STATION_ORDER]) })
    const page = await mountPage()
    const card = cardInTheQueue(page, 'station-order-3')

    await itemOnCard(card, 'f').trigger('click')
    await itemOnCard(card, 'g').trigger('click')
    await card.get('[data-test="fulfill"]').trigger('click')
    await flushPromises()

    expect(textsOf('[data-test="station-done-dialog"] [data-test="unit"]')).toEqual([
      '1 x Frankfurter · Hinweis: Mit Ketchup',
      '1 x Frankfurter · Hinweis: Ohne Ketchup',
    ])
  })

  it('keeps the selection when the employee backs out of the question', async () => {
    stationLaptop()
    const page = await mountPage()
    const card = cardInTheQueue(page, 'station-order-1')
    await itemOnCard(card, 'a').trigger('click')
    await card.get('[data-test="fulfill"]').trigger('click')
    await flushPromises()

    ;(document.querySelector('[data-test="station-done-dialog"] [data-test="cancel"]') as HTMLElement).click()
    await flushPromises()

    expect(document.querySelector('[data-test="station-done-dialog"]')).toBeNull()
    expect(itemOnCard(card, 'a').classes()).toContain('selected')
    expect(card.get('[data-test="fulfill"]').attributes('disabled')).toBeUndefined()
  })

  it('posts the selected items and updates the screen from the answer', async () => {
    const laptop = stationLaptop({
      fulfill: queue([AS_IT_COMES_STATION_ORDER], [AS_IT_COMES_STATION_ORDER]),
    })
    const page = await mountPage()
    const card = cardInTheQueue(page, 'station-order-1')

    await itemOnCard(card, 'a').trigger('click')
    await itemOnCard(card, 'b').trigger('click')
    await card.get('[data-test="fulfill"]').trigger('click')
    await flushPromises()
    ;(document.querySelector('[data-test="station-done-dialog"] [data-test="confirm"]') as HTMLElement).click()
    await flushPromises()

    expect(laptop.writes()).toMatchObject([
      { url: '/api/station/items/fulfill', body: { orderItemIds: ['a', 'b'] } },
    ])
    expect(page.findAll('[data-test="orders-column"] [data-test="station-order"]')).toHaveLength(1)
    expect(page.get('[data-test="orders-column"] [data-test="table-name"]').text()).toBe('Tisch 7')
  })

  it('states the reason when the laptop did not save the change', async () => {
    stationLaptop({
      fulfill: refusal('errors.station.changeNotSaved', { code: 'ItemNotFulfilled' }),
    })
    const page = await mountPage()
    const card = cardInTheQueue(page, 'station-order-1')

    await itemOnCard(card, 'a').trigger('click')
    await card.get('[data-test="fulfill"]').trigger('click')
    await flushPromises()
    ;(document.querySelector('[data-test="station-done-dialog"] [data-test="confirm"]') as HTMLElement).click()
    await flushPromises()

    expect(page.get('[data-test="action-failed"]').text()).toBe(
      'Die Bestellung konnte nicht aktualisiert werden. Laden Sie die Seite neu und versuchen Sie es erneut.',
    )
  })
})

describe('hiding an order from the second column', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('offers the hide control on the as-it-comes card only', async () => {
    stationLaptop()
    const page = await mountPage()

    expect(page.findAll('[data-test="hide"]')).toHaveLength(1)
    expect(page.get('[data-test="as-it-comes-column"] [data-test="hide"]').text()).toBe('Hier ausblenden')
  })

  it('takes the card out of the second column and leaves it in the first', async () => {
    const laptop = stationLaptop({
      hide: queue([
        TOGETHER_STATION_ORDER,
        { ...AS_IT_COMES_STATION_ORDER, isHiddenFromAsItComesQueue: true },
      ]),
    })
    const page = await mountPage()

    await page.get('[data-test="as-it-comes-column"] [data-test="hide"]').trigger('click')
    await flushPromises()

    expect(laptop.writes()).toMatchObject([{ url: '/api/station/orders/station-order-2/hide', body: undefined }])
    expect(page.findAll('[data-test="as-it-comes-column"] [data-test="station-order"]')).toHaveLength(0)
    expect(page.findAll('[data-test="orders-column"] [data-test="station-order"]')).toHaveLength(2)
  })

  it('states the reason when the order belongs to another station', async () => {
    stationLaptop({
      hide: refusal('errors.station.orderNotAtThisStation', { code: 'UnprocessableEntity' }),
    })
    const page = await mountPage()

    await page.get('[data-test="as-it-comes-column"] [data-test="hide"]').trigger('click')
    await flushPromises()

    expect(page.get('[data-test="action-failed"]').text()).toBe(
      'Diese Bestellung gehört nicht zu dieser Ausgabestelle. Laden Sie die Seite neu und versuchen Sie es erneut.',
    )
  })
})
