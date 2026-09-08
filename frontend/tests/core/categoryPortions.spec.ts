import { describe, expect, it } from 'vitest'
import { portionsOfCategory } from '../../src/core/categoryPortions'
import type { CatalogItem, DraftLine, DraftOrder } from '../../src/core/apiTypes'

const MAIN_COURSE = 'category-hauptspeise'
const DRINKS = 'category-getraenke'

function item(id: string, name: string, categoryId: string): CatalogItem {
  return {
    id,
    name,
    categoryId,
    priceCents: 400,
    sortOrder: 0,
    isAvailable: true,
    stationIds: ['station-1'],
    productionMinutes: null,
  }
}

const ITEMS: CatalogItem[] = [
  item('item-semmel', 'Semmel', MAIN_COURSE),
  item('item-gulasch', 'Gulasch', MAIN_COURSE),
  item('item-bier', 'Bier', DRINKS),
]

function line(catalogItemId: string, note: string | null = null): DraftLine {
  return { catalogItemId, note, stationId: 'station-1', name: '' }
}

function draftWith(lines: DraftLine[]): DraftOrder {
  return { tableName: '', note: null, lines, clientOrderId: null, deliveryModes: {} }
}

describe('portionsOfCategory', () => {
  it('counts every portion of an item, not the item once', () => {
    const draft = draftWith([line('item-semmel'), line('item-semmel')])

    expect(portionsOfCategory(draft, ITEMS, MAIN_COURSE)).toBe(2)
  })

  it('adds up the portions of the different items of one category', () => {
    const draft = draftWith([line('item-semmel'), line('item-semmel'), line('item-gulasch')])

    expect(portionsOfCategory(draft, ITEMS, MAIN_COURSE)).toBe(3)
  })

  it('counts a portion carrying a note as well', () => {
    const draft = draftWith([line('item-semmel'), line('item-semmel', 'ohne Senf')])

    expect(portionsOfCategory(draft, ITEMS, MAIN_COURSE)).toBe(2)
  })

  it('leaves the portions of another category out', () => {
    const draft = draftWith([line('item-semmel'), line('item-bier'), line('item-bier')])

    expect(portionsOfCategory(draft, ITEMS, DRINKS)).toBe(2)
  })

  it('counts nothing for a category with no portions on the order', () => {
    const draft = draftWith([line('item-bier')])

    expect(portionsOfCategory(draft, ITEMS, MAIN_COURSE)).toBe(0)
  })

  it('counts nothing on an order that is still empty', () => {
    expect(portionsOfCategory(draftWith([]), ITEMS, MAIN_COURSE)).toBe(0)
  })

  it('ignores a portion of an item that has left the catalogue', () => {
    const draft = draftWith([line('item-semmel'), line('item-that-is-gone')])

    expect(portionsOfCategory(draft, ITEMS, MAIN_COURSE)).toBe(1)
  })

  it('counts nothing for two categories that only share a name, never an id', () => {
    const draft = draftWith([line('item-bier')])

    expect(portionsOfCategory(draft, ITEMS, 'Getränke')).toBe(0)
  })
})
