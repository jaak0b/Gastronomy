import type { ThemeDefinition } from 'vuetify'
import { mutedInkOf, themeColourOf, themeVariableOf } from '../../shared/core/themeColours'
import { appTheme } from '../../shared/theme'

export interface ChartColours {
  data: string
  ink: string
  muted: string
  warning: string
}

export interface NamedChartTheme {
  name: string
  theme: Record<string, unknown>
}

export const LIGHT_CHART_THEME = 'admin-light'

export const DARK_CHART_THEME = 'admin-dark'

function chartColoursOf(definition: ThemeDefinition): ChartColours {
  return {
    data: themeColourOf(definition, 'chartData'),
    ink: themeColourOf(definition, 'on-surface'),
    muted: mutedInkOf(definition),
    warning: themeColourOf(definition, 'warning'),
  }
}

export const LIGHT_CHART_COLOURS = chartColoursOf(appTheme.themes.light)

export const DARK_CHART_COLOURS = chartColoursOf(appTheme.themes.dark)

function chartThemeOf(definition: ThemeDefinition, colours: ChartColours): Record<string, unknown> {
  const surface = themeColourOf(definition, 'surface')
  const hairline = String(themeVariableOf(definition, 'border-color'))
  const axis = {
    axisLine: { lineStyle: { color: hairline } },
    axisTick: { lineStyle: { color: hairline } },
    axisLabel: { color: colours.muted },
    splitLine: { lineStyle: { color: hairline, type: 'solid' } },
  }
  return {
    color: [colours.data],
    backgroundColor: 'transparent',
    textStyle: { color: colours.muted },
    legend: { textStyle: { color: colours.ink } },
    timeAxis: axis,
    valueAxis: axis,
    categoryAxis: axis,
    line: { lineStyle: { width: 2 } },
    markLine: { lineStyle: { color: colours.muted }, label: { color: colours.muted, textBorderWidth: 0 } },
    markPoint: { label: { color: colours.ink, textBorderWidth: 0 } },
    tooltip: {
      backgroundColor: surface,
      borderColor: hairline,
      textStyle: { color: colours.ink },
      axisPointer: { lineStyle: { color: colours.muted, width: 1 } },
    },
  }
}

export const CHART_THEMES: readonly NamedChartTheme[] = [
  { name: LIGHT_CHART_THEME, theme: chartThemeOf(appTheme.themes.light, LIGHT_CHART_COLOURS) },
  { name: DARK_CHART_THEME, theme: chartThemeOf(appTheme.themes.dark, DARK_CHART_COLOURS) },
]
