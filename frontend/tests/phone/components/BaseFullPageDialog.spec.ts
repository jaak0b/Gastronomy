import { afterEach, describe, expect, it } from 'vitest'
import { enableAutoUnmount, flushPromises, mount } from '@vue/test-utils'
import BaseFullPageDialog from '../../../src/phone/components/BaseFullPageDialog.vue'
import { testPlugins } from '../../support/plugins'

enableAutoUnmount(afterEach)

describe('a page that covers the whole phone screen', () => {
  afterEach(() => {
    document.body.innerHTML = ''
  })

  it('shows its title, its content and its buttons below the content', async () => {
    mount(BaseFullPageDialog, {
      props: { title: 'Geben Sie den bezahlten Betrag ein' },
      slots: {
        default: '<p data-test="content">Ausgewählt: 7,00 €</p>',
        actions: '<button data-test="action">Abbrechen</button>',
      },
      attrs: { 'data-test': 'full-page' },
      global: { plugins: testPlugins('de') },
      attachTo: document.body,
    })
    await flushPromises()

    expect({
      title: document.querySelector('[data-test="full-page"] [data-test="page-title"]')?.textContent,
      content: document.querySelector('[data-test="full-page"] [data-test="card"] [data-test="content"]')?.textContent,
      action: document.querySelector('[data-test="full-page"] [data-test="actions"] [data-test="action"]')?.textContent,
    }).toEqual({
      title: 'Geben Sie den bezahlten Betrag ein',
      content: 'Ausgewählt: 7,00 €',
      action: 'Abbrechen',
    })
  })
})
