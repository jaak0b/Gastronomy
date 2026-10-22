import { describe, expect, it } from 'vitest'
import { defineComponent, h } from 'vue'
import { flushPromises, mount } from '@vue/test-utils'
import { useTheme } from 'vuetify'
import { useChartTheme } from '../../../src/admin/composables/useChartTheme'

const ChartThemeReader = defineComponent({
  setup() {
    const appTheme = useTheme()
    const chartTheme = useChartTheme()
    return () =>
      h('div', [
        h('span', { 'data-test': 'chart-theme-name' }, chartTheme.name.value),
        h('span', { 'data-test': 'chart-data-colour' }, chartTheme.colours.value.data),
        h('button', { 'data-test': 'switch-to-dark', onClick: () => appTheme.change('dark') }),
      ])
  },
})

describe('the chart theme of the device', () => {
  it('is the light chart theme while the app is light', () => {
    const reader = mount(ChartThemeReader)

    expect(reader.get('[data-test="chart-theme-name"]').text()).toBe('admin-light')
    expect(reader.get('[data-test="chart-data-colour"]').text()).toBe('#0d9488')
  })

  it('follows the app to the dark chart theme', async () => {
    const reader = mount(ChartThemeReader)

    await reader.get('[data-test="switch-to-dark"]').trigger('click')
    await flushPromises()

    expect(reader.get('[data-test="chart-theme-name"]').text()).toBe('admin-dark')
    expect(reader.get('[data-test="chart-data-colour"]').text()).toBe('#00a897')
  })
})
