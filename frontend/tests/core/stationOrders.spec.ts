import { describe, expect, it } from 'vitest'
import { buildStationDeliveryModes, buildStationOrders } from '../../src/core/stationOrders'

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

describe('buildStationOrders, the part of an order each station sees', () => {
  it('puts everything that goes to one station into one station order', () => {
    const stationOrders = buildStationOrders([BRATWURST, POMMES])

    expect(stationOrders).toHaveLength(1)
    expect(stationOrders[0].stationId).toBe('station-kueche')
    expect(stationOrders[0].lines.map((line) => line.name)).toEqual(['Bratwurst', 'Pommes'])
  })

  it('keeps the stations apart', () => {
    const stationOrders = buildStationOrders([BRATWURST, BIER])

    expect(stationOrders.map((stationOrder) => stationOrder.stationId)).toEqual([
      'station-kueche',
      'station-theke',
    ])
  })

  it('follows the order the items were chosen in, so the screen does not jump about', () => {
    const stationOrders = buildStationOrders([BIER, BRATWURST, POMMES])

    expect(stationOrders.map((stationOrder) => stationOrder.stationId)).toEqual([
      'station-theke',
      'station-kueche',
    ])
  })

  it('gathers the lines that have no station yet into a station order of their own', () => {
    const stationOrders = buildStationOrders([BRATWURST, NOWHERE_YET])

    expect(stationOrders.map((stationOrder) => stationOrder.stationId)).toEqual([
      'station-kueche',
      null,
    ])
  })
})

describe('buildStationDeliveryModes, what the phone tells the laptop about each station', () => {
  it('sends together for a station the server never touched', () => {
    expect(buildStationDeliveryModes(buildStationOrders([BRATWURST]), {})).toEqual([
      { stationId: 'station-kueche', deliveryMode: 'together' },
    ])
  })

  it('sends the choice the server made', () => {
    expect(
      buildStationDeliveryModes(buildStationOrders([BRATWURST, BIER]), {
        'station-theke': 'asItComes',
      }),
    ).toEqual([
      { stationId: 'station-kueche', deliveryMode: 'together' },
      { stationId: 'station-theke', deliveryMode: 'asItComes' },
    ])
  })

  it('says nothing about lines that have no station yet', () => {
    expect(buildStationDeliveryModes(buildStationOrders([NOWHERE_YET]), {})).toEqual([])
  })

  it('ignores a choice for a station that is not on this order any more', () => {
    expect(
      buildStationDeliveryModes(buildStationOrders([BRATWURST]), {
        'station-theke': 'asItComes',
      }),
    ).toEqual([{ stationId: 'station-kueche', deliveryMode: 'together' }])
  })
})
