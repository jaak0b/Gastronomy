import type { ThemeDefinition } from 'vuetify'
import { channelsOfHex } from '../../shared/core/colourChannels'
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

function colourOf(definition: ThemeDefinition, token: string): string {
  const colour = definition.colors?.[token]
  if (typeof colour !== 'string') {
    throw new Error(`The app theme has no colour named ${token}.`)
  }
  return colour
}

function variableOf(definition: ThemeDefinition, name: string): string | number {
  const value = definition.variables?.[name]
  if (value === undefined) {
    throw new Error(`The app theme has no variable named ${name}.`)
  }
  return value
}

function translucent(hexColour: string, opacity: number): string {
  return `rgba(${channelsOfHex(hexColour).join(', ')}, ${opacity})`
}

function chartColoursOf(definition: ThemeDefinition): ChartColours {
  const ink = colourOf(definition, 'on-surface')
  return {
    data: colourOf(definition, 'chartData'),
    ink,
    muted: translucent(ink, Number(variableOf(definition, 'medium-emphasis-opacity'))),
    warning: colourOf(definition, 'warning'),
  }
}

export const LIGHT_CHART_COLOURS = chartColoursOf(appTheme.themes.light)

export const DARK_CHART_COLOURS = chartColoursOf(appTheme.themes.dark)

function chartThemeOf(definition: ThemeDefinition, colours: ChartColours): Record<string, unknown> {
  const surface = colourOf(definition, 'surface')
  const hairline = String(variableOf(definition, 'border-color'))
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
