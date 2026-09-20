import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import { testPlugins } from '../../../support/plugins'
import SendFailurePanel from '../../../../src/phone/components/review/SendFailurePanel.vue'

function mountPanel(failure: { key: string }) {
  return mount(SendFailurePanel, { props: { failure }, global: { plugins: testPlugins('de') } })
}

describe('SendFailurePanel', () => {
  it('tells the server the laptop was not reached and points at the retry', () => {
    const panel = mountPanel({ key: 'review.sendFailed' })

    expect(panel.get('.failure-message').text()).toBe(
      'Der Rechner war nicht erreichbar. Tippen Sie auf "Erneut senden".',
    )
  })

  it('names the saving problem without pointing at a button that is not on the screen', () => {
    const panel = mountPanel({ key: 'review.sendFailedDatabase' })

    expect(panel.get('.failure-message').text()).toBe(
      'Der Rechner konnte die Bestellung nicht speichern. Senden Sie sie noch einmal.',
    )
  })
})
