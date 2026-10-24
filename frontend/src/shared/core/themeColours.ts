import type { ThemeDefinition } from 'vuetify'
import { channelsOfHex } from './colourChannels'

export function themeColourOf(definition: ThemeDefinition, token: string): string {
  const colour = definition.colors?.[token]
  if (typeof colour !== 'string') {
    throw new Error(`The app theme has no colour named ${token}.`)
  }
  return colour
}

export function themeVariableOf(definition: ThemeDefinition, name: string): string | number {
  const value = definition.variables?.[name]
  if (value === undefined) {
    throw new Error(`The app theme has no variable named ${name}.`)
  }
  return value
}

function translucentColour(hexColour: string, opacity: number): string {
  return `rgba(${channelsOfHex(hexColour).join(', ')}, ${opacity})`
}

export function mutedInkOf(definition: ThemeDefinition): string {
  return translucentColour(
    themeColourOf(definition, 'on-surface'),
    Number(themeVariableOf(definition, 'medium-emphasis-opacity')),
  )
}

