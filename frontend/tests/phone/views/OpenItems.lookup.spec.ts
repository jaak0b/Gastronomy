import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import OpenItems from '../../../src/phone/views/OpenItems.vue'
import { TABLE_LOOKUP_DEBOUNCE_MS } from '../../../src/phone/core/openItems'
import { useOpenItemsStore } from '../../../src/phone/stores/openItems'
import { TOKEN_STORAGE_KEY, useSessionStore } from '../../../src/shared/stores/session'
import { testPlugins } from '../../support/plugins'
import { OPEN_LIST, TABLE_REPORT, lineNamed, lookupCard, isHalfTaken, laptopWithALookup } from './openItemsFixture'
import { stubLaptop, answer } from '../../support/laptop'
import { nextTick } from 'vue'

describe('looking up one table from the screen that shows what is open', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
  })

  afterEach(() => {
    vi.useRealTimers()
    vi.unstubAllGlobals()
    document.body.innerHTML = ''
  })

  async function mountTheScreenWithALookup(
    list: unknown = OPEN_LIST,
    report: unknown = TABLE_REPORT,
  ) {
    const laptop = laptopWithALookup(report, list)
    localStorage.setItem(TOKEN_STORAGE_KEY, 'lookup.token-here')
    const session = useSessionStore()
    session.deviceToken = 'token-here'
    session.language = 'de'
    const screen = mount(OpenItems, {
      global: { plugins: testPlugins() },
      attachTo: document.body,
    })
    await flushPromises()
    vi.useFakeTimers()
    return { screen, laptop }
  }

  async function typeTheTableName(
    screen: Awaited<ReturnType<typeof mountTheScreenWithALookup>>['screen'],
    name: string,
  ): Promise<void> {
    await screen.get('[data-test="table-field"] input').setValue(name)
    await vi.advanceTimersByTimeAsync(TABLE_LOOKUP_DEBOUNCE_MS)
    await nextTick()
  }

  async function arriveFromAnOrderSentToBeSettled(itemIds: string[]) {
    laptopWithALookup(TABLE_REPORT)
    localStorage.setItem(TOKEN_STORAGE_KEY, 'lookup.token-here')
    const session = useSessionStore()
    session.deviceToken = 'token-here'
    session.language = 'de'
    useOpenItemsStore().openTableAndSelectItemsOnceLoaded('Tisch 12', itemIds)
    const screen = mount(OpenItems, {
      global: { plugins: testPlugins() },
      attachTo: document.body,
    })
    await flushPromises()
    return screen
  }

  function tickOf(screen: Awaited<ReturnType<typeof mountTheScreenWithALookup>>['screen'], orderNumber: number) {
    return lookupCard(screen, orderNumber)
      .findAll('[data-test="line-tick"] input')
      .map((tick) => (tick.element as HTMLInputElement).checked)
  }

  it('shows the table of the order just sent in the search field', async () => {
    const screen = await arriveFromAnOrderSentToBeSettled(['item-half', 'item-waiting', 'item-settled'])

    expect((screen.get('[data-test="table-field"] input').element as HTMLInputElement).value).toBe('Tisch 12')
  })

  it('ticks the open items of the order just sent and leaves the older order of the table unticked', async () => {
    const screen = await arriveFromAnOrderSentToBeSettled(['item-half', 'item-waiting', 'item-settled'])

    expect({ older: tickOf(screen, 137), justSent: tickOf(screen, 138) }).toEqual({
      older: [false],
      justSent: [true, true],
    })
  })

  it('starts with an empty search when the screen is opened again later', async () => {
    const first = await arriveFromAnOrderSentToBeSettled(['item-waiting'])
    first.unmount()

    const again = mount(OpenItems, { global: { plugins: testPlugins() }, attachTo: document.body })
    await flushPromises()

    expect({
      typed: (again.get('[data-test="table-field"] input').element as HTMLInputElement).value,
      ticked: useOpenItemsStore().selectedItemIds,
    }).toEqual({ typed: '', ticked: [] })
  })

  it('hides the list and its notices while the lookup shows the orders of a table', async () => {
    const { screen } = await mountTheScreenWithALookup({
      ...OPEN_LIST,
      tables: [],
      itemsWithoutAnOrderCount: 2,
    })
    expect(screen.find('[data-test="empty"]').exists()).toBe(true)
    expect(screen.find('[data-test="list-incomplete"]').exists()).toBe(true)

    await typeTheTableName(screen, 'Tisch 12')

    expect(screen.find('[data-test="empty"]').exists()).toBe(false)
    expect(screen.find('[data-test="list-incomplete"]').exists()).toBe(false)
    expect(screen.find('[data-test="open-table"]').exists()).toBe(false)
    expect(screen.findAll('[data-test="lookup-card"]')).toHaveLength(3)
  })

  it('brings the list back when the waiter clears the table name', async () => {
    const { screen } = await mountTheScreenWithALookup()
    await typeTheTableName(screen, 'Tisch 12')
    expect(screen.findAll('[data-test="lookup-card"]')).toHaveLength(3)

    await screen.get('[data-test="table-field"] input').setValue('')
    await nextTick()

    expect(screen.findAll('[data-test="lookup-card"]')).toHaveLength(0)
    expect(screen.find('[data-test="open-table"]').exists()).toBe(true)
  })

  it('colours each order by how far its positions have been produced', async () => {
    const { screen } = await mountTheScreenWithALookup()

    await typeTheTableName(screen, 'Tisch 12')

    expect([137, 138, 139].map((orderNumber) => lookupCard(screen, orderNumber).attributes('data-state'))).toEqual([
      'none',
      'some',
      'all',
    ])
  })

  it('marks each position as produced or not, with an icon beside the state', async () => {
    const { screen } = await mountTheScreenWithALookup()

    await typeTheTableName(screen, 'Tisch 12')

    const notProduced = lineNamed(lookupCard(screen, 137), 'Bratwurst')
    expect(notProduced.classes()).toContain('is-not-produced')
    expect(notProduced.get('[data-test="line-state"]').classes()).toContain('mdi-clock-outline')

    const produced = lineNamed(lookupCard(screen, 139), 'Kuchen')
    expect(produced.classes()).toContain('is-produced')
    expect(produced.get('[data-test="line-state"]').classes()).toContain('mdi-check')
  })

  it('writes the word for paid on a settled position and leaves out its tick box', async () => {
    const { screen } = await mountTheScreenWithALookup()

    await typeTheTableName(screen, 'Tisch 12')

    const settled = lineNamed(lookupCard(screen, 138), 'Wasser')
    expect(settled.get('[data-test="line-paid"]').text()).toBe('Bezahlt')
    expect(settled.find('[data-test="line-tick"]').exists()).toBe(false)
  })

  it('settles a ticked position through the same footer as the list', async () => {
    const { screen, laptop } = await mountTheScreenWithALookup()

    await typeTheTableName(screen, 'Tisch 12')
    await lineNamed(lookupCard(screen, 137), 'Bratwurst').trigger('click')
    await nextTick()
    expect(screen.get('[data-test="selected-total"]').text()).toBe('Ausgewählt: 3,50 €')

    await screen.get('[data-test="settle-in-cash"]').trigger('click')
    await vi.advanceTimersByTimeAsync(0)

    expect(laptop.writtenBodies()[0]).toEqual({
      lines: [{ orderItemId: 'item-plain', paidPriceCents: 350, paymentNotice: null }],
      paymentMethod: 'cash',
    })
  })

  it('takes the whole table at once across its orders', async () => {
    const { screen } = await mountTheScreenWithALookup()

    await typeTheTableName(screen, 'Tisch 12')
    await screen.get('[data-test="whole-table"] input').trigger('click')
    await vi.advanceTimersByTimeAsync(0)

    expect(screen.get('[data-test="selected-total"]').text()).toBe('Ausgewählt: 12,00 €')
  })

  it('shows the whole table as half taken while only some of its items are ticked', async () => {
    const { screen } = await mountTheScreenWithALookup()

    await typeTheTableName(screen, 'Tisch 12')
    await lineNamed(lookupCard(screen, 137), 'Bratwurst').trigger('click')
    await nextTick()

    expect(isHalfTaken(screen, '[data-test="whole-table"]')).toBe(true)
  })

  it('ticks every open item of one order from its header and leaves the other orders alone', async () => {
    const { screen } = await mountTheScreenWithALookup()

    await typeTheTableName(screen, 'Tisch 12')
    await lookupCard(screen, 138).get('[data-test="whole-order"] input').trigger('click')
    await nextTick()

    expect({ first: tickOf(screen, 137), second: tickOf(screen, 138), third: tickOf(screen, 139) }).toEqual({
      first: [false],
      second: [true, true],
      third: [false],
    })
  })

  it('shows an order as half taken after one of its items is unticked again', async () => {
    const { screen } = await mountTheScreenWithALookup()
    await typeTheTableName(screen, 'Tisch 12')
    const card = lookupCard(screen, 138)
    await card.get('[data-test="whole-order"] input').trigger('click')
    await nextTick()

    await lineNamed(card, 'Bier').trigger('click')
    await nextTick()

    expect(isHalfTaken(card, '[data-test="whole-order"]')).toBe(true)
  })

  it('ticks the rest of a half taken order when its header is tapped', async () => {
    const { screen } = await mountTheScreenWithALookup()
    await typeTheTableName(screen, 'Tisch 12')
    await lineNamed(lookupCard(screen, 138), 'Bier').trigger('click')
    await nextTick()

    await lookupCard(screen, 138).get('[data-test="whole-order"] input').trigger('click')
    await nextTick()

    expect(tickOf(screen, 138)).toEqual([true, true])
  })

  it('offers no header tick on an order whose items are all settled', async () => {
    const paidOrder = {
      ...TABLE_REPORT.orders[1],
      items: [TABLE_REPORT.orders[1].items[2]],
    }
    const { screen } = await mountTheScreenWithALookup(OPEN_LIST, {
      ...TABLE_REPORT,
      orders: [TABLE_REPORT.orders[0], paidOrder],
    })

    await typeTheTableName(screen, 'Tisch 12')

    const cards = screen.findAll('[data-test="lookup-card"]')
    expect({
      open: cards[0].find('[data-test="whole-order"]').exists(),
      paid: cards[1].find('[data-test="whole-order"]').exists(),
    }).toEqual({ open: true, paid: false })
  })

  it('says that the table has no orders at all when the lookup finds none', async () => {
    const { screen } = await mountTheScreenWithALookup(OPEN_LIST, {
      tableName: 'Tisch 99',
      openAmountCents: 0,
      orders: [],
    })

    await typeTheTableName(screen, 'Tisch 99')

    expect(screen.get('[data-test="lookup-empty"]').text()).toBe(
      'Für diesen Tisch gibt es keine Bestellungen.',
    )
    expect(screen.find('[data-test="open-table"]').exists()).toBe(false)
  })

  it('says the laptop could not be reached and loads the lookup again from the reload button', async () => {
    const laptop = stubLaptop()
      .answersEverythingElse(answer(OPEN_LIST))
      .answers('GET', '/api/open-items/table-names', answer({ tableNames: [] }))
      .answers('GET', '/api/open-items/table', answer({}, 500))
    localStorage.setItem(TOKEN_STORAGE_KEY, 'lookup.token-here')
    const session = useSessionStore()
    session.deviceToken = 'token-here'
    session.language = 'de'
    const screen = mount(OpenItems, {
      global: { plugins: testPlugins() },
      attachTo: document.body,
    })
    await flushPromises()
    vi.useFakeTimers()

    await typeTheTableName(screen, 'Tisch 12')

    expect(screen.get('[data-test="load-failed"]').text()).toBe(
      'Tippen Sie auf "Liste neu laden". Der Rechner war nicht erreichbar, deshalb kann diese Liste veraltet sein.',
    )
    expect(screen.find('[data-test="open-table"]').exists()).toBe(false)

    await screen.get('[data-test="reload"]').trigger('click')
    await vi.advanceTimersByTimeAsync(0)

    expect(laptop.callsTo('GET', '/api/open-items/table')).toHaveLength(2)
  })
})
