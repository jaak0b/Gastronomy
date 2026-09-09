import { beforeEach, describe, expect, it } from 'vitest'
import {
  basketItemCount,
  buildBasketView,
  lineCannotBeOrdered,
  withStationNamesTheCatalogStillKnows,
  withoutLinesThatCannotBeOrdered,
} from '../../src/core/basket'
import { orderTotalCents } from '../../src/core/totals'
import { DRAFT_STORAGE_KEY } from '../../src/core/draftCart'
import type { Catalog, DraftOrder } from '../../src/core/apiTypes'

function catalog(): Catalog {
  return {
    categories: [
      { categoryId: 'category-essen', name: 'Essen', colourHex: '#FFEB3B', sortOrder: 1 },
    ],
    items: [
      {
        id: 'item-bratwurst',
        name: 'Bratwurst',
        categoryId: 'category-essen',
        priceCents: 350,
        sortOrder: 1,
        isAvailable: true,
        stationIds: ['station-kueche'],
      },
      {
        id: 'item-bier',
        name: 'Bier',
        categoryId: 'category-getraenke',
        priceCents: 420,
        sortOrder: 2,
        isAvailable: false,
        stationIds: ['station-theke-innen', 'station-theke-aussen'],
      },
    ],
    stations: [
      { id: 'station-kueche', name: 'Kueche', sortOrder: 1 },
      { id: 'station-theke-innen', name: 'Theke innen', sortOrder: 2 },
      { id: 'station-theke-aussen', name: 'Theke aussen', sortOrder: 3 },
    ],
  }
}

function draftWith(lines: DraftOrder['lines']): DraftOrder {
  return { tableName: '', note: null, lines, clientOrderId: null }
}

describe('buildBasketView', () => {
  it('names the item and its price', () => {
    const draft = draftWith([
      {
        catalogItemId: 'item-bratwurst',
        note: null,
        stationId: null,
        name: 'Bratwurst',
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
      },
    ])

    const view = buildBasketView(draft, catalog())

    expect(view[0].isSoldOut).toBe(true)
  })
})

describe('a line whose item was taken off the menu while the basket was open', () => {
  function draftWithVanishedItem(): DraftOrder {
    return draftWith([
      {
        catalogItemId: 'item-gone',
        note: 'ohne Zwiebeln',
        stationId: null,
        name: 'Currywurst',
      },
    ])
  }

  it('stays on the screen instead of being silently deleted', () => {
    const view = buildBasketView(draftWithVanishedItem(), catalog())

    expect(view).toHaveLength(1)
  })

  it('keeps the name the guest ordered it by', () => {
    const view = buildBasketView(draftWithVanishedItem(), catalog())

    expect(view[0].name).toBe('Currywurst')
  })

  it('carries no price, because the laptop no longer names one for it', () => {
    const view = buildBasketView(draftWithVanishedItem(), catalog())

    expect(view[0].unitPriceCents).toBeNull()
  })

  it('is marked as no longer on the menu', () => {
    const view = buildBasketView(draftWithVanishedItem(), catalog())

    expect(view[0].isNoLongerOnTheMenu).toBe(true)
  })

  it('offers no station choice, because nothing says where it is prepared any more', () => {
    const view = buildBasketView(draftWithVanishedItem(), catalog())

    expect(view[0].candidateStationIds).toEqual([])
  })

  it('counts nothing towards the total, so the total matches what the laptop would record', () => {
    const view = buildBasketView(draftWithVanishedItem(), catalog())

    expect(orderTotalCents(view)).toBe(0)
  })

  it('carries no name at all when the draft predates the stored name', () => {
    const draft = draftWith([
      {
        catalogItemId: 'item-gone',
        note: null,
        stationId: null,
        name: '',
      },
    ])

    const view = buildBasketView(draft, catalog())

    expect(view[0].name).toBe('')
    expect(view[0].isNoLongerOnTheMenu).toBe(true)
  })
})

