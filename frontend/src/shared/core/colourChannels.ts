export type ColourChannels = [number, number, number]

function channelAt(hexColour: string, position: number): number {
  return Number.parseInt(hexColour.slice(position, position + 2), 16)
}

export function channelsOfHex(hexColour: string): ColourChannels {
  return [channelAt(hexColour, 1), channelAt(hexColour, 3), channelAt(hexColour, 5)]
}
