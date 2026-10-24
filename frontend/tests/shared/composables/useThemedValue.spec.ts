import { describe, expect, it } from 'vitest'
import { defineComponent, h } from 'vue'
import { flushPromises, mount } from '@vue/test-utils'
import { useTheme } from 'vuetify'
import { useThemedValue } from '../../../src/shared/composables/useThemedValue'

const ThemedValueReader = defineComponent({
  setup() {
    const appTheme = useTheme()
    const ink = useThemedValue('#1f2328', '#e6e8eb')
    return () =>
      h('div', [
        h('span', { 'data-test': 'ink' }, ink.value),
        h('button', { 'data-test': 'switch-to-dark', onClick: () => appTheme.change('dark') }),
      ])
  },
})

describe('a value that follows the theme of the device', () => {
  it('is the light value while the app is light', () => {
    const reader = mount(ThemedValueReader)

    expect(reader.get('[data-test="ink"]').text()).toBe('#1f2328')
  })

  it('becomes the dark value when the app switches to dark', async () => {
    const reader = mount(ThemedValueReader)

    await reader.get('[data-test="switch-to-dark"]').trigger('click')
    await flushPromises()

    expect(reader.get('[data-test="ink"]').text()).toBe('#e6e8eb')
  })
})