describe('withoutLinesThatCannotBeOrdered', () => {
  beforeEach(() => {
    localStorage.clear()
  })

  function draftWithOneVanishedItem(): DraftOrder {
    return draftWith([
      {
        catalogItemId: 'item-bratwurst',
        note: null,
        stationId: null,
        name: 'Bratwurst',
      },
      {
        catalogItemId: 'item-gone',
        note: 'ohne Zwiebeln',
        stationId: null,
        name: 'Currywurst',
      },
    ])
  }

  it('takes out the line the laptop no longer knows, so the order can be sent', () => {
    const remaining = withoutLinesThatCannotBeOrdered(draftWithOneVanishedItem(), catalog())

    expect(remaining.lines.map((line) => line.catalogItemId)).toEqual(['item-bratwurst'])
  })

  it('takes out a line whose item has sold out, because that one blocks the send too', () => {
    const draft = draftWith([
      {
        catalogItemId: 'item-bratwurst',
        note: null,
        stationId: null,
        name: 'Bratwurst',
      },
      {
        catalogItemId: 'item-bier',
        note: null,
        stationId: null,
        name: 'Bier',
      },
    ])

    const remaining = withoutLinesThatCannotBeOrdered(draft, catalog())

    expect(remaining.lines.map((line) => line.catalogItemId)).toEqual(['item-bratwurst'])
  })

  it('keeps the table and the note the server has already typed', () => {
    const draft = { ...draftWithOneVanishedItem(), tableName: 'Tisch 12', note: 'schnell bitte' }

    const remaining = withoutLinesThatCannotBeOrdered(draft, catalog())

    expect(remaining.tableName).toBe('Tisch 12')
    expect(remaining.note).toBe('schnell bitte')
  })

  it('writes the shortened order to storage, so a reload does not bring the line back', () => {
    withoutLinesThatCannotBeOrdered(draftWithOneVanishedItem(), catalog())

    const stored = JSON.parse(localStorage.getItem(DRAFT_STORAGE_KEY) ?? 'null')

    expect(stored.lines).toEqual([
      {
        catalogItemId: 'item-bratwurst',
        note: null,
        stationId: null,
        name: 'Bratwurst',
      },
    ])
  })

  it('changes nothing when every item on the order can still be ordered', () => {
    const draft = draftWith([
      {
        catalogItemId: 'item-bratwurst',
        note: null,
        stationId: null,
        name: 'Bratwurst',
      },
    ])

    const remaining = withoutLinesThatCannotBeOrdered(draft, catalog())

    expect(remaining.lines).toHaveLength(1)
  })
})

describe('withStationNamesTheCatalogStillKnows', () => {
  beforeEach(() => {
    localStorage.clear()
  })

  function beerRoutedTo(stationId: string | null, stationName: string): DraftOrder {
    return draftWith([
      { catalogItemId: 'item-bier', note: null, stationId, name: 'Bier', stationName },
    ])
  }

  it('writes down the name the catalog carries for a line that remembers none', () => {
    const remembered = withStationNamesTheCatalogStillKnows(
      beerRoutedTo('station-theke-innen', ''),
      catalog(),
    )

    expect(remembered.lines[0].stationName).toBe('Theke innen')
  })

  it('writes down the one station of a line nobody was ever asked about', () => {
    const draft = draftWith([
      { catalogItemId: 'item-bratwurst', note: null, stationId: null, name: 'Bratwurst', stationName: '' },
    ])

    const remembered = withStationNamesTheCatalogStillKnows(draft, catalog())

    expect(remembered.lines[0].stationName).toBe('Kueche')
  })

  it('leaves a line unnamed when the catalog no longer carries its station either', () => {
    const remembered = withStationNamesTheCatalogStillKnows(
      beerRoutedTo('station-abgebaut', ''),
      catalog(),
    )

    expect(remembered.lines[0].stationName).toBe('')
  })

  it('keeps the name a line wrote down when its station was chosen', () => {
    const remembered = withStationNamesTheCatalogStillKnows(
      beerRoutedTo('station-theke-innen', 'Theke drinnen'),
      catalog(),
    )

    expect(remembered.lines[0].stationName).toBe('Theke drinnen')
  })

  it('writes the filled name to storage, so the next reload starts with it', () => {
    withStationNamesTheCatalogStillKnows(beerRoutedTo('station-theke-innen', ''), catalog())

    const stored = JSON.parse(localStorage.getItem(DRAFT_STORAGE_KEY) ?? 'null')

    expect(stored.lines[0].stationName).toBe('Theke innen')
  })

  it('touches nothing when every line already knows the name of its station', () => {
    const draft = beerRoutedTo('station-theke-innen', 'Theke innen')

    const remembered = withStationNamesTheCatalogStillKnows(draft, catalog())

    expect(remembered).toBe(draft)
    expect(localStorage.getItem(DRAFT_STORAGE_KEY)).toBeNull()
  })
})

