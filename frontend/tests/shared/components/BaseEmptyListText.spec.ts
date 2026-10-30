import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import BaseEmptyListText from '../../../src/shared/components/BaseEmptyListText.vue'
import { testPlugins } from '../../support/plugins'

describe('the text of a list that has no entries', () => {
  it('says Keine Einträge in German', () => {
    const text = mount(BaseEmptyListText, { global: { plugins: testPlugins('de') } })

    expect(text.get('[data-test="empty-list-text"]').text()).toBe('Keine Einträge')
  })

  it('says No entries in English', () => {
    const text = mount(BaseEmptyListText, { global: { plugins: testPlugins('en') } })

    expect(text.get('[data-test="empty-list-text"]').text()).toBe('No entries')
  })

  it('takes the test name of the screen that shows it', () => {
    const text = mount(BaseEmptyListText, {
      attrs: { 'data-test': 'no-orders' },
      global: { plugins: testPlugins('de') },
    })

    expect(text.get('[data-test="no-orders"]').text()).toBe('Keine Einträge')
  })
})
