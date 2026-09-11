import { describe, expect, it } from 'vitest'
import { appTheme } from '../src/theme'

describe('the palette the whole app inherits', () => {
  it('lets the device decide between the two themes', () => {
    expect(appTheme.defaultTheme).toBe('system')
  })

  it('offers a light and a dark theme', () => {
    expect(Object.keys(appTheme.themes)).toEqual(['light', 'dark'])
    expect(appTheme.themes.light.dark).toBe(false)
    expect(appTheme.themes.dark.dark).toBe(true)
  })
})
