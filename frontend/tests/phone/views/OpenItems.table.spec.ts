import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { enableAutoUnmount, flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import OpenItems from '../../../src/phone/views/OpenItems.vue'
import { useOpenItemsStore } from '../../../src/phone/stores/openItems'
import { currentRoute, navigate } from '../../../src/shared/router/router'
import { TOKEN_STORAGE_KEY, useSessionStore } from '../../../src/shared/stores/session'
import { testPlugins } from '../../support/plugins'
import {
  TABLE_REPORT,
  TISCH_12,
  lineNamed,
  orderCard,
  mountScreen,
  mountScreenToCancelItems,
  openItemsLaptop,
} from './openItemsFixture'
import { answer, emptyAnswer, stubLaptop } from '../../support/laptop'

enableAutoUnmount(afterEach)

const TABLE_PATH = '/api/open-items/tables/table-12/search'

describe('the orders of the table the waiter chose to settle', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
    navigate('/open-items')
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('loads the table the waiter tapped on the plan, by its id', async () => {
    const laptop = openItemsLaptop(TABLE_REPORT)

    await mountScreen()

    expect(laptop.urls()).toEqual([TABLE_PATH])
  })

  it('is titled with the word of the button that opened it, in German and in English', async () => {
    openItemsLaptop(TABLE_REPORT)
    const german = await mountScreen()
    const germanTitle = german.get('[data-test="settle-title"]').text()
    german.unmount()

    const english = await mountScreen('en')

    expect([germanTitle, english.get('[data-test="settle-title"]').text()]).toEqual(['Kassieren', 'Take payment'])
  })

  it('puts a check under Bezahlt on a settled position and leaves its tick box cell empty', async () => {
    openItemsLaptop(TABLE_REPORT)

    const screen = await mountScreen()

    const settled = lineNamed(orderCard(screen, 138), 'Wasser')
    expect(settled.get('[data-test="paid-mark"]').classes()).toEqual(expect.arrayContaining(['mdi-check', 'text-success']))
    expect(settled.get('[data-test="tick-cell"]').find('[data-test="line-tick"]').exists()).toBe(false)
  })

  it('shows the shared text of an empty list when the laptop finds no order to settle', async () => {
    openItemsLaptop({ ...TABLE_REPORT, orders: [] })

    const screen = await mountScreen()

    expect(screen.get('[data-test="no-orders"]').text()).toBe('Keine Einträge')
  })

  it('says the laptop could not be reached', async () => {
    stubLaptop().answers('POST', TABLE_PATH, answer({}, 500))

    const screen = await mountScreen()

    expect(screen.get('[data-test="load-failed"]').text()).toBe(
      'Der Rechner ist nicht erreichbar. Laden Sie die Seite neu.',
    )
  })

  it('shows no text of an empty list when the laptop could not be reached', async () => {
    stubLaptop().answers('POST', TABLE_PATH, answer({}, 500))

    const screen = await mountScreen()

    expect(screen.find('[data-test="no-orders"]').exists()).toBe(false)
  })

  it('says in one sentence that the table could not be loaded when the laptop does not know it', async () => {
    stubLaptop().answers('POST', TABLE_PATH, emptyAnswer(404))

    const screen = await mountScreen()

    expect(screen.get('[data-test="load-failed"]').text()).toBe(
      'Dieser Tisch konnte nicht geladen werden. Öffnen Sie ihn noch einmal über den Tischplan.',
    )
  })

  it('says the same in English', async () => {
    stubLaptop().answers('POST', TABLE_PATH, emptyAnswer(404))

    const screen = await mountScreen('en')

    expect(screen.get('[data-test="load-failed"]').text()).toBe(
      'This table could not be loaded. Open it again from the table plan.',
    )
  })

  it('sends the waiter to the table plan when no table was chosen, as after a reload', async () => {
    const laptop = stubLaptop()
    localStorage.setItem(TOKEN_STORAGE_KEY, 'lookup.token-here')
    useSessionStore().deviceToken = 'token-here'

    mount(OpenItems, { global: { plugins: testPlugins() }, attachTo: document.body })
    await flushPromises()

    expect({ route: currentRoute.value, asked: laptop.urls() }).toEqual({ route: { name: 'home' }, asked: [] })
  })

  it('closes the table when the waiter leaves, so the next table starts with nothing ticked', async () => {
    openItemsLaptop(TABLE_REPORT)
    const screen = await mountScreen()
    useOpenItemsStore().toggleItem('item-plain')

    screen.unmount()

    expect({ table: useOpenItemsStore().tableId, ticked: useOpenItemsStore().selectedItemIds }).toEqual({
      table: null,
      ticked: [],
    })
  })
})

