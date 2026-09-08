import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import { createI18n } from 'vue-i18n'
import SendFailurePanel from '../../../src/components/review/SendFailurePanel.vue'
import de from '../../../src/locales/de.json'
import en from '../../../src/locales/en.json'

function mountPanel(failure: { key: string }) {
  const i18n = createI18n({ legacy: false, locale: 'de', messages: { de, en } })
  return mount(SendFailurePanel, { props: { failure }, global: { plugins: [i18n] } })
}

describe('SendFailurePanel', () => {
  it('tells the server the laptop was not reached and the order is still here', () => {
    const panel = mountPanel({ key: 'review.sendFailed' })

    expect(panel.get('.failure-message').text()).toBe(
      'Tippen Sie auf "Erneut senden". Der Laptop war nicht erreichbar, die Bestellung steht noch vollständig hier.',
    )
  })

  it('names the saving problem when that is what went wrong', () => {
    const panel = mountPanel({ key: 'review.sendFailedDatabase' })

    expect(panel.get('.failure-message').text()).toBe(
      'Tippen Sie auf "Erneut senden". Der Laptop konnte die Bestellung gerade nicht speichern, sie steht aber noch vollständig hier.',
    )
  })
})
