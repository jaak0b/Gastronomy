import { afterEach, describe, expect, it } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import AmountPaidDialog from '../../../src/components/openItems/AmountPaidDialog.vue'
import type { AppLanguage } from '../../../src/core/apiTypes'
import { testPlugins } from '../../support/plugins'

function mountDialog(language: AppLanguage = 'de', selectedTotalCents = 700) {
  return mount(AmountPaidDialog, {
    props: { isSettling: false, notice: null, selectedTotalCents, language },
    global: { plugins: testPlugins(language) },
    attachTo: document.body,
  })
}

function fieldIn(selector: string): HTMLInputElement {
  return document.querySelector(`.amount-paid-dialog ${selector} input`) as HTMLInputElement
}

async function typeIn(selector: string, typed: string): Promise<void> {
  const field = fieldIn(selector)
  field.value = typed
  field.dispatchEvent(new Event('input', { bubbles: true }))
  await flushPromises()
}

function confirmButton(): HTMLButtonElement {
  return document.querySelector('.amount-paid-dialog .confirm') as HTMLButtonElement
}

describe('the dialog that asks what the table handed over', () => {
  afterEach(() => {
    document.body.innerHTML = ''
  })

  it('offers the selection as the amount, written the way a German waiter types it', async () => {
    mountDialog('de')
    await flushPromises()

    expect(fieldIn('.amount-field').value).toBe('7,00')
  })

  it('offers the same amount with a dot to an English waiter', async () => {
    mountDialog('en')
    await flushPromises()

    expect(fieldIn('.amount-field').value).toBe('7.00')
  })

  it('names what the selection comes to, so the full price is on screen', async () => {
    mountDialog('de')
    await flushPromises()

    expect(document.querySelector('.amount-paid-dialog .selected-total')?.textContent).toContain(
      'Ausgewählt: 7,00 €',
    )
  })

  it('hands back the amount in cents and the reason beside it', async () => {
    const dialog = mountDialog('de')
    await flushPromises()

    await typeIn('.amount-field', '2,50')
    await typeIn('.reason-field', 'Stammgast')
    confirmButton().click()
    await flushPromises()

    expect(dialog.emitted('confirm')).toEqual([[250, 'Stammgast']])
  })

  it('hands back no reason when the table paid the full price', async () => {
    const dialog = mountDialog('de')
    await flushPromises()

    confirmButton().click()
    await flushPromises()

    expect(dialog.emitted('confirm')).toEqual([[700, null]])
  })

  it('hands back no reason when the guest rounded up', async () => {
    const dialog = mountDialog('de')
    await flushPromises()

    await typeIn('.amount-field', '10,00')
    confirmButton().click()
    await flushPromises()

    expect(dialog.emitted('confirm')).toEqual([[1000, null]])
  })

  it('keeps the confirming button shut while the field stands empty', async () => {
    const dialog = mountDialog('de')
    await flushPromises()

    await typeIn('.amount-field', '')
    confirmButton().click()
    await flushPromises()

    expect(confirmButton().hasAttribute('disabled')).toBe(true)
    expect(dialog.emitted('confirm')).toBeUndefined()
  })

  it('keeps a keystroke that can still grow into an amount', async () => {
    mountDialog('de')
    await flushPromises()

    await typeIn('.amount-field', '12,5')

    expect(fieldIn('.amount-field').value).toBe('12,5')
  })

  it('refuses a keystroke that can never be part of an amount', async () => {
    mountDialog('de')
    await flushPromises()

    await typeIn('.amount-field', '12,50')
    await typeIn('.amount-field', '12,50€')

    expect(fieldIn('.amount-field').value).toBe('12,50')
  })

  it('refuses a third decimal, so the field never holds an amount nobody can hand over', async () => {
    mountDialog('de')
    await flushPromises()

    await typeIn('.amount-field', '12,50')
    await typeIn('.amount-field', '12,509')

    expect(fieldIn('.amount-field').value).toBe('12,50')
  })

  it('keeps the confirming button shut while a short amount carries no reason', async () => {
    const dialog = mountDialog('de')
    await flushPromises()

    await typeIn('.amount-field', '2,50')
    confirmButton().click()
    await flushPromises()

    expect(confirmButton().hasAttribute('disabled')).toBe(true)
    expect(dialog.emitted('confirm')).toBeUndefined()
  })

  it('shows no reason field until the amount falls short of the price', async () => {
    mountDialog('de')
    await flushPromises()

    expect(document.querySelector('.amount-paid-dialog .reason-field')).toBeNull()

    await typeIn('.amount-field', '2,50')

    expect(document.querySelector('.amount-paid-dialog .reason-field')).not.toBeNull()
  })

  it('says that the waiter backed out and hands back nothing', async () => {
    const dialog = mountDialog('de')
    await flushPromises()

    ;(document.querySelector('.amount-paid-dialog .cancel') as HTMLElement).click()
    await flushPromises()

    expect(dialog.emitted('cancel')).toHaveLength(1)
    expect(dialog.emitted('confirm')).toBeUndefined()
  })
})
