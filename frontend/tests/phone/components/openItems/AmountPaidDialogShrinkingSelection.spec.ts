import { afterEach, describe, expect, it } from 'vitest'
import { enableAutoUnmount, flushPromises, mount } from '@vue/test-utils'
import AmountPaidDialog from '../../../../src/phone/components/openItems/AmountPaidDialog.vue'
import { testPlugins } from '../../../support/plugins'

enableAutoUnmount(afterEach)

function mountDialog(selectedTotalCents: number) {
  return mount(AmountPaidDialog, {
    props: { isSettling: false, notice: null, selectedTotalCents, language: 'de' as const },
    global: { plugins: testPlugins('de') },
    attachTo: document.body,
  })
}

function amountField(): HTMLInputElement {
  return document.querySelector('.amount-paid-dialog .amount-field input') as HTMLInputElement
}

function confirmButton(): HTMLButtonElement {
  return document.querySelector('.amount-paid-dialog .confirm') as HTMLButtonElement
}

describe('the amount dialog when another phone settles part of the selection', () => {
  afterEach(() => {
    document.body.innerHTML = ''
  })

  it('offers the shrunk selection as the amount, not the amount the table no longer owes', async () => {
    const dialog = mountDialog(1000)
    await flushPromises()

    await dialog.setProps({ selectedTotalCents: 700 })
    await flushPromises()

    expect(amountField().value).toBe('7,00')
  })

  it('hands back what the shrunk selection comes to when the waiter confirms untouched', async () => {
    const dialog = mountDialog(1000)
    await flushPromises()

    await dialog.setProps({ selectedTotalCents: 700 })
    await flushPromises()
    confirmButton().click()
    await flushPromises()

    expect(dialog.emitted('confirm')).toEqual([[700, null]])
  })

  it('keeps what the waiter typed when the selection moves under it', async () => {
    const dialog = mountDialog(1000)
    await flushPromises()

    const field = amountField()
    field.value = '12,00'
    field.dispatchEvent(new Event('input', { bubbles: true }))
    await flushPromises()

    await dialog.setProps({ selectedTotalCents: 700 })
    await flushPromises()

    expect(amountField().value).toBe('12,00')

    confirmButton().click()
    await flushPromises()

    expect(dialog.emitted('confirm')).toEqual([[1200, null]])
  })

  it('keeps the field the waiter emptied empty when the selection moves', async () => {
    const dialog = mountDialog(1000)
    await flushPromises()

    const field = amountField()
    field.value = ''
    field.dispatchEvent(new Event('input', { bubbles: true }))
    await flushPromises()

    await dialog.setProps({ selectedTotalCents: 700 })
    await flushPromises()

    expect(amountField().value).toBe('')
    expect(confirmButton().hasAttribute('disabled')).toBe(true)
  })
})
