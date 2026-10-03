import { afterEach, describe, expect, it, vi } from 'vitest'
import { enableAutoUnmount, flushPromises, mount } from '@vue/test-utils'
import AmountPaidDialog from '../../../../src/phone/components/openItems/AmountPaidDialog.vue'
import type { AppLanguage } from '../../../../src/shared/core/deviceLanguage'
import { testPlugins } from '../../../support/plugins'
import { inputOf, typeIn } from '../../../support/dom'

enableAutoUnmount(afterEach)

function mountDialog(language: AppLanguage = 'de', selectedTotalCents = 700) {
  return mount(AmountPaidDialog, {
    props: { isSettling: false, notice: null, selectedTotalCents, language },
    global: { plugins: testPlugins(language) },
    attachTo: document.body,
  })
}

const AMOUNT_DIALOG = '[data-test="amount-paid-dialog"]'

function confirmButton(): HTMLButtonElement {
  return document.querySelector('[data-test="amount-paid-dialog"] [data-test="confirm-in-cash"]') as HTMLButtonElement
}

function buttonTexts(): string[] {
  return Array.from(document.querySelectorAll('[data-test="amount-paid-dialog"] [data-test="actions"] button')).map(
    (button) => button.textContent?.trim() ?? '',
  )
}

describe('the dialog that asks what the table handed over', () => {
  afterEach(() => {
    document.body.innerHTML = ''
  })

  it('offers the selection as the amount, written the way a German waiter types it', async () => {
    mountDialog('de')
    await flushPromises()

    expect(inputOf(`${AMOUNT_DIALOG} [data-test="amount-field"]`).value).toBe('7,00')
  })

  it('offers the same amount with a dot to an English waiter', async () => {
    mountDialog('en')
    await flushPromises()

    expect(inputOf(`${AMOUNT_DIALOG} [data-test="amount-field"]`).value).toBe('7.00')
  })

  it('names what the selection comes to, so the full price is on screen', async () => {
    mountDialog('de')
    await flushPromises()

    expect(document.querySelector('[data-test="amount-paid-dialog"] [data-test="selected-total"]')?.textContent).toContain(
      'Ausgewählt: 7,00 €',
    )
  })

  it('hands back the amount in cents and the reason beside it', async () => {
    const dialog = mountDialog('de')
    await flushPromises()

    await typeIn(`${AMOUNT_DIALOG} [data-test="amount-field"]`, '2,50')
    await typeIn(`${AMOUNT_DIALOG} [data-test="reason-field"]`, 'Stammgast')
    confirmButton().click()
    await flushPromises()

    expect(dialog.emitted('confirm')).toEqual([[250, 'Stammgast', 'cash']])
  })

  it('hands back card when the waiter settles by card', async () => {
    const dialog = mountDialog('de')
    await flushPromises()

    ;(document.querySelector('[data-test="amount-paid-dialog"] [data-test="confirm-by-card"]') as HTMLElement).click()
    await flushPromises()

    expect(dialog.emitted('confirm')).toEqual([[700, null, 'card']])
  })

  it('offers cash and card while the typed amount is above zero', async () => {
    mountDialog('de')
    await flushPromises()

    expect(buttonTexts()).toEqual(['Bar abrechnen', 'Mit Karte abrechnen', 'Abbrechen'])
  })

  it('offers cash and card in English too', async () => {
    mountDialog('en')
    await flushPromises()

    expect(buttonTexts()).toEqual(['Settle in cash', 'Settle by card', 'Cancel'])
  })

  it('offers one settle button that says nothing was paid once the amount is zero', async () => {
    const dialog = mountDialog('de')
    await flushPromises()

    await typeIn(`${AMOUNT_DIALOG} [data-test="amount-field"]`, '0')
    await typeIn(`${AMOUNT_DIALOG} [data-test="reason-field"]`, 'Essen für die Kapelle')
    expect(buttonTexts()).toEqual(['Abrechnen', 'Abbrechen'])
    ;(document.querySelector('[data-test="amount-paid-dialog"] [data-test="confirm-nothing-paid"]') as HTMLElement).click()
    await flushPromises()

    expect(dialog.emitted('confirm')).toEqual([[0, 'Essen für die Kapelle', 'none']])
  })

  it('offers cash and card again when the waiter types an amount back in', async () => {
    mountDialog('de')
    await flushPromises()

    await typeIn(`${AMOUNT_DIALOG} [data-test="amount-field"]`, '0')
    await typeIn(`${AMOUNT_DIALOG} [data-test="amount-field"]`, '0,50')

    expect(buttonTexts()).toEqual(['Bar abrechnen', 'Mit Karte abrechnen', 'Abbrechen'])
  })

  it('keeps every button shut while a settlement is on its way', async () => {
    mount(AmountPaidDialog, {
      props: { isSettling: true, notice: null, selectedTotalCents: 700, language: 'de' },
      global: { plugins: testPlugins('de') },
      attachTo: document.body,
    })
    await flushPromises()

    const shut = Array.from(document.querySelectorAll('[data-test="amount-paid-dialog"] [data-test="actions"] button')).map(
      (button) => button.hasAttribute('disabled'),
    )
    expect(shut).toEqual([true, true, true])
  })

  it('hands back no reason when the table paid the full price', async () => {
    const dialog = mountDialog('de')
    await flushPromises()

    confirmButton().click()
    await flushPromises()

    expect(dialog.emitted('confirm')).toEqual([[700, null, 'cash']])
  })

  it('hands back no reason when the guest rounded up', async () => {
    const dialog = mountDialog('de')
    await flushPromises()

    await typeIn(`${AMOUNT_DIALOG} [data-test="amount-field"]`, '10,00')
    confirmButton().click()
    await flushPromises()

    expect(dialog.emitted('confirm')).toEqual([[1000, null, 'cash']])
  })

  it('keeps the confirming button shut while the field stands empty', async () => {
    const dialog = mountDialog('de')
    await flushPromises()

    await typeIn(`${AMOUNT_DIALOG} [data-test="amount-field"]`, '')
    confirmButton().click()
    await flushPromises()

    expect(confirmButton().hasAttribute('disabled')).toBe(true)
    expect(dialog.emitted('confirm')).toBeUndefined()
  })

  it('keeps a keystroke that can still grow into an amount', async () => {
    mountDialog('de')
    await flushPromises()

    await typeIn(`${AMOUNT_DIALOG} [data-test="amount-field"]`, '12,5')

    expect(inputOf(`${AMOUNT_DIALOG} [data-test="amount-field"]`).value).toBe('12,5')
  })

  it('refuses a keystroke that can never be part of an amount', async () => {
    mountDialog('de')
    await flushPromises()

    await typeIn(`${AMOUNT_DIALOG} [data-test="amount-field"]`, '12,50')
    await typeIn(`${AMOUNT_DIALOG} [data-test="amount-field"]`, '12,50€')

    expect(inputOf(`${AMOUNT_DIALOG} [data-test="amount-field"]`).value).toBe('12,50')
  })

  it('refuses a third decimal, so the field never holds an amount nobody can hand over', async () => {
    mountDialog('de')
    await flushPromises()

    await typeIn(`${AMOUNT_DIALOG} [data-test="amount-field"]`, '12,50')
    await typeIn(`${AMOUNT_DIALOG} [data-test="amount-field"]`, '12,509')

    expect(inputOf(`${AMOUNT_DIALOG} [data-test="amount-field"]`).value).toBe('12,50')
  })

  it('keeps the confirming button shut while a short amount carries no reason', async () => {
    const dialog = mountDialog('de')
    await flushPromises()

    await typeIn(`${AMOUNT_DIALOG} [data-test="amount-field"]`, '2,50')
    confirmButton().click()
    await flushPromises()

    expect(confirmButton().hasAttribute('disabled')).toBe(true)
    expect(dialog.emitted('confirm')).toBeUndefined()
  })

  it('shows no reason field until the amount falls short of the price', async () => {
    mountDialog('de')
    await flushPromises()

    expect(document.querySelector('[data-test="amount-paid-dialog"] [data-test="reason-field"]')).toBeNull()

    await typeIn(`${AMOUNT_DIALOG} [data-test="amount-field"]`, '2,50')

    expect(document.querySelector('[data-test="amount-paid-dialog"] [data-test="reason-field"]')).not.toBeNull()
  })

  it('says that the waiter backed out and hands back nothing', async () => {
    const dialog = mountDialog('de')
    await flushPromises()

    ;(document.querySelector('[data-test="amount-paid-dialog"] [data-test="cancel"]') as HTMLElement).click()
    await flushPromises()

    expect(dialog.emitted('cancel')).toHaveLength(1)
    expect(dialog.emitted('confirm')).toBeUndefined()
  })
})

describe('the dialog on a phone whose keyboard covers the lower screen', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
    document.body.innerHTML = ''
  })

  it('keeps the amount field and the actions above the keyboard', async () => {
    vi.stubGlobal('innerHeight', 800)
    vi.stubGlobal('visualViewport', {
      height: 400,
      scale: 1,
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
    })
    mountDialog('de')
    await flushPromises()

    const card = document.querySelector('[data-test="amount-paid-dialog"] [data-test="card"]') as HTMLElement
    expect(card.style.paddingBottom).toBe('400px')
  })
})
