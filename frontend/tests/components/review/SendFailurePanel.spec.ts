import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import { createI18n } from 'vue-i18n'
import SendFailurePanel from '../../../src/components/review/SendFailurePanel.vue'
import de from '../../../src/locales/de.json'
import en from '../../../src/locales/en.json'

function mountPanel(failure: { key: string; paperFallbackKey: string | null }) {
  const i18n = createI18n({ legacy: false, locale: 'de', messages: { de, en } })
  return mount(SendFailurePanel, { props: { failure }, global: { plugins: [i18n] } })
}

describe('SendFailurePanel', () => {
  it('tells the server the laptop was not reached and the order is still here', () => {
    const panel = mountPanel({ key: 'review.sendFailed', paperFallbackKey: null })

    expect(panel.get('.failure-message').text()).toBe(
      'Tippen Sie auf "Erneut senden". Der Laptop war nicht erreichbar, die Bestellung steht noch vollständig hier.',
    )
  })

  it('says nothing about paper after a first failure', () => {
    const panel = mountPanel({ key: 'review.sendFailed', paperFallbackKey: null })

    expect(panel.find('.paper-fallback').exists()).toBe(false)
  })

  it('tells the server to write the order down after a second failure', () => {
    const panel = mountPanel({
      key: 'review.sendFailed',
      paperFallbackKey: 'review.sendFailedAgain',
    })

    expect(panel.get('.paper-fallback').text()).toBe(
      'Schreiben Sie die Bestellung auf Papier und bringen Sie sie zur Ausgabestelle. Das Senden hat mehrmals nicht geklappt.',
    )
  })

  it('names the saving problem when that is what went wrong', () => {
    const panel = mountPanel({ key: 'review.sendFailedDatabase', paperFallbackKey: null })

    expect(panel.get('.failure-message').text()).toBe(
      'Tippen Sie auf "Erneut senden". Der Laptop konnte die Bestellung gerade nicht speichern, sie steht aber noch vollständig hier.',
    )
  })
})
