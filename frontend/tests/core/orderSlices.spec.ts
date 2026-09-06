import { describe, expect, it } from 'vitest'
import { deliveryModesOf, orderSlices } from '../../src/core/orderSlices'

const BRATWURST = {
  name: 'Bratwurst',
  stationId: 'station-kueche',
  candidateStationIds: ['station-kueche'],
}
const POMMES = {
  name: 'Pommes',
  stationId: 'station-kueche',
  candidateStationIds: ['station-kueche'],
}
const BIER = {
  name: 'Bier',
  stationId: 'station-theke',
  candidateStationIds: ['station-theke'],
}
const NOWHERE_YET = {
  name: 'Schnitzel',
  stationId: null,
  candidateStationIds: ['station-kueche', 'station-grill'],
}

describe('orderSlices, the part of an order each station sees', () => {
  it('puts everything that goes to one station into one slice', () => {
    const slices = orderSlices([BRATWURST, POMMES])

    expect(slices).toHaveLength(1)
    expect(slices[0].stationId).toBe('station-kueche')
    expect(slices[0].lines.map((line) => line.name)).toEqual(['Bratwurst', 'Pommes'])
  })

  it('keeps the stations apart', () => {
    const slices = orderSlices([BRATWURST, BIER])

    expect(slices.map((slice) => slice.stationId)).toEqual(['station-kueche', 'station-theke'])
  })

  it('follows the order the items were chosen in, so the screen does not jump about', () => {
    const slices = orderSlices([BIER, BRATWURST, POMMES])

    expect(slices.map((slice) => slice.stationId)).toEqual(['station-theke', 'station-kueche'])
  })

  it('gathers the lines that have no station yet into a slice of their own', () => {
    const slices = orderSlices([BRATWURST, NOWHERE_YET])

    expect(slices.map((slice) => slice.stationId)).toEqual(['station-kueche', null])
  })
})

describe('deliveryModesOf, what the phone tells the laptop about each station', () => {
  it('sends together for a station the server never touched', () => {
    expect(deliveryModesOf(orderSlices([BRATWURST]), {})).toEqual([
      { stationId: 'station-kueche', deliveryMode: 'together' },
    ])
  })

  it('sends the choice the server made', () => {
    expect(
      deliveryModesOf(orderSlices([BRATWURST, BIER]), { 'station-theke': 'asItComes' }),
    ).toEqual([
      { stationId: 'station-kueche', deliveryMode: 'together' },
      { stationId: 'station-theke', deliveryMode: 'asItComes' },
    ])
  })

  it('says nothing about lines that have no station yet', () => {
    expect(deliveryModesOf(orderSlices([NOWHERE_YET]), {})).toEqual([])
  })

  it('ignores a choice for a station that is not on this order any more', () => {
    expect(deliveryModesOf(orderSlices([BRATWURST]), { 'station-theke': 'asItComes' })).toEqual([
      { stationId: 'station-kueche', deliveryMode: 'together' },
    ])
  })
})
