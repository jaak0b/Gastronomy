import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { refusal } from '../../support/laptop'
import { NOTED_STATION_ORDER, queue, stationLaptop, mountPage, mountPageFollowingTheChosenLanguage, cardInTheQueue, itemOnCard, textsOf } from './stationPageFixture'

describe('the screen at a station', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
    stationLaptop()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('names the station in its heading, so nobody works the wrong pile', async () => {
    const page = await mountPage()

    expect(page.get('[data-test="station-name"]').text()).toBe('Küche')
  })

  it('writes the page in the language the header picker chooses', async () => {
    const page = await mountPageFollowingTheChosenLanguage()

    expect(page.get('[data-test="orders-heading"]').text()).toBe('Alle Bestellungen')

    await page.get('[data-test="language-switch"] .v-field').trigger('mousedown')
    await vi.waitFor(() => expect(document.querySelector('[data-test="option-en"]')).not.toBeNull())
    ;(document.querySelector('[data-test="option-en"]') as HTMLElement).click()
    await flushPromises()

    expect(page.get('[data-test="orders-heading"]').text()).toBe('All orders')
  })

  it('puts every order in the left column and only the as-it-comes ones in the right', async () => {
    const page = await mountPage()

    expect(page.findAll('[data-test="orders-column"] [data-test="station-order"]')).toHaveLength(2)
    expect(page.findAll('[data-test="as-it-comes-column"] [data-test="station-order"]')).toHaveLength(1)
    expect(page.get('[data-test="as-it-comes-column"] [data-test="table-name"]').text()).toBe('Tisch 7')
  })

  it('shows the order number and the number of this station on a card', async () => {
    const page = await mountPage()

    expect(cardInTheQueue(page, 'station-order-1').get('[data-test="station-order-heading"]').text()).toBe(
      'Bestellung 137 · Nr. 12 ·',
    )
  })

  it('shows when each order was taken and who took it', async () => {
    const page = await mountPage()

    expect(page.findAll('[data-test="orders-column"] [data-test="taken-by"]').map((entry) => entry.text())).toEqual([
      '05.09.2026, 20:00 · Anna',
      '05.09.2026, 20:05 · Ben',
    ])
  })

  it('shows the table prominently, because that is what goes on the tray', async () => {
    const page = await mountPage()

    expect(cardInTheQueue(page, 'station-order-1').get('[data-test="table-name"]').text()).toBe('Tisch 3')
  })

  it('names the delivery mode in words and marks the card by mode', async () => {
    const page = await mountPage()

    expect(page.findAll('[data-test="orders-column"] [data-test="delivery-mode"]').map((entry) => entry.text())).toEqual([
      'Gemeinsam',
      'Einzeln',
    ])
    expect(cardInTheQueue(page, 'station-order-1').attributes('style')).toContain(
      'var(--v-theme-together)',
    )
    expect(cardInTheQueue(page, 'station-order-2').attributes('style')).toContain(
      'var(--v-theme-individual)',
    )
  })

  it('counts what is done on the card', async () => {
    const page = await mountPage()

    expect(cardInTheQueue(page, 'station-order-1').get('[data-test="done-counter"]').text()).toBe('1 / 3 zubereitet')
    expect(cardInTheQueue(page, 'station-order-2').get('[data-test="done-counter"]').text()).toBe('0 / 2 zubereitet')
  })

  it('keeps a done item off the card and shows every open one with its note', async () => {
    const page = await mountPage()

    const card = cardInTheQueue(page, 'station-order-1')

    expect(card.findAll('[data-test="station-item"]')).toHaveLength(2)
    expect(itemOnCard(card, 'a').get('[data-test="item-name"]').text()).toBe('Bratwurst')
    expect(itemOnCard(card, 'a').get('[data-test="item-note"]').text()).toBe('Hinweis: ohne Senf')
    expect(itemOnCard(card, 'a').find('[data-test="item-note-icon"]').exists()).toBe(true)
    expect(itemOnCard(card, 'b').get('[data-test="item-name"]').text()).toBe('Pommes')
    expect(itemOnCard(card, 'b').find('[data-test="item-note"]').exists()).toBe(false)
  })
})

