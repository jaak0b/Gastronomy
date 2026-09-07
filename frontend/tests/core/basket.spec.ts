import { beforeEach, describe, expect, it } from 'vitest'
import {
  basketItemCount,
  buildBasketView,
  lineCannotBeOrdered,
  refreshLineSnapshots,
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
        unitPriceCents: 350,
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
        candidateStationIds: ['station-kueche'],
        isSoldOut: false,
        isNoLongerOnTheMenu: false,
      },
    ])
  })

  it('shows the price the laptop carries now rather than the one the line was added at', () => {
    const draft = draftWith([
      {
        catalogItemId: 'item-bratwurst',
        note: null,
        stationId: null,
        name: 'Bratwurst',
        unitPriceCents: 300,
      },
    ])

    const view = buildBasketView(draft, catalog())

    expect(view[0].unitPriceCents).toBe(350)
  })

  it('flags a line whose item sold out while the basket was open', () => {
    const draft = draftWith([
      {
        catalogItemId: 'item-bier',
        note: null,
        stationId: null,
        name: 'Bier',
        unitPriceCents: 420,
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
        unitPriceCents: 400,
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

  it('keeps the price it was added at', () => {
    const view = buildBasketView(draftWithVanishedItem(), catalog())

    expect(view[0].unitPriceCents).toBe(400)
  })

  it('is marked as no longer on the menu', () => {
    const view = buildBasketView(draftWithVanishedItem(), catalog())

    expect(view[0].isNoLongerOnTheMenu).toBe(true)
  })

  it('offers no station choice, because nothing says where it is prepared any more', () => {
    const view = buildBasketView(draftWithVanishedItem(), catalog())

    expect(view[0].candidateStationIds).toEqual([])
  })

  it('still counts towards the total the server reads out loud', () => {
    const view = buildBasketView(draftWithVanishedItem(), catalog())

    expect(orderTotalCents(view)).toBe(400)
  })

  it('carries no name at all when the draft predates the stored name', () => {
    const draft = draftWith([
      {
        catalogItemId: 'item-gone',
        note: null,
        stationId: null,
        name: '',
        unitPriceCents: 0,
      },
    ])

    const view = buildBasketView(draft, catalog())

    expect(view[0].name).toBe('')
    expect(view[0].isNoLongerOnTheMenu).toBe(true)
  })
})

describe('refreshLineSnapshots', () => {
  beforeEach(() => {
    localStorage.clear()
  })

  it('follows a rename the laptop pushed while the item is still on the menu', () => {
    const draft = draftWith([
      {
        catalogItemId: 'item-bratwurst',
        note: null,
        stationId: null,
        name: 'Bratwurst alt',
        unitPriceCents: 350,
      },
    ])

    const refreshed = refreshLineSnapshots(draft, catalog())

    expect(refreshed.lines[0].name).toBe('Bratwurst')
  })

  it('follows a price change the laptop pushed while the item is still on the menu', () => {
    const draft = draftWith([
      {
        catalogItemId: 'item-bratwurst',
        note: null,
        stationId: null,
        name: 'Bratwurst',
        unitPriceCents: 300,
      },
    ])

    const refreshed = refreshLineSnapshots(draft, catalog())

    expect(refreshed.lines[0].unitPriceCents).toBe(350)
  })

  it('leaves the snapshot of a vanished item exactly as it was', () => {
    const draft = draftWith([
      {
        catalogItemId: 'item-gone',
        note: null,
        stationId: null,
        name: 'Currywurst',
        unitPriceCents: 400,
      },
    ])

    const refreshed = refreshLineSnapshots(draft, catalog())

    expect(refreshed.lines[0]).toEqual({
      catalogItemId: 'item-gone',
      note: null,
      stationId: null,
      name: 'Currywurst',
      unitPriceCents: 400,
    })
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
        unitPriceCents: 350,
      },
      {
        catalogItemId: 'item-gone',
        note: 'ohne Zwiebeln',
        stationId: null,
        name: 'Currywurst',
        unitPriceCents: 400,
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
        unitPriceCents: 350,
      },
      {
        catalogItemId: 'item-bier',
        note: null,
        stationId: null,
        name: 'Bier',
        unitPriceCents: 420,
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
        unitPriceCents: 350,
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
        unitPriceCents: 350,
      },
    ])

    const remaining = withoutLinesThatCannotBeOrdered(draft, catalog())

    expect(remaining.lines).toHaveLength(1)
  })
})

describe('lineCannotBeOrdered', () => {
  function shownLineOf(catalogItemId: string, name: string, unitPriceCents: number) {
    return buildBasketView(
      draftWith([{ catalogItemId, note: null, stationId: null, name, unitPriceCents }]),
      catalog(),
    )[0]
  }

  it('lets a line through while its item is on the menu and in stock', () => {
    expect(lineCannotBeOrdered(shownLineOf('item-bratwurst', 'Bratwurst', 350))).toBe(false)
  })

  it('holds a line back once its item has sold out', () => {
    expect(lineCannotBeOrdered(shownLineOf('item-bier', 'Bier', 420))).toBe(true)
  })

  it('holds a line back once the laptop no longer has its item at all', () => {
    expect(lineCannotBeOrdered(shownLineOf('item-gone', 'Currywurst', 400))).toBe(true)
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
        unitPriceCents: 350,
      },
      {
        catalogItemId: 'item-bratwurst',
        note: null,
        stationId: null,
        name: 'Bratwurst',
        unitPriceCents: 350,
      },
      {
        catalogItemId: 'item-bier',
        note: null,
        stationId: null,
        name: 'Bier',
        unitPriceCents: 420,
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
        unitPriceCents: 400,
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
