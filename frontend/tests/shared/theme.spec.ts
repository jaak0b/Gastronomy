import { describe, expect, it } from 'vitest'
import { createVuetify } from 'vuetify'
import { channelsOfHex, type ColourChannels } from '../../src/shared/core/colourChannels'
import { appTheme } from '../../src/shared/theme'

const READABLE_TEXT_CONTRAST = 4.5
const GROUNDS = ['surface', 'background'] as const

const vuetify = createVuetify({ theme: appTheme })

function colourOf(themeName: 'light' | 'dark', token: string): ColourChannels {
  const hex = appTheme.themes[themeName].colors?.[token]
  if (typeof hex !== 'string') {
    throw new Error(`The ${themeName} theme has no colour named ${token}.`)
  }
  return channelsOfHex(hex)
}

function tonalOpacityOf(themeName: 'light' | 'dark'): number {
  const opacity = vuetify.theme.themes.value[themeName]?.variables['activated-opacity']
  if (typeof opacity !== 'number') {
    throw new Error(`Vuetify has no activated opacity for the ${themeName} theme.`)
  }
  return opacity
}

function linearShareOf(channel: number): number {
  const share = channel / 255
  return share <= 0.04045 ? share / 12.92 : ((share + 0.055) / 1.055) ** 2.4
}

function relativeLuminanceOf([red, green, blue]: ColourChannels): number {
  return 0.2126 * linearShareOf(red) + 0.7152 * linearShareOf(green) + 0.0722 * linearShareOf(blue)
}

function contrastRatio(first: ColourChannels, second: ColourChannels): number {
  const [lighter, darker] = [relativeLuminanceOf(first), relativeLuminanceOf(second)].sort(
    (left, right) => right - left,
  )
  return (lighter + 0.05) / (darker + 0.05)
}

function mixedChannel(tint: number, ground: number, opacity: number): number {
  return tint * opacity + ground * (1 - opacity)
}

function tintedOver(
  [tintRed, tintGreen, tintBlue]: ColourChannels,
  [groundRed, groundGreen, groundBlue]: ColourChannels,
  opacity: number,
): ColourChannels {
  return [
    mixedChannel(tintRed, groundRed, opacity),
    mixedChannel(tintGreen, groundGreen, opacity),
    mixedChannel(tintBlue, groundBlue, opacity),
  ]
}

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

describe('the warning colour that paints the text and the icon of every warning alert', () => {
  describe.each(['light', 'dark'] as const)('in the %s theme', (themeName) => {
    it.each(GROUNDS)('can be read as text on the %s', (ground) => {
      const contrast = contrastRatio(colourOf(themeName, 'warning'), colourOf(themeName, ground))

      expect(contrast).toBeGreaterThanOrEqual(READABLE_TEXT_CONTRAST)
    })

    it.each(GROUNDS)('can be read as text on a tonal alert lying on the %s', (ground) => {
      const warning = colourOf(themeName, 'warning')
      const tintedGround = tintedOver(warning, colourOf(themeName, ground), tonalOpacityOf(themeName))

      expect(contrastRatio(warning, tintedGround)).toBeGreaterThanOrEqual(READABLE_TEXT_CONTRAST)
    })
  })
})