describe('switching a card between the list and the grouped view', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('groups the open items by name and note and brings the list back', async () => {
    stationLaptop({ orders: queue([NOTED_STATION_ORDER]) })
    const page = await mountPage()
    const card = cardInTheQueue(page, 'station-order-3')

    expect(card.get('[data-test="grouped-toggle"]').text()).toBe('Gruppiert')

    await card.get('[data-test="grouped-toggle"]').trigger('click')

    expect(card.get('[data-test="grouped-toggle"]').text()).toBe('Liste')
    expect(card.findAll('[data-test="grouped-line"]').map((line) => line.text())).toEqual([
      '1 x Frankfurter · Hinweis: Mit Ketchup',
      '1 x Frankfurter · Hinweis: Ohne Ketchup',
    ])
    expect(card.findAll('[data-test="station-item"]')).toHaveLength(0)
    expect(card.find('[data-test="card-actions"]').exists()).toBe(false)

    await card.get('[data-test="grouped-toggle"]').trigger('click')

    expect(card.get('[data-test="grouped-toggle"]').text()).toBe('Gruppiert')
    expect(card.findAll('[data-test="station-item"]')).toHaveLength(2)
    expect(card.find('[data-test="card-actions"]').exists()).toBe(true)
  })
})

describe('the overview board on the station screen', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
    stationLaptop()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('opens from the header with every open article and closes again', async () => {
    const page = await mountPage()

    await page.get('[data-test="show-overview"]').trigger('click')
    await flushPromises()

    expect(textsOf('[data-test="station-open-board"] [data-test="stat-together"]')).toEqual(['Gemeinsam 1'])
    expect(textsOf('[data-test="station-open-board"] [data-test="stat-as-it-comes"]')).toEqual(['Einzeln 1'])
    expect(textsOf('[data-test="station-open-board"] [data-test="unit"]')).toEqual([
      '1 x Bier',
      '1 x Bratwurst',
      '1 x Bratwurst · Hinweis: ohne Senf',
      '1 x Pommes',
    ])

    ;(document.querySelector('[data-test="station-open-board"] [data-test="back-to-orders"]') as HTMLElement).click()
    await flushPromises()

    expect(document.querySelector('[data-test="station-open-board"]')).toBeNull()
    expect(page.findAll('[data-test="orders-column"] [data-test="station-order"]')).toHaveLength(2)
  })

  it('shows two rows when two of the same article carry different notes', async () => {
    stationLaptop({ orders: queue([NOTED_STATION_ORDER]) })
    const page = await mountPage()

    await page.get('[data-test="show-overview"]').trigger('click')
    await flushPromises()

    expect(textsOf('[data-test="station-open-board"] [data-test="unit"]')).toEqual([
      '1 x Frankfurter · Hinweis: Mit Ketchup',
      '1 x Frankfurter · Hinweis: Ohne Ketchup',
    ])
  })

  it('shows the load warning inside the board when the queue never loaded', async () => {
    stationLaptop({
      orders: () => {
        throw new TypeError('Failed to fetch')
      },
    })
    const page = await mountPage()

    await page.get('[data-test="show-overview"]').trigger('click')
    await flushPromises()

    expect(textsOf('[data-test="station-open-board"] [data-test="board-failed"]')).toEqual([
      'Laden Sie die Seite neu. Der Rechner war nicht erreichbar, deshalb kann diese Liste veraltet sein.',
    ])
  })

  it('shows a refused action inside the board', async () => {
    stationLaptop({
      hide: refusal('errors.station.orderNotAtThisStation', { code: 'UnprocessableEntity' }),
    })
    const page = await mountPage()

    await page.get('[data-test="as-it-comes-column"] [data-test="hide"]').trigger('click')
    await flushPromises()
    await page.get('[data-test="show-overview"]').trigger('click')
    await flushPromises()

    expect(textsOf('[data-test="station-open-board"] [data-test="board-failed"]')).toEqual([
      'Diese Bestellung gehört nicht zu dieser Ausgabestelle. Laden Sie die Seite neu und versuchen Sie es erneut.',
    ])
  })
})
