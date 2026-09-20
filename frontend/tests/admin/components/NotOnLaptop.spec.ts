import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import NotOnLaptop from '../../../src/admin/components/NotOnLaptop.vue'
import { testPlugins } from '../../support/plugins'

function mountNotice(locale: 'de' | 'en' = 'de') {
  return mount(NotOnLaptop, { global: { plugins: testPlugins(locale) } })
}

describe('the admin pages opened somewhere other than the laptop', () => {
  it('names the laptop as the only place for the admin pages, in German', () => {
    const notice = mountNotice()

    expect(notice.get('.not-on-laptop').text()).toBe(
      'Die Verwaltung lässt sich nur am Rechner öffnen.',
    )
  })

  it('names the laptop as the only place for the admin pages, in English', () => {
    const notice = mountNotice('en')

    expect(notice.get('.not-on-laptop').text()).toBe(
      'The admin pages are only available on the computer.',
    )
  })
})
