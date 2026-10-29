import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import BaseEmptyListText from '../../../src/admin/components/BaseEmptyListText.vue'

describe('the text of an admin list that has no entries', () => {
  it('shows the text its list gives it', () => {
    const text = mount(BaseEmptyListText, { props: { text: 'Keine Feste vorhanden' } })

    expect(text.get('[data-test="none-yet"]').text()).toBe('Keine Feste vorhanden')
  })
})
