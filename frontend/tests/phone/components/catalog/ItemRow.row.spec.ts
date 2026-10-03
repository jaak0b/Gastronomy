import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { enableAutoUnmount, mount } from '@vue/test-utils'
import { testPlugins } from '../../../support/plugins'
import ItemRow from '../../../../src/phone/components/catalog/ItemRow.vue'
import { CatalogItemView } from '../../../../src/shared/api/generatedSchemas'
import type { ItemPosition } from '../../../../src/phone/core/itemPositions'
import { plain, noted, stationNameFor, mountRow, schnitzel, at } from './itemRowFixture'

enableAutoUnmount(afterEach)

beforeEach(() => {
  document.body.innerHTML = ''
})

describe('the item row itself', () => {
  it('names the item and its price', () => {
    const row = mountRow(true, [])

    expect(row.get('[data-test="name"]').text()).toBe('Wasser')
    expect(row.get('[data-test="unit-price"]').text()).toBe('2,00 €')
  })

  it('adds one of the item and opens nothing when the name is tapped', async () => {
    const row = mountRow(true, [])

    await row.get('[data-test="add"]').trigger('click')

    expect(row.emitted('add')).toHaveLength(1)
    expect(document.querySelector('[data-test="note-dialog"]')).toBeNull()
  })

  it('puts the plain portions and the noted portion each on a row of their own', () => {
    const row = mountRow(true, [plain(0), plain(1), noted(2, 'ohne Eis')])

    const rows = row.findAll('[data-test="note-group"]')
    expect(rows).toHaveLength(2)
    const plainRows = rows.filter((entry) => !entry.find('[data-test="group-note"]').exists())
    expect(plainRows.map((entry) => entry.get('[data-test="group-count"]').text())).toEqual(['2'])
    expect(plainRows.map((entry) => entry.get('[data-test="group-label"]').text())).toEqual(['Theke'])
  })

  it('takes the most recently added plain portion off again', async () => {
    const row = mountRow(true, [plain(0), plain(4)])

    await row.get('[data-test="note-group"] [data-test="group-remove"]').trigger('click')

    expect(row.emitted('removeOne')).toEqual([[4]])
  })

  it('cannot be tapped once the item has sold out', () => {
    const row = mountRow(false, [])

    expect(row.get('[data-test="add"]').attributes('disabled')).toBeDefined()
    expect(row.get('[data-test="add-note"]').attributes('disabled')).toBeDefined()
  })

  it('says that a sold out item is sold out', () => {
    const row = mountRow(false, [])

    expect(row.get('[data-test="sold-out"]').text()).toBe('Ausverkauft')
  })
})

describe('the note control on an article with nothing ordered yet', () => {
  it('sits inside the header and renders no rows area', () => {
    const row = mountRow(true, [])

    expect(row.get('[data-test="item-head"]').find('[data-test="add-note"]').exists()).toBe(true)
    expect(row.find('[data-test="note-group"]').exists()).toBe(false)
  })
})

describe('the note button placement once rows are on the order', () => {
  it('stays in the header for Schnitzel, an item with several rows underneath', () => {
    const row = mount(ItemRow, {
      props: {
        item: schnitzel(),
        positions: [at(0, 'station-schank', 'Schank'), at(1, 'station-kueche', 'Küche')],
        language: 'de' as const,
        estimateRange: null,
        stationNameFor,
      },
      global: { plugins: testPlugins('de') },
      attachTo: document.body,
    })

    expect(row.get('[data-test="item-head"]').find('[data-test="add-note"]').exists()).toBe(true)
  })
})

describe('the default station shown on a plain row', () => {
  function wurstel(): CatalogItemView {
    return {
      id: 'item-wurstel',
      name: 'Würstel',
      categoryId: 'category-essen',
      priceCents: 250,
      sortOrder: 1,
      isAvailable: true,
      stationIds: ['station-bar'],
      productionMinutes: null,
      isQueueIndependent: false,
    }
  }

  it('names the station the plain-only item is routed to, on its one row', () => {
    const positions: ItemPosition[] = Array.from({ length: 15 }, (_unused, index) => ({
      index,
      note: null,
      hasAStationChoice: false,
      stationId: 'station-bar',
      stationName: null,
    }))
    const row = mount(ItemRow, {
      props: {
        item: wurstel(),
        positions,
        language: 'de' as const,
        estimateRange: null,
        stationNameFor,
      },
      global: { plugins: testPlugins('de') },
      attachTo: document.body,
    })

    expect(row.findAll('[data-test="note-group"]')).toHaveLength(1)
    const onlyRow = row.get('[data-test="note-group"]')
    expect(onlyRow.get('[data-test="group-count"]').text()).toBe('15')
    expect(onlyRow.get('[data-test="group-station-fixed"]').text()).toBe('Theke')
    expect(onlyRow.find('[data-test="group-remove"]').exists()).toBe(true)
    expect(onlyRow.find('[data-test="group-add"]').exists()).toBe(true)
  })
})
