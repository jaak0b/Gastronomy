import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import NotOnLaptop from '../../../src/components/admin/NotOnLaptop.vue'
import { testPlugins } from '../../support/plugins'

function mountNotice(locale: 'de' | 'en' = 'de') {
  return mount(NotOnLaptop, { global: { plugins: testPlugins(locale) } })
}

describe('the admin pages opened somewhere other than the laptop', () => {
  it('sends the reader to the button in the program window, in German', () => {
    const notice = mountNotice()

    expect(notice.get('.not-on-laptop').text()).toBe(
      'Klicken Sie am Laptop im Fenster "Bestellsystem" auf "Verwaltung öffnen". Auf dem Telefon lässt sich die Verwaltung nicht öffnen.',
    )
  })

  it('sends the reader to the button in the program window, in English', () => {
    const notice = mountNotice('en')

    expect(notice.get('.not-on-laptop').text()).toBe(
      'Press "Open the admin pages" in the Ordering system window on the laptop. The admin pages do not open on a phone.',
    )
  })
})
