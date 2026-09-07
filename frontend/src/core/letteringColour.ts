const SIX_DIGIT_HEX = /^#[0-9a-fA-F]{6}$/
const DARK_LETTERING = '#000000'
const LIGHT_LETTERING = '#FFFFFF'
const BRIGHTNESS_THAT_STILL_CARRIES_DARK_LETTERING = 0.179

function brightnessOfChannel(component: number): number {
  const share = component / 255
  return share <= 0.03928 ? share / 12.92 : ((share + 0.055) / 1.055) ** 2.4
}

function channelAt(colourHex: string, position: number): number {
  return brightnessOfChannel(Number.parseInt(colourHex.slice(position, position + 2), 16))
}

export function letteringColourOn(colourHex: string): string {
  if (!SIX_DIGIT_HEX.test(colourHex)) {
    return DARK_LETTERING
  }
  const brightness =
    0.2126 * channelAt(colourHex, 1)
    + 0.7152 * channelAt(colourHex, 3)
    + 0.0722 * channelAt(colourHex, 5)
  return brightness > BRIGHTNESS_THAT_STILL_CARRIES_DARK_LETTERING
    ? DARK_LETTERING
    : LIGHT_LETTERING
}
