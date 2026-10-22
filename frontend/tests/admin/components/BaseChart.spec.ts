import { describe, expect, it, vi } from 'vitest'
import { defineComponent, h } from 'vue'
import { flushPromises, mount } from '@vue/test-utils'
import { useTheme } from 'vuetify'
import BaseChart from '../../../src/admin/components/BaseChart.vue'

vi.mock('vue-echarts', async () => {
  const vue = await import('vue')
  return {
    default: vue.defineComponent({
      name: 'EChartsStandIn',
      props: { option: Object, theme: String, initOptions: Object, autoresize: Boolean },
      setup: () => () => vue.h('div'),
    }),
  }
})

const OPTION = { series: [{ type: 'line', data: [[1, 2]] }] }

const ChartInAThemedApp = defineComponent({
  setup() {
    const appTheme = useTheme()
    return () =>
      h('div', [
        h('button', { 'data-test': 'switch-to-dark', onClick: () => appTheme.change('dark') }),
        h(BaseChart, { option: OPTION }),
      ])
  },
})

function chartDrawnBy(app: ReturnType<typeof mount>) {
  return app.getComponent({ name: 'EChartsStandIn' })
}

describe('the shared chart', () => {
  it('hands the option to ECharts unchanged', () => {
    const app = mount(ChartInAThemedApp)

    expect(chartDrawnBy(app).props('option')).toEqual(OPTION)
  })

  it('draws as SVG and follows the size of its container', () => {
    const app = mount(ChartInAThemedApp)

    expect(chartDrawnBy(app).props()).toMatchObject({ initOptions: { renderer: 'svg' }, autoresize: true })
  })

  it('takes the light chart theme while the app is light', () => {
    const app = mount(ChartInAThemedApp)

    expect(chartDrawnBy(app).props('theme')).toBe('admin-light')
  })

  it('switches to the dark chart theme with the app', async () => {
    const app = mount(ChartInAThemedApp)

    await app.get('[data-test="switch-to-dark"]').trigger('click')
    await flushPromises()

    expect(chartDrawnBy(app).props('theme')).toBe('admin-dark')
  })
})
