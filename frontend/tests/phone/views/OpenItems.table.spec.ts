import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { enableAutoUnmount, flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import OpenItems from '../../../src/phone/views/OpenItems.vue'
import { useOpenItemsStore } from '../../../src/phone/stores/openItems'
import { currentRoute, navigate } from '../../../src/shared/router/router'
import { TOKEN_STORAGE_KEY, useSessionStore } from '../../../src/shared/stores/session'
import { testPlugins } from '../../support/plugins'
import { TABLE_REPORT, lineNamed, orderCard, mountScreen, openItemsLaptop } from './openItemsFixture'
import { answer, emptyAnswer, inTurn, stubLaptop } from '../../support/laptop'

enableAutoUnmount(afterEach)

const TABLE_PATH = '/api/open-items/tables/table-12'

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

    expect([germanTitle, english.get('[data-test="settle-title"]').text()]).toEqual(['Abrechnen', 'Settle'])
  })

  it('colours each order by how far its positions have been produced', async () => {
    openItemsLaptop(TABLE_REPORT)

    const screen = await mountScreen()

    expect([137, 138, 139].map((orderNumber) => orderCard(screen, orderNumber).attributes('data-state'))).toEqual([
      'none',
      'some',
      'all',
    ])
  })

  it('marks each position as produced or not, with an icon beside the state', async () => {
    openItemsLaptop(TABLE_REPORT)

    const screen = await mountScreen()

    const notProduced = lineNamed(orderCard(screen, 137), 'Bratwurst')
    expect(notProduced.classes()).toContain('is-not-produced')
    expect(notProduced.get('[data-test="line-state"]').classes()).toContain('mdi-clock-outline')
    const produced = lineNamed(orderCard(screen, 139), 'Kuchen')
    expect(produced.classes()).toContain('is-produced')
    expect(produced.get('[data-test="line-state"]').classes()).toContain('mdi-check')
  })

  it('writes the word for paid on a settled position and leaves out its tick box', async () => {
    openItemsLaptop(TABLE_REPORT)

    const screen = await mountScreen()

    const settled = lineNamed(orderCard(screen, 138), 'Wasser')
    expect(settled.get('[data-test="line-paid"]').text()).toBe('Bezahlt')
    expect(settled.find('[data-test="line-tick"]').exists()).toBe(false)
  })

  it('says that the table has no orders at all when the laptop finds none', async () => {
    openItemsLaptop({ ...TABLE_REPORT, openAmountCents: 0, orders: [] })

    const screen = await mountScreen()

    expect(screen.get('[data-test="no-orders"]').text()).toBe('Für diesen Tisch gibt es keine Bestellungen.')
  })

  it('says the laptop could not be reached and loads the table again from the reload button', async () => {
    const laptop = stubLaptop().answers('GET', TABLE_PATH, inTurn(answer({}, 500), answer(TABLE_REPORT)))
    const screen = await mountScreen()
    expect(screen.get('[data-test="load-failed"]').text()).toBe(
      'Der Rechner war nicht erreichbar. Tippen Sie auf "Neu laden".',
    )

    await screen.get('[data-test="reload"]').trigger('click')
    await flushPromises()

    expect(laptop.callsTo('GET', TABLE_PATH)).toHaveLength(2)
    expect(screen.findAll('[data-test="order-card"]')).toHaveLength(3)
  })

  it('says in one sentence that the table could not be loaded when the laptop does not know it', async () => {
    stubLaptop().answers('GET', TABLE_PATH, emptyAnswer(404))

    const screen = await mountScreen()

    expect(screen.get('[data-test="load-failed"]').text()).toBe(
      'Dieser Tisch konnte nicht geladen werden. Öffnen Sie ihn noch einmal über den Tischplan.',
    )
  })

  it('says the same in English', async () => {
    stubLaptop().answers('GET', TABLE_PATH, emptyAnswer(404))

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
