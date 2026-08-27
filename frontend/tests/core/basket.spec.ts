import { beforeEach, describe, expect, it } from 'vitest'
import { basketItemCount, buildBasketView, refreshLineSnapshots } from '../../src/core/basket'
import { orderTotalCents } from '../../src/core/totals'
import type { Catalog, DraftOrder } from '../../src/core/apiTypes'

function catalog(): Catalog {
  return {
    version: '7',
    categories: [{ name: 'Essen', sortOrder: 1 }],
    items: [
      {
        id: 'item-bratwurst',
        name: 'Bratwurst',
        categoryName: 'Essen',
        priceCents: 350,
        sortOrder: 1,
        isAvailable: true,
        locationIds: ['location-kueche'],
      },
      {
        id: 'item-bier',
        name: 'Bier',
        categoryName: 'Getraenke',
        priceCents: 420,
        sortOrder: 2,
        isAvailable: false,
        locationIds: ['location-theke-innen', 'location-theke-aussen'],
      },
    ],
    locations: [
      { id: 'location-kueche', name: 'Kueche', sortOrder: 1 },
      { id: 'location-theke-innen', name: 'Theke innen', sortOrder: 2 },
      { id: 'location-theke-aussen', name: 'Theke aussen', sortOrder: 3 },
    ],
    tableSuggestions: [{ label: 'Tisch 1', sortOrder: 1 }],
  }
}

function draftWith(lines: DraftOrder['lines']): DraftOrder {
  return { tableLabel: '', note: null, lines, clientOrderId: null }
}

describe('buildBasketView', () => {
  it('names the item and its price', () => {
    const draft = draftWith([
      {
        catalogItemId: 'item-bratwurst',
        quantity: 2,
        note: null,
        productionLocationId: null,
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
        quantity: 2,
        note: null,
        productionLocationId: null,
        candidateLocationIds: ['location-kueche'],
        isSoldOut: false,
        isNoLongerOnTheMenu: false,
      },
    ])
  })

  it('shows the price the laptop carries now rather than the one the line was added at', () => {
    const draft = draftWith([
      {
        catalogItemId: 'item-bratwurst',
        quantity: 1,
        note: null,
        productionLocationId: null,
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
        quantity: 1,
        note: null,
        productionLocationId: null,
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
        quantity: 2,
        note: 'ohne Zwiebeln',
        productionLocationId: null,
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

    expect(view[0].candidateLocationIds).toEqual([])
  })

  it('still counts towards the total the server reads out loud', () => {
    const view = buildBasketView(draftWithVanishedItem(), catalog())

    expect(orderTotalCents(view)).toBe(800)
  })

  it('carries no name at all when the draft predates the stored name', () => {
    const draft = draftWith([
      {
        catalogItemId: 'item-gone',
        quantity: 1,
        note: null,
        productionLocationId: null,
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
        quantity: 1,
        note: null,
        productionLocationId: null,
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
        quantity: 1,
        note: null,
        productionLocationId: null,
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
        quantity: 1,
        note: null,
        productionLocationId: null,
        name: 'Currywurst',
        unitPriceCents: 400,
      },
    ])

    const refreshed = refreshLineSnapshots(draft, catalog())

    expect(refreshed.lines[0]).toEqual({
      catalogItemId: 'item-gone',
      quantity: 1,
      note: null,
      productionLocationId: null,
      name: 'Currywurst',
      unitPriceCents: 400,
    })
  })
})

describe('basketItemCount', () => {
  it('counts every article rather than every line', () => {
    const draft = draftWith([
      {
        catalogItemId: 'item-bratwurst',
        quantity: 2,
        note: null,
        productionLocationId: null,
        name: 'Bratwurst',
        unitPriceCents: 350,
      },
      {
        catalogItemId: 'item-bier',
        quantity: 3,
        note: null,
        productionLocationId: null,
        name: 'Bier',
        unitPriceCents: 420,
      },
    ])

    const count = basketItemCount(draft)

    expect(count).toBe(5)
  })

  it('counts a line whose item was taken off the menu', () => {
    const draft = draftWith([
      {
        catalogItemId: 'item-gone',
        quantity: 2,
        note: null,
        productionLocationId: null,
        name: 'Currywurst',
        unitPriceCents: 400,
      },
    ])

    const count = basketItemCount(draft)

    expect(count).toBe(2)
  })

  it('counts nothing in an empty basket', () => {
    const count = basketItemCount(draftWith([]))

    expect(count).toBe(0)
  })
})
