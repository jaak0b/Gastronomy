import { describe, expect, it } from 'vitest'
import {
  CHART_THEMES,
  DARK_CHART_COLOURS,
  LIGHT_CHART_COLOURS,
} from '../../../src/admin/core/chartTheme'

function chartThemeNamed(name: string) {
  return CHART_THEMES.find((chartTheme) => chartTheme.name === name)?.theme
}

describe('the colours a chart takes from the app theme', () => {
  it('are the tokens of the light theme in light mode', () => {
    expect(LIGHT_CHART_COLOURS).toEqual({
      data: '#0d9488',
      ink: '#1f2328',
      muted: 'rgba(31, 35, 40, 0.71)',
      warning: '#875a00',
    })
  })

  it('are the tokens of the dark theme in dark mode', () => {
    expect(DARK_CHART_COLOURS).toEqual({
      data: '#00a897',
      ink: '#e6e8eb',
      muted: 'rgba(230, 232, 235, 0.66)',
      warning: '#ffd54f',
    })
  })
})

describe('the chart themes registered with ECharts', () => {
  it('are one light and one dark theme', () => {
    expect(CHART_THEMES.map((chartTheme) => chartTheme.name)).toEqual(['admin-light', 'admin-dark'])
  })

  it('draw a series in the data colour with muted axis text on the light surface', () => {
    expect(chartThemeNamed('admin-light')).toMatchObject({
      color: ['#0d9488'],
      timeAxis: { axisLabel: { color: 'rgba(31, 35, 40, 0.71)' } },
      tooltip: { backgroundColor: '#ffffff', borderColor: '#e1e4e8' },
    })
  })

  it('draw a series in the data colour with muted axis text on the dark surface', () => {
    expect(chartThemeNamed('admin-dark')).toMatchObject({
      color: ['#00a897'],
      timeAxis: { axisLabel: { color: 'rgba(230, 232, 235, 0.66)' } },
      tooltip: { backgroundColor: '#252a32', borderColor: '#343a44' },
    })
  })
})
