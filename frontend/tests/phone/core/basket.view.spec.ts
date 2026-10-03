import { describe, expect, it } from 'vitest'
import { basketItemCount, buildBasketView } from '../../../src/phone/core/basket'
import type { DraftOrder } from '../../../src/phone/core/draftCart'
import { CatalogView } from '../../../src/shared/api/generatedSchemas'
import { catalog, draftWith } from './basketFixture'

describe('buildBasketView', () => {
  it('names the item and its price', () => {
    const draft = draftWith([
      {
        catalogItemId: 'item-bratwurst',
        note: null,
        stationId: null,
        name: 'Bratwurst',
        stationName: '',
      },
    ])

    const view = buildBasketView(draft, catalog())

    expect(view).toEqual([
      {
        catalogItemId: 'item-bratwurst',
        name: 'Bratwurst',
        unitPriceCents: 350,
        note: null,
        stationId: null,
        stationName: 'Kueche',
        candidateStationIds: ['station-kueche'],
        isSoldOut: false,
        isNoLongerOnTheMenu: false,
        isNoLongerPreparedAtItsStation: false,
      },
    ])
  })

  it('flags a line whose item sold out while the basket was open', () => {
    const draft = draftWith([
      {
        catalogItemId: 'item-bier',
        note: null,
        stationId: null,
        name: 'Bier',
        stationName: '',
      },
    ])

    const view = buildBasketView(draft, catalog())

    expect(view[0].isSoldOut).toBe(true)
  })
})

describe('basketItemCount', () => {
  it('counts every position in the basket', () => {
    const draft = draftWith([
      {
        catalogItemId: 'item-bratwurst',
        note: null,
        stationId: null,
        name: 'Bratwurst',
        stationName: '',
      },
      {
        catalogItemId: 'item-bratwurst',
        note: null,
        stationId: null,
        name: 'Bratwurst',
        stationName: '',
      },
      {
        catalogItemId: 'item-bier',
        note: null,
        stationId: null,
        name: 'Bier',
        stationName: '',
      },
    ])

    const count = basketItemCount(draft)

    expect(count).toBe(3)
  })

  it('counts a position whose item was taken off the menu', () => {
    const draft = draftWith([
      {
        catalogItemId: 'item-gone',
        note: null,
        stationId: null,
        name: 'Currywurst',
        stationName: '',
      },
    ])

    const count = basketItemCount(draft)

    expect(count).toBe(1)
  })

  it('counts nothing in an empty basket', () => {
    const count = basketItemCount(draftWith([]))

    expect(count).toBe(0)
  })
})

describe('the station a line names on the summary', () => {
  function withoutTheOutdoorBar(): CatalogView {
    const menu = catalog()
    return {
      ...menu,
      stations: menu.stations.filter((station) => station.id !== 'station-theke-aussen'),
    }
  }

  function beerAtTheOutdoorBar(): DraftOrder {
    return draftWith([
      {
        catalogItemId: 'item-bier',
        note: null,
        stationId: 'station-theke-aussen',
        name: 'Bier',
        stationName: 'Theke aussen',
      },
    ])
  }

  it('takes the name from the item list, so a station renamed during the evening reads new', () => {
    const view = buildBasketView(beerAtTheOutdoorBar(), catalog())

    expect(view[0].stationName).toBe('Theke aussen')
  })

  it('takes the name the line kept once the station has left the item list', () => {
    const view = buildBasketView(beerAtTheOutdoorBar(), withoutTheOutdoorBar())

    expect(view[0].stationName).toBe('Theke aussen')
  })

  it('holds the line back once its station has left the item list', () => {
    const menu = withoutTheOutdoorBar()
    const beerInStock = {
      ...menu,
      items: menu.items.map((item) => ({ ...item, isAvailable: true })),
    }

    const view = buildBasketView(beerAtTheOutdoorBar(), beerInStock)

    expect(view[0].isNoLongerPreparedAtItsStation).toBe(true)
  })

  it('names the one station that prepares an item the waiter was never asked about', () => {
    const draft = draftWith([
      {
        catalogItemId: 'item-bratwurst',
        note: null,
        stationId: null,
        name: 'Bratwurst',
        stationName: '',
      },
    ])

    const view = buildBasketView(draft, catalog())

    expect(view[0].stationName).toBe('Kueche')
  })
})
