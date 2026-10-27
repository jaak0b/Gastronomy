import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { enableAutoUnmount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { useOpenItemsStore } from '../../../src/phone/stores/openItems'
import { TISCH_12, mountScreen, openItemsLaptop } from './openItemsFixture'
import { noConnection, refusal } from '../../support/laptop'
import { inputOf, typeIn, clickOn } from '../../support/dom'
import { nextTick } from 'vue'

enableAutoUnmount(afterEach)

describe('settling what the table actually handed over', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
    openItemsLaptop()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  async function openTheAmountDialog(screen: Awaited<ReturnType<typeof mountScreen>>) {
    const openItems = useOpenItemsStore()
    openItems.toggleItem('item-1')
    await nextTick()
    await screen.get('[data-test="settle"]').trigger('click')
    await flushPromises()
  }

  const AMOUNT_DIALOG = '[data-test="amount-paid-dialog"]'

  async function pressConfirm(button = '[data-test="confirm-in-cash"]'): Promise<void> {
    await clickOn(`${AMOUNT_DIALOG} ${button}`)
  }

  it('sends the smaller amount together with the typed reason', async () => {
    const laptop = openItemsLaptop()
    const screen = await mountScreen()
    await openTheAmountDialog(screen)

    await typeIn(`${AMOUNT_DIALOG} [data-test="amount-field"]`, '2,00')
    await typeIn(`${AMOUNT_DIALOG} [data-test="reason-field"]`, 'Stammgast')
    await pressConfirm()

    expect(laptop.bodiesSentTo('POST', '/api/open-items/settle')[0]).toEqual({
      lines: [{ orderItemId: 'item-1', paidPriceCents: 200, paymentNotice: 'Stammgast' }],
      paymentMethod: 'cash',
    })
  })

  it('asks for a reason as soon as the amount falls short, and sends nothing until it is there', async () => {
    const laptop = openItemsLaptop()
    const screen = await mountScreen()
    await openTheAmountDialog(screen)

    await typeIn(`${AMOUNT_DIALOG} [data-test="amount-field"]`, '2,00')
    await pressConfirm()

    expect(document.querySelector('[data-test="amount-paid-dialog"] [data-test="reason-field"]')).not.toBeNull()
    expect(
      document.querySelector('[data-test="amount-paid-dialog"] [data-test="confirm-in-cash"]')?.hasAttribute('disabled'),
    ).toBe(true)
    expect(laptop.bodiesSentTo('POST', '/api/open-items/settle')).toEqual([])
  })

  it('sends nothing at all when the table was given the items, until a reason stands', async () => {
    const laptop = openItemsLaptop()
    const screen = await mountScreen()
    await openTheAmountDialog(screen)

    await typeIn(`${AMOUNT_DIALOG} [data-test="amount-field"]`, '0')
    await typeIn(`${AMOUNT_DIALOG} [data-test="reason-field"]`, 'Essen fuer die Kapelle')
    await pressConfirm('[data-test="confirm-nothing-paid"]')

    expect(laptop.bodiesSentTo('POST', '/api/open-items/settle')[0]).toEqual({
      lines: [{ orderItemId: 'item-1', paidPriceCents: 0, paymentNotice: 'Essen fuer die Kapelle' }],
      paymentMethod: 'none',
    })
  })

  it('needs no reason when the amount matches what the selection costs', async () => {
    const laptop = openItemsLaptop()
    const screen = await mountScreen()
    await openTheAmountDialog(screen)

    await pressConfirm()

    expect(document.querySelector('[data-test="amount-paid-dialog"] [data-test="reason-field"]')).toBeNull()
    expect(laptop.bodiesSentTo('POST', '/api/open-items/settle')[0]).toEqual({
      lines: [{ orderItemId: 'item-1', paidPriceCents: 350, paymentNotice: null }],
      paymentMethod: 'cash',
    })
  })

  it('lets a guest round up without explaining themselves', async () => {
    const laptop = openItemsLaptop()
    const screen = await mountScreen()
    await openTheAmountDialog(screen)

    await typeIn(`${AMOUNT_DIALOG} [data-test="amount-field"]`, '5,00')
    await pressConfirm()

    expect(laptop.bodiesSentTo('POST', '/api/open-items/settle')[0]).toEqual({
      lines: [{ orderItemId: 'item-1', paidPriceCents: 500, paymentNotice: null }],
      paymentMethod: 'cash',
    })
  })

  it('sends nothing when the waiter backs out of the dialog', async () => {
    const laptop = openItemsLaptop()
    const screen = await mountScreen()
    await openTheAmountDialog(screen)

    await typeIn(`${AMOUNT_DIALOG} [data-test="amount-field"]`, '2,00')
    ;(document.querySelector('[data-test="amount-paid-dialog"] [data-test="cancel"]') as HTMLElement).click()
    await flushPromises()

    expect(laptop.bodiesSentTo('POST', '/api/open-items/settle')).toEqual([])
    expect(document.querySelector('[data-test="amount-paid-dialog"]')).toBeNull()
  })

  it('stops the reason where the laptop stops storing it', async () => {
    const screen = await mountScreen()
    await openTheAmountDialog(screen)

    await typeIn(`${AMOUNT_DIALOG} [data-test="amount-field"]`, '2,00')

    expect(inputOf(`${AMOUNT_DIALOG} [data-test="reason-field"]`).getAttribute('maxlength')).toBe('200')
  })

  it('goes back to the table when the answer never came, because the table is where the reload is', async () => {
    const screen = await mountScreen()
    openItemsLaptop(TISCH_12, noConnection())
    await openTheAmountDialog(screen)

    await typeIn(`${AMOUNT_DIALOG} [data-test="amount-field"]`, '2,00')
    await typeIn(`${AMOUNT_DIALOG} [data-test="reason-field"]`, 'Stammgast')
    await pressConfirm()

    expect(document.querySelector('[data-test="amount-paid-dialog"]')).toBeNull()
    expect(screen.get('[data-test="settle-notice"]').text()).toBe(
      'Es ist nicht klar, ob die Zahlung gespeichert wurde. Tippen Sie auf "Neu laden" und schauen Sie nach, ob die Positionen noch offen sind.',
    )
    expect(useOpenItemsStore().selectedItemIds).toEqual(['item-1'])
  })

  it('keeps what was typed on screen when the laptop refuses, so nobody types it twice', async () => {
    const screen = await mountScreen()
    openItemsLaptop(
      TISCH_12,
      refusal('phone.openItems.errors.settleFailed', { status: 400, code: 'ValidationFailed' }),
    )
    await openTheAmountDialog(screen)

    await typeIn(`${AMOUNT_DIALOG} [data-test="amount-field"]`, '2,00')
    await typeIn(`${AMOUNT_DIALOG} [data-test="reason-field"]`, 'Stammgast')
    await pressConfirm()

    expect(inputOf(`${AMOUNT_DIALOG} [data-test="amount-field"]`).value).toBe('2,00')
    expect(inputOf(`${AMOUNT_DIALOG} [data-test="reason-field"]`).value).toBe('Stammgast')
    expect(document.querySelector('[data-test="amount-paid-dialog"]')?.textContent).toContain(
      'Die Zahlung wurde nicht gespeichert. Versuchen Sie es noch einmal.',
    )
  })

  it('names the item a waiter cancelled while this one was settling', async () => {
    const screen = await mountScreen()
    openItemsLaptop(
      TISCH_12,
      refusal('errors.settlement.itemWasCancelled', { code: 'ItemWasCancelled', parameters: { name: 'Bratwurst' } }),
    )
    await openTheAmountDialog(screen)

    await pressConfirm()

    expect({
      reason: document.querySelector(`${AMOUNT_DIALOG} [data-test="settle-notice"]`)?.textContent?.trim(),
      reload: document.querySelector(`${AMOUNT_DIALOG} [data-test="reload-after-refusal"]`)?.textContent?.trim(),
    }).toEqual({
      reason: 'Bratwurst wurde inzwischen storniert. Tippen Sie auf "Neu laden" und wählen Sie die Positionen noch einmal aus.',
      reload: 'Neu laden',
    })
  })

  it('closes the amount page and loads the table again when the waiter taps the reload after a refusal', async () => {
    const screen = await mountScreen()
    const laptop = openItemsLaptop(
      TISCH_12,
      refusal('errors.settlement.itemWasCancelled', { code: 'ItemWasCancelled', parameters: { name: 'Bratwurst' } }),
    )
    await openTheAmountDialog(screen)
    await pressConfirm()

    await clickOn(`${AMOUNT_DIALOG} [data-test="reload-after-refusal"]`)

    expect({
      amountPageOpen: document.querySelector(AMOUNT_DIALOG) !== null,
      tableLoads: laptop.callsTo('POST', '/api/open-items/tables/table-12/search').length,
    }).toEqual({ amountPageOpen: false, tableLoads: 2 })
  })

  it('leaves no reason on the table once the waiter taps the reload after a refusal', async () => {
    const screen = await mountScreen()
    openItemsLaptop(
      TISCH_12,
      refusal('errors.settlement.itemWasCancelled', { code: 'ItemWasCancelled', parameters: { name: 'Bratwurst' } }),
    )
    await openTheAmountDialog(screen)
    await pressConfirm()

    await clickOn(`${AMOUNT_DIALOG} [data-test="reload-after-refusal"]`)

    expect(document.querySelector('[data-test="settle-notice"]') !== null).toBe(false)
  })

  it('shows the old reason neither on the table nor on the amount page once the waiter backed out', async () => {
    const screen = await mountScreen()
    openItemsLaptop(
      TISCH_12,
      refusal('errors.settlement.itemWasCancelled', { code: 'ItemWasCancelled', parameters: { name: 'Bratwurst' } }),
    )
    await openTheAmountDialog(screen)
    await pressConfirm()

    await clickOn(`${AMOUNT_DIALOG} [data-test="cancel"]`)
    const onTheTable = document.querySelector('[data-test="settle-notice"]') !== null
    await clickOn('[data-test="settle"]')

    expect({
      onTheTable,
      onTheAmountPage: document.querySelector(`${AMOUNT_DIALOG} [data-test="settle-notice"]`) !== null,
    }).toEqual({ onTheTable: false, onTheAmountPage: false })
  })
})
