import { beforeEach, describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import ConfirmDialog from '../../../src/components/admin/ConfirmDialog.vue'
import { dialogText, testPlugins, waitForDialog } from '../../support/plugins'

function mountDialog() {
  return mount(ConfirmDialog, {
    props: {
      title: 'Station abschalten?',
      body: 'Die bisherigen Bestellungen bleiben gespeichert.',
      confirmLabel: 'Station abschalten',
    },
    global: { plugins: testPlugins() },
    attachTo: document.body,
  })
}

describe('the question asked before something is switched off', () => {
  beforeEach(() => {
    document.body.innerHTML = ''
  })

  it('states what is about to happen', async () => {
    mountDialog()
    await waitForDialog()

    expect(dialogText('.confirm-title')).toBe('Station abschalten?')
  })

  it('says what it means for the data that is already there', async () => {
    mountDialog()
    await waitForDialog()

    expect(dialogText('.confirm-body')).toBe('Die bisherigen Bestellungen bleiben gespeichert.')
  })

  it('names the action on the button that carries it out', async () => {
    mountDialog()
    await waitForDialog()

    expect(dialogText('.confirm')).toBe('Station abschalten')
  })

  it('offers a way out that is not the action', async () => {
    mountDialog()
    await waitForDialog()

    expect(dialogText('.cancel')).toBe('Abbrechen')
  })

  it('reports the confirmation only when the action button is pressed', async () => {
    const dialog = mountDialog()
    await waitForDialog()

    ;(document.querySelector('.confirm') as HTMLElement).click()

    expect(dialog.emitted('confirm')).toHaveLength(1)
    expect(dialog.emitted('cancel')).toBeUndefined()
  })

  it('reports a cancellation when the way out is taken', async () => {
    const dialog = mountDialog()
    await waitForDialog()

    ;(document.querySelector('.cancel') as HTMLElement).click()

    expect(dialog.emitted('cancel')).toHaveLength(1)
    expect(dialog.emitted('confirm')).toBeUndefined()
  })

  it('is announced to a screen reader as a question that needs an answer', async () => {
    mountDialog()
    await waitForDialog()

    expect(document.querySelector('.confirm-dialog')!.getAttribute('role')).toBe('dialog')
    expect(document.querySelector('.confirm-dialog')!.getAttribute('aria-modal')).toBe('true')
  })
})
