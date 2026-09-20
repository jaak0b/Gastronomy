import { describe, expect, it } from 'vitest'
import {
  laptopConfirmedField,
  stillDiffersFromTheLaptop,
} from '../../../src/admin/core/laptopConfirmedField'

function namesTheSameStations(left: string[], right: string[]): boolean {
  return left.length === right.length && left.every((stationId) => right.includes(stationId))
}

describe('a field held against what the laptop confirmed', () => {
  it('starts out agreeing with the laptop', () => {
    const price = laptopConfirmedField('3,50', '3,50')

    expect(stillDiffersFromTheLaptop(price)).toBe(false)
  })

  it('differs as soon as somebody types something else', () => {
    const price = laptopConfirmedField('3,50', '3,50')

    price.edited = '4,00'

    expect(stillDiffersFromTheLaptop(price)).toBe(true)
  })

  it('agrees again once the laptop has confirmed what was typed', () => {
    const price = laptopConfirmedField('4,00', '3,50')

    price.confirmedByTheLaptop = '4,00'

    expect(stillDiffersFromTheLaptop(price)).toBe(false)
  })

  it('takes the comparison it is given, so a reordered list still counts as the same', () => {
    const stations = laptopConfirmedField(['bar', 'kitchen'], ['kitchen', 'bar'])

    expect(stillDiffersFromTheLaptop(stations, namesTheSameStations)).toBe(false)
    expect(stillDiffersFromTheLaptop(stations)).toBe(true)
  })

  it('reports a list with another entry as different', () => {
    const stations = laptopConfirmedField(['bar', 'kitchen'], ['bar'])

    expect(stillDiffersFromTheLaptop(stations, namesTheSameStations)).toBe(true)
  })
})
