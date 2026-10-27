import type { ThemeDefinition } from 'vuetify'
import type { ShareState } from '../api/generatedSchemas'
import { assertNever } from './assertNever'
import { channelsOfHex } from './colourChannels'
import { themeColourOf } from './themeColours'

export type StatusToken = 'error' | 'warning' | 'success'

export const STATUS_TINT_PERCENT = 30

export function statusTokenOf(state: ShareState): StatusToken {
  switch (state) {
    case 'none':
      return 'error'
    case 'some':
      return 'warning'
    case 'all':
      return 'success'
    default:
      return assertNever(state)
  }
}

export function statusTintOf(definition: ThemeDefinition, token: StatusToken): string {
  const status = channelsOfHex(themeColourOf(definition, token))
  const surface = channelsOfHex(themeColourOf(definition, 'surface'))
  const mixed = status.map((channel, index) =>
    Math.round((channel * STATUS_TINT_PERCENT + surface[index] * (100 - STATUS_TINT_PERCENT)) / 100),
  )
  return `#${mixed.map((channel) => channel.toString(16).padStart(2, '0')).join('')}`
}
