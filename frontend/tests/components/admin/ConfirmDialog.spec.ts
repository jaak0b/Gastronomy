import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import { createI18n } from 'vue-i18n'
import ConfirmDialog from '../../../src/components/admin/ConfirmDialog.vue'
import de from '../../../src/locales/de.json'
import en from '../../../src/locales/en.json'

function mountDialog() {
  const i18n = createI18n({ legacy: false, locale: 'de', messages: { de, en } })
  return mount(ConfirmDialog, {
    props: {
      title: 'Station abschalten?',
      body: 'Die bisherigen Bestellungen bleiben gespeichert.',
      confirmLabel: 'Station abschalten',
    },
    global: { plugins: [i18n] },
  })
}

describe('the question asked before something is switched off', () => {
  it('states what is about to happen', () => {
    const dialog = mountDialog()

    expect(dialog.get('.confirm-title').text()).toBe('Station abschalten?')
  })

  it('says what it means for the data that is already there', () => {
    const dialog = mountDialog()

    expect(dialog.get('.confirm-body').text()).toBe(
      'Die bisherigen Bestellungen bleiben gespeichert.',
    )
  })

  it('names the action on the button that carries it out', () => {
    const dialog = mountDialog()

    expect(dialog.get('.confirm').text()).toBe('Station abschalten')
  })

  it('offers a way out that is not the action', () => {
    const dialog = mountDialog()

    expect(dialog.get('.cancel').text()).toBe('Abbrechen')
  })

  it('reports the confirmation only when the action button is pressed', async () => {
    const dialog = mountDialog()

    await dialog.get('.confirm').trigger('click')

    expect(dialog.emitted('confirm')).toHaveLength(1)
    expect(dialog.emitted('cancel')).toBeUndefined()
  })

  it('reports a cancellation when the way out is taken', async () => {
    const dialog = mountDialog()

    await dialog.get('.cancel').trigger('click')

    expect(dialog.emitted('cancel')).toHaveLength(1)
    expect(dialog.emitted('confirm')).toBeUndefined()
  })

  it('is announced to a screen reader as a question that needs an answer', () => {
    const dialog = mountDialog()

    expect(dialog.get('.confirm-dialog').attributes('role')).toBe('dialog')
    expect(dialog.get('.confirm-dialog').attributes('aria-modal')).toBe('true')
  })
})
