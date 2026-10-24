import { describe, expect, it } from 'vitest'
import { mutedInkOf, themeColourOf, themeVariableOf } from '../../../src/shared/core/themeColours'
import { appTheme } from '../../../src/shared/theme'

describe('reading the app theme', () => {
  it('gives the colour of a token', () => {
    expect(themeColourOf(appTheme.themes.light, 'surface-light')).toBe('#ebedf0')
  })

  it('refuses a colour the theme does not have, so a typo cannot draw in a default colour', () => {
    expect(() => themeColourOf(appTheme.themes.light, 'tableFill')).toThrow(
      'The app theme has no colour named tableFill.',
    )
  })

  it('refuses a variable the theme does not have', () => {
    expect(() => themeVariableOf(appTheme.themes.dark, 'table-opacity')).toThrow(
      'The app theme has no variable named table-opacity.',
    )
  })

  it('gives the text colour at medium emphasis as the muted ink', () => {
    expect(mutedInkOf(appTheme.themes.dark)).toBe('rgba(230, 232, 235, 0.66)')
  })
})