describe('a table opened with the order just sent ticked', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
    navigate('/open-items')
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  function tickOf(screen: Awaited<ReturnType<typeof mountScreen>>, orderNumber: number) {
    return orderCard(screen, orderNumber)
      .findAll('[data-test="line-tick"] input')
      .map((tick) => (tick.element as HTMLInputElement).checked)
  }

  it('ticks the open items of the order just sent and leaves the older order of the table unticked', async () => {
    openItemsLaptop(TABLE_REPORT)
    localStorage.setItem(TOKEN_STORAGE_KEY, 'lookup.token-here')
    useSessionStore().deviceToken = 'token-here'
    useOpenItemsStore().openTableAndSelectItemsOnceLoaded('table-12', ['item-half', 'item-waiting', 'item-settled'])

    const screen = mount(OpenItems, { global: { plugins: testPlugins() }, attachTo: document.body })
    await flushPromises()

    expect({ older: tickOf(screen, 137), justSent: tickOf(screen, 138) }).toEqual({
      older: [false],
      justSent: [true, true],
    })
  })
})

describe('a table with an item a waiter cancelled', () => {
  const WITH_CANCELLED_BIER = {
    ...TISCH_12,
    orders: [
      {
        ...TISCH_12.orders[0],
        items: [TISCH_12.orders[0].items[0], { ...TISCH_12.orders[0].items[1], state: 'cancelled' }],
      },
    ],
  }

  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
    navigate('/open-items')
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('shows the cancelled item struck through', async () => {
    openItemsLaptop(WITH_CANCELLED_BIER)

    const screen = await mountScreen()

    expect(lineNamed(screen, 'Bier').get('[data-test="line-name-cell"]').classes()).toContain('cancelled-item')
    expect(lineNamed(screen, 'Bratwurst').get('[data-test="line-name-cell"]').classes()).not.toContain('cancelled-item')
  })

  it('keeps the tick box column on the cancelled item and leaves it empty, so the names stay in line', async () => {
    openItemsLaptop(WITH_CANCELLED_BIER)

    const screen = await mountScreen()

    const bier = lineNamed(screen, 'Bier')
    expect(bier.find('[data-test="tick-cell"]').exists()).toBe(true)
    expect(bier.find('[data-test="line-tick"]').exists()).toBe(false)
  })
})

describe('tapping an item on the settle and the cancel screen', () => {
  const BRATWURST_HISTORY = {
    statusChanges: [{ state: 'open', changedAtUtc: '2026-09-05T18:00:00Z', staffMemberName: 'Anna', stationName: null }],
    settledAtUtc: null,
    chargedPriceCents: null,
    paymentMethod: null,
    paymentNotice: null,
    settledByStaffMemberName: null,
  }

  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
    navigate('/open-items')
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it.each([
    ['settle', mountScreen],
    ['cancel', mountScreenToCancelItems],
  ])('opens the price and the history and ticks nothing when the waiter taps the details button on the %s screen', async (_, mountTheScreen) => {
    openItemsLaptop(TISCH_12).answers('GET', '/api/open-items/items/item-1/history', answer(BRATWURST_HISTORY))
    const screen = await mountTheScreen()

    await lineNamed(screen, 'Bratwurst').get('[data-test="item-details"]').trigger('click')
    await flushPromises()

    expect({
      price: document.querySelector('[data-test="details-price"]')?.textContent?.trim(),
      history: document.querySelector('[data-test="entry-event"]')?.textContent?.trim(),
      ticked: useOpenItemsStore().selectedItemIds,
    }).toEqual({ price: 'Preis: 3,50 €', history: 'Bestellt', ticked: [] })
  })

  it.each([
    ['settle', mountScreen],
    ['cancel', mountScreenToCancelItems],
  ])('ticks the item and opens no history when the waiter taps its row on the %s screen', async (_, mountTheScreen) => {
    const laptop = openItemsLaptop(TISCH_12)
    const screen = await mountTheScreen()

    await lineNamed(screen, 'Bratwurst').get('[data-test="line-name"]').trigger('click')
    await flushPromises()

    expect({
      historyAsked: laptop.callsTo('GET', '/api/open-items/items/item-1/history').length,
      ticked: useOpenItemsStore().selectedItemIds,
    }).toEqual({ historyAsked: 0, ticked: ['item-1'] })
  })

  it('unticks the item when the waiter taps its row again', async () => {
    openItemsLaptop(TISCH_12)
    const screen = await mountScreen()
    await lineNamed(screen, 'Bratwurst').get('[data-test="line-name"]').trigger('click')

    await lineNamed(screen, 'Bratwurst').get('[data-test="line-name"]').trigger('click')

    expect(useOpenItemsStore().selectedItemIds).toEqual([])
  })

  it('ticks nothing when the waiter taps the row of a paid item while taking payment', async () => {
    openItemsLaptop(TABLE_REPORT)
    const screen = await mountScreen()

    await lineNamed(screen, 'Wasser').get('[data-test="line-name"]').trigger('click')

    expect(useOpenItemsStore().selectedItemIds).toEqual([])
  })

  it('ticks the item and opens no history when the waiter taps its tick box', async () => {
    const laptop = openItemsLaptop(TISCH_12)
    const screen = await mountScreen()

    await lineNamed(screen, 'Bratwurst').get('[data-test="line-tick"] input').trigger('click')
    await flushPromises()

    expect({
      historyAsked: laptop.callsTo('GET', '/api/open-items/items/item-1/history').length,
      ticked: useOpenItemsStore().selectedItemIds,
    }).toEqual({ historyAsked: 0, ticked: ['item-1'] })
  })
})
