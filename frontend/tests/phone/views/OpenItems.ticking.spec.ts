import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { enableAutoUnmount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { useOpenItemsStore } from '../../../src/phone/stores/openItems'
import { TABLE_REPORT, TISCH_12, isHalfTaken, lineNamed, orderCard, mountScreen, openItemsLaptop } from './openItemsFixture'
import { answer } from '../../support/laptop'
import { clickOn } from '../../support/dom'
import { nextTick } from 'vue'

enableAutoUnmount(afterEach)

function tickOf(screen: Awaited<ReturnType<typeof mountScreen>>, orderNumber: number) {
  return orderCard(screen, orderNumber)
    .findAll('[data-test="line-tick"] input')
    .map((tick) => (tick.element as HTMLInputElement).checked)
}

describe('ticking what the guests at one table pay for', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
    openItemsLaptop(TABLE_REPORT)
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('offers no settling action while the waiter has ticked nothing', async () => {
    const screen = await mountScreen()

    expect(screen.find('[data-test="settle-footer"]').exists()).toBe(false)
  })

  it('ticks an item when the waiter taps its row, so the whole row is the target', async () => {
    const screen = await mountScreen()

    await lineNamed(orderCard(screen, 137), 'Bratwurst').trigger('click')

    expect(useOpenItemsStore().selectedItemIds).toEqual(['item-plain'])
  })

  it('settles a ticked position through the footer under the orders', async () => {
    const laptop = openItemsLaptop(TABLE_REPORT)
    const screen = await mountScreen()
    await lineNamed(orderCard(screen, 137), 'Bratwurst').trigger('click')
    await nextTick()
    expect(screen.get('[data-test="selected-total"]').text()).toBe('Ausgewählt: 3,50 €')
    await screen.get('[data-test="settle"]').trigger('click')
    await flushPromises()

    await clickOn('[data-test="amount-paid-dialog"] [data-test="confirm-in-cash"]')

    expect(laptop.writtenBodies()[0]).toEqual({
      lines: [{ orderItemId: 'item-plain', paidPriceCents: 350, paymentNotice: null }],
      paymentMethod: 'cash',
    })
  })

  it('takes the whole table at once across its orders', async () => {
    const screen = await mountScreen()

    await screen.get('[data-test="whole-table"] input').trigger('click')
    await flushPromises()

    expect(screen.get('[data-test="selected-total"]').text()).toBe('Ausgewählt: 12,00 €')
  })

  it('shows the whole table as half taken while only some of its items are ticked', async () => {
    const screen = await mountScreen()

    await lineNamed(orderCard(screen, 137), 'Bratwurst').trigger('click')
    await nextTick()

    expect(isHalfTaken(screen, '[data-test="whole-table"]')).toBe(true)
  })

  it('ticks every open item of one order from its header and leaves the other orders alone', async () => {
    const screen = await mountScreen()

    await orderCard(screen, 138).get('[data-test="whole-order"] input').trigger('click')
    await nextTick()

    expect({ first: tickOf(screen, 137), second: tickOf(screen, 138), third: tickOf(screen, 139) }).toEqual({
      first: [false],
      second: [true, true],
      third: [false],
    })
  })

  it('shows an order as half taken after one of its items is unticked again', async () => {
    const screen = await mountScreen()
    const card = orderCard(screen, 138)
    await card.get('[data-test="whole-order"] input').trigger('click')
    await nextTick()

    await lineNamed(card, 'Bier').trigger('click')
    await nextTick()

    expect(isHalfTaken(card, '[data-test="whole-order"]')).toBe(true)
  })

  it('ticks the rest of a half taken order when its header is tapped', async () => {
    const screen = await mountScreen()
    await lineNamed(orderCard(screen, 138), 'Bier').trigger('click')
    await nextTick()

    await orderCard(screen, 138).get('[data-test="whole-order"] input').trigger('click')
    await nextTick()

    expect(tickOf(screen, 138)).toEqual([true, true])
  })

  it('offers no header tick on an order whose items are all settled', async () => {
    const paidOrder = { ...TABLE_REPORT.orders[1], items: [TABLE_REPORT.orders[1].items[2]] }
    openItemsLaptop({ ...TABLE_REPORT, orders: [TABLE_REPORT.orders[0], paidOrder] })

    const screen = await mountScreen()

    expect({
      open: orderCard(screen, 137).find('[data-test="whole-order"]').exists(),
      paid: orderCard(screen, 138).find('[data-test="whole-order"]').exists(),
    }).toEqual({ open: true, paid: false })
  })

  it('fetches the table again when the waiter asks for it, which is what the notices name', async () => {
    const laptop = openItemsLaptop(TABLE_REPORT)
    const screen = await mountScreen()

    await screen.get('[data-test="reload"]').trigger('click')
    await flushPromises()

    expect(laptop.urls()).toEqual(['/api/open-items/tables/table-12', '/api/open-items/tables/table-12'])
  })
})

describe('settling items somebody else had already settled', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('warns that somebody else had already settled part of the selection', async () => {
    openItemsLaptop(
      TISCH_12,
      answer({
        settledOrderItemIds: ['item-1'],
        reappliedOrderItemIds: [],
        alreadySettledByOthersOrderItemIds: ['item-2'],
      }),
    )
    const screen = await mountScreen()
    useOpenItemsStore().setWholeTable(true)
    await nextTick()
    await screen.get('[data-test="settle"]').trigger('click')
    await flushPromises()

    await clickOn('[data-test="amount-paid-dialog"] [data-test="confirm-in-cash"]')

    expect(screen.get('[data-test="settle-notice"]').text()).toBe(
      'Jemand anderes hatte 1 Position aus Ihrer Auswahl schon abgerechnet. Geben Sie dem Gast 3,50 € zurück.',
    )
  })
})
