import { beforeEach, describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import BaseConfirmDialog from '../../../src/shared/components/BaseConfirmDialog.vue'
import { dialogText, testPlugins, waitForDialog } from '../../support/plugins'

function mountDialog() {
  return mount(BaseConfirmDialog, {
    props: {
      title: 'Ausgabestelle abschalten?',
      body: 'Die bisherigen Bestellungen bleiben gespeichert.',
      confirmLabel: 'Ausgabestelle abschalten',
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

    expect(dialogText('[data-test="confirm-title"]')).toBe('Ausgabestelle abschalten?')
  })

  it('says what it means for the data that is already there', async () => {
    mountDialog()
    await waitForDialog()

    expect(dialogText('[data-test="confirm-body"]')).toBe('Die bisherigen Bestellungen bleiben gespeichert.')
  })

  it('names the action on the button that carries it out', async () => {
    mountDialog()
    await waitForDialog()

    expect(dialogText('[data-test="confirm"]')).toBe('Ausgabestelle abschalten')
  })

  it('offers a way out that is not the action', async () => {
    mountDialog()
    await waitForDialog()

    expect(dialogText('[data-test="cancel"]')).toBe('Abbrechen')
  })

  it('reports the confirmation only when the action button is pressed', async () => {
    const dialog = mountDialog()
    await waitForDialog()

    ;(document.querySelector('[data-test="confirm"]') as HTMLElement).click()

    expect(dialog.emitted('confirm')).toHaveLength(1)
    expect(dialog.emitted('cancel')).toBeUndefined()
  })

  it('reports a cancellation when the way out is taken', async () => {
    const dialog = mountDialog()
    await waitForDialog()

    ;(document.querySelector('[data-test="cancel"]') as HTMLElement).click()

    expect(dialog.emitted('cancel')).toHaveLength(1)
    expect(dialog.emitted('confirm')).toBeUndefined()
  })
})
