import { describe, expect, it } from 'vitest'
import { appTheme } from '../../src/shared/theme'

describe('the palette the whole app inherits', () => {
  it('lets the device decide between the two themes', () => {
    expect(appTheme.defaultTheme).toBe('system')
  })

  it('darkens warning text in the light theme so it can be read on white, and keeps it in the dark theme', () => {
    expect(appTheme.themes.light.colors?.['warning-text']).toBe('#9a6700')
    expect(appTheme.themes.dark.colors?.['warning-text']).toBe('#ffd54f')
  })

  it('offers a light and a dark theme', () => {
    expect(Object.keys(appTheme.themes)).toEqual(['light', 'dark'])
    expect(appTheme.themes.light.dark).toBe(false)
    expect(appTheme.themes.dark.dark).toBe(true)
  })
})