describe('lineCannotBeOrdered', () => {
  function shownLineOf(catalogItemId: string, name: string) {
    return buildBasketView(
      draftWith([{ catalogItemId, note: null, stationId: null, name }]),
      catalog(),
    )[0]
  }

  it('lets a line through while its item is on the menu and in stock', () => {
    expect(lineCannotBeOrdered(shownLineOf('item-bratwurst', 'Bratwurst'))).toBe(false)
  })

  it('holds a line back once its item has sold out', () => {
    expect(lineCannotBeOrdered(shownLineOf('item-bier', 'Bier'))).toBe(true)
  })

  it('holds a line back once the laptop no longer has its item at all', () => {
    expect(lineCannotBeOrdered(shownLineOf('item-gone', 'Currywurst'))).toBe(true)
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
      },
      {
        catalogItemId: 'item-bratwurst',
        note: null,
        stationId: null,
        name: 'Bratwurst',
      },
      {
        catalogItemId: 'item-bier',
        note: null,
        stationId: null,
        name: 'Bier',
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

describe('a line whose station no longer prepares its item', () => {
  function draftWithAMovedItem(): DraftOrder {
    return draftWith([
      {
        catalogItemId: 'item-bratwurst',
        note: null,
        stationId: 'station-theke-innen',
        name: 'Bratwurst',
      },
    ])
  }

  it('is flagged, because the station on the line no longer makes the item', () => {
    const view = buildBasketView(draftWithAMovedItem(), catalog())

    expect(view[0].isNoLongerPreparedAtItsStation).toBe(true)
  })

  it('holds the send back, so the laptop is never asked to refuse it', () => {
    const view = buildBasketView(draftWithAMovedItem(), catalog())

    expect(lineCannotBeOrdered(view[0])).toBe(true)
  })

  it('leaves the order with the other lines that cannot be ordered', () => {
    const remaining = withoutLinesThatCannotBeOrdered(draftWithAMovedItem(), catalog())

    expect(remaining.lines).toEqual([])
  })

  it('is not flagged while the station on the line still makes the item', () => {
    const draft = draftWith([
      {
        catalogItemId: 'item-bratwurst',
        note: null,
        stationId: 'station-kueche',
        name: 'Bratwurst',
      },
    ])

    expect(buildBasketView(draft, catalog())[0].isNoLongerPreparedAtItsStation).toBe(false)
  })

  it('is not flagged on a line that carries no station of its own', () => {
    const draft = draftWith([
      {
        catalogItemId: 'item-bratwurst',
        note: null,
        stationId: null,
        name: 'Bratwurst',
      },
    ])

    expect(buildBasketView(draft, catalog())[0].isNoLongerPreparedAtItsStation).toBe(false)
  })

  it('is not flagged on a line whose item left the menu, which the line already says', () => {
    const draft = draftWith([
      {
        catalogItemId: 'item-gone',
        note: null,
        stationId: 'station-kueche',
        name: 'Currywurst',
      },
    ])

    expect(buildBasketView(draft, catalog())[0].isNoLongerPreparedAtItsStation).toBe(false)
  })
})

describe('the station a line names on the summary', () => {
  function withoutTheOutdoorBar(): Catalog {
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
