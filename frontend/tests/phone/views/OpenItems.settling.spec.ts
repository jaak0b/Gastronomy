import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { useOpenItemsStore } from '../../../src/phone/stores/openItems'
import { OPEN_LIST, mountScreen, openItemsLaptop } from './openItemsFixture'
import { answer, noConnection, refusal } from '../../support/laptop'
import { inputOf, typeIn, clickOn } from '../../support/dom'
import { nextTick } from 'vue'

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
    await screen.get('[data-test="settle-amount-paid"]').trigger('click')
    await flushPromises()
  }

  const AMOUNT_DIALOG = '[data-test="amount-paid-dialog"]'

  async function pressConfirm(button = '[data-test="confirm-in-cash"]'): Promise<void> {
    await clickOn(`${AMOUNT_DIALOG} ${button}`)
  }

  it('offers the selection as the amount, so the ordinary case is one more tap', async () => {
    const screen = await mountScreen()

    await openTheAmountDialog(screen)

    expect(document.querySelector('[data-test="amount-paid-dialog"] [data-test="selected-total"]')?.textContent).toContain(
      'Ausgewählt: 3,50 €',
    )
    expect(inputOf(`${AMOUNT_DIALOG} [data-test="amount-field"]`).value).toBe('3,50')
  })

  it('sends the smaller amount together with the typed reason', async () => {
    const laptop = openItemsLaptop()
    const screen = await mountScreen()
    await openTheAmountDialog(screen)

    await typeIn(`${AMOUNT_DIALOG} [data-test="amount-field"]`, '2,00')
    await typeIn(`${AMOUNT_DIALOG} [data-test="reason-field"]`, 'Stammgast')
    await pressConfirm()

    expect(laptop.writtenBodies()[0]).toEqual({
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
    expect(laptop.writtenBodies()).toEqual([])
  })

  it('sends nothing at all when the table was given the items, until a reason stands', async () => {
    const laptop = openItemsLaptop()
    const screen = await mountScreen()
    await openTheAmountDialog(screen)

    await typeIn(`${AMOUNT_DIALOG} [data-test="amount-field"]`, '0')
    await typeIn(`${AMOUNT_DIALOG} [data-test="reason-field"]`, 'Essen fuer die Kapelle')
    await pressConfirm('[data-test="confirm-nothing-paid"]')

    expect(laptop.writtenBodies()[0]).toEqual({
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
    expect(laptop.writtenBodies()[0]).toEqual({
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

    expect(laptop.writtenBodies()[0]).toEqual({
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

    expect(laptop.writtenBodies()).toEqual([])
    expect(document.querySelector('[data-test="amount-paid-dialog"]')).toBeNull()
  })

  it('stops the reason where the laptop stops storing it', async () => {
    const screen = await mountScreen()
    await openTheAmountDialog(screen)

    await typeIn(`${AMOUNT_DIALOG} [data-test="amount-field"]`, '2,00')

    expect(inputOf(`${AMOUNT_DIALOG} [data-test="reason-field"]`).getAttribute('maxlength')).toBe('200')
  })

  it('goes back to the list when the answer never came, because the list is where the reload is', async () => {
    const screen = await mountScreen()
    openItemsLaptop(OPEN_LIST, noConnection())
    await openTheAmountDialog(screen)

    await typeIn(`${AMOUNT_DIALOG} [data-test="amount-field"]`, '2,00')
    await typeIn(`${AMOUNT_DIALOG} [data-test="reason-field"]`, 'Stammgast')
    await pressConfirm()

    expect(document.querySelector('[data-test="amount-paid-dialog"]')).toBeNull()
    expect(screen.get('[data-test="settle-notice"]').text()).toBe(
      'Es ist nicht klar, ob die Abrechnung angekommen ist. Laden Sie die Liste neu und schauen Sie nach, ob die Positionen noch offen sind.',
    )
    expect(useOpenItemsStore().selectedItemIds).toEqual(['item-1'])
  })

  it('keeps what was typed on screen when the laptop refuses, so nobody types it twice', async () => {
    const screen = await mountScreen()
    openItemsLaptop(
      OPEN_LIST,
      refusal('phone.openItems.errors.settleFailed', { status: 400, code: 'ValidationFailed' }),
    )
    await openTheAmountDialog(screen)

    await typeIn(`${AMOUNT_DIALOG} [data-test="amount-field"]`, '2,00')
    await typeIn(`${AMOUNT_DIALOG} [data-test="reason-field"]`, 'Stammgast')
    await pressConfirm()

    expect(inputOf(`${AMOUNT_DIALOG} [data-test="amount-field"]`).value).toBe('2,00')
    expect(inputOf(`${AMOUNT_DIALOG} [data-test="reason-field"]`).value).toBe('Stammgast')
    expect(document.querySelector('[data-test="amount-paid-dialog"]')?.textContent).toContain(
      'Versuchen Sie es noch einmal. Das Abrechnen ist fehlgeschlagen.',
    )
  })
})
