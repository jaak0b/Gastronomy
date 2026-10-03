import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import OpenItems from '../../../src/phone/views/OpenItems.vue'
import { useOpenItemsStore } from '../../../src/phone/stores/openItems'
import { TOKEN_STORAGE_KEY, useSessionStore } from '../../../src/shared/stores/session'
import { testPlugins } from '../../support/plugins'
import { OPEN_LIST, SETTLED, TWO_TABLES, openEveryTable, lineOf, mountScreen, openItemsLaptop } from './openItemsFixture'
import { answer, stubLaptop, neverAnswers } from '../../support/laptop'
import { nextTick } from 'vue'

describe('the screen that shows what the tables still owe', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
    openItemsLaptop()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('names every table with something open and what it still owes', async () => {
    const screen = await mountScreen()

    expect(screen.get('[data-test="open-table"] [data-test="table-name"]').text()).toBe('Tisch 12')
    expect(screen.get('[data-test="open-table"] [data-test="open-amount"]').text()).toBe('Offen: 7,00 €')
  })

  it('offers no settling action while the waiter has ticked nothing', async () => {
    const screen = await mountScreen()

    expect(screen.find('[data-test="settle-footer"]').exists()).toBe(false)
  })

  it('shows what has been ticked once the waiter takes a whole table', async () => {
    const screen = await mountScreen()
    const openItems = useOpenItemsStore()

    openItems.setWholeTable(openItems.tables[0], true)
    await nextTick()

    expect(screen.get('[data-test="selected-total"]').text()).toBe('Ausgewählt: 7,00 €')
  })

  it('keeps a table folded up until the waiter opens it, so the list stays readable', async () => {
    const screen = await mountScreen()

    expect(screen.findAll('[data-test="open-line"]')).toHaveLength(0)
  })

  it('ticks an item when the waiter taps its row, so the whole row is the target', async () => {
    const screen = await mountScreen()

    await screen.get('[data-test="open-table-title"]').trigger('click')
    await flushPromises()
    await lineOf(screen, '12', 'Bratwurst').trigger('click')

    expect(useOpenItemsStore().selectedItemIds).toEqual(['item-1'])
  })

  it('takes no tick from a second table, so one settlement can never span two tables', async () => {
    openItemsLaptop(TWO_TABLES, answer(SETTLED))
    const screen = await mountScreen()
    await openEveryTable(screen)
    const openItems = useOpenItemsStore()
    openItems.toggleItem('item-1')
    await nextTick()

    await lineOf(screen, '123', 'Bier').trigger('click')

    expect(openItems.selectedItemIds).toEqual(['item-1'])
  })

  it('offers no line of the table it is holding back, so nobody taps what does nothing', async () => {
    openItemsLaptop(TWO_TABLES, answer(SETTLED))
    const screen = await mountScreen()
    await openEveryTable(screen)
    useOpenItemsStore().toggleItem('item-1')
    await nextTick()

    expect(lineOf(screen, '12', 'Bratwurst').classes()).not.toContain('v-list-item--disabled')
    expect(lineOf(screen, '123', 'Bier').classes()).toContain('v-list-item--disabled')
  })

  it('takes a tick from any table again once nothing is ticked', async () => {
    openItemsLaptop(TWO_TABLES, answer(SETTLED))
    const screen = await mountScreen()
    await openEveryTable(screen)
    const openItems = useOpenItemsStore()
    openItems.toggleItem('item-1')
    await nextTick()
    openItems.toggleItem('item-1')
    await nextTick()

    await lineOf(screen, '123', 'Bier').trigger('click')

    expect(openItems.selectedItemIds).toEqual(['item-7'])
  })

  it('settles what has been ticked at the price the phone showed', async () => {
    const laptop = openItemsLaptop()
    const screen = await mountScreen()
    const openItems = useOpenItemsStore()
    openItems.toggleItem('item-1')
    await nextTick()

    await screen.get('[data-test="settle-in-cash"]').trigger('click')
    await flushPromises()

    expect(laptop.writtenBodies()[0]).toEqual({
      lines: [{ orderItemId: 'item-1', paidPriceCents: 350, paymentNotice: null }],
      paymentMethod: 'cash',
    })
    expect(document.querySelector('[data-test="amount-paid-dialog"]')).toBeNull()
  })

  it('fetches the list again when the waiter asks for it, which is what the notices name', async () => {
    const laptop = stubLaptop().answersEverythingElse(answer(OPEN_LIST))
    const screen = await mountScreen()
    const afterMounting = laptop.calls.length

    await screen.get('[data-test="reload"]').trigger('click')
    await flushPromises()

    expect(laptop.calls).toHaveLength(afterMounting + 1)
  })

  it('says that every table is settled when nothing is open', async () => {
    stubLaptop().answersEverythingElse(answer({ tables: [], itemsWithoutAnOrderCount: 0 }))
    const screen = await mountScreen()

    expect(screen.get('[data-test="empty"]').text()).toContain('Es ist nichts offen.')
  })

  it('claims nothing about the tables before the first list has arrived', async () => {
    stubLaptop().answersEverythingElse(neverAnswers())
    localStorage.setItem(TOKEN_STORAGE_KEY, 'lookup.token-here')
    const session = useSessionStore()
    session.deviceToken = 'token-here'
    session.language = 'de'

    const screen = mount(OpenItems, { global: { plugins: testPlugins() }, attachTo: document.body })
    await nextTick()

    expect(screen.find('[data-test="empty"]').exists()).toBe(false)
  })

  it('says that the list is short of items the laptop cannot trace back to an order', async () => {
    stubLaptop().answersEverythingElse(answer({ tables: [], itemsWithoutAnOrderCount: 2 }))
    const screen = await mountScreen()

    expect(screen.get('[data-test="list-incomplete"]').text()).toBe(
      'Fragen Sie am Tisch nach, was noch offen ist. Diese Liste zeigt 2 Positionen nicht, weil der Rechner die Bestellungen dazu nicht mehr findet.',
    )
  })

  it('warns that somebody else had already settled part of the selection', async () => {
    const screen = await mountScreen()
    openItemsLaptop(
      OPEN_LIST,
      answer({
          settledOrderItemIds: ['item-1'],
          reappliedOrderItemIds: [],
          alreadySettledByOthersOrderItemIds: ['item-2'],
        }),
    )
    const openItems = useOpenItemsStore()
    openItems.setWholeTable(openItems.tables[0], true)
    await nextTick()

    await screen.get('[data-test="settle-in-cash"]').trigger('click')
    await flushPromises()

    expect(screen.get('[data-test="settle-notice"]').text()).toBe(
      'Jemand anderes hatte 1 Position aus Ihrer Auswahl schon abgerechnet. Geben Sie dem Gast 3,50 € zurück.',
    )
  })
})
