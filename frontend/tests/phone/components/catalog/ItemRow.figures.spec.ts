import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { enableAutoUnmount, mount } from '@vue/test-utils'
import { testPlugins } from '../../../support/plugins'
import ItemRow from '../../../../src/phone/components/catalog/ItemRow.vue'
import { item, plain, stationNameFor, mountRow, mountRowWithEstimate, schnitzel, at } from './itemRowFixture'

enableAutoUnmount(afterEach)

beforeEach(() => {
  document.body.innerHTML = ''
})

describe('the waiting time written on an item row', () => {
  it('sits on the facts line under the name, the price beside the name', () => {
    const row = mountRowWithEstimate({ min: 6, max: 6 })

    expect(row.get('[data-test="name"]').text()).toBe('Wasser')
    expect(row.get('[data-test="unit-price"]').text()).toBe('2,00 €')
    expect(row.get('[data-test="facts"] [data-test="estimate"]').text()).toBe('~6 Min.')
  })

  it('keeps the facts line inside the add-one target, so one tap covers the whole block', () => {
    const row = mountRowWithEstimate({ min: 6, max: 6 })

    expect(row.get('[data-test="add"]').find('[data-test="facts"]').exists()).toBe(true)
  })

  it('writes just as short in English', () => {
    const row = mountRowWithEstimate({ min: 6, max: 6 }, 'en')

    expect(row.get('[data-test="name"]').text()).toBe('Wasser')
    expect(row.get('[data-test="estimate"]').text()).toBe('~6 min')
  })

  it('says right away when there is nothing to wait for', () => {
    const row = mountRowWithEstimate({ min: 0, max: 0 })

    expect(row.get('[data-test="estimate"]').text()).toBe('~0 Min.')
  })

  it('names the span between the quickest and the slowest station', () => {
    const row = mountRowWithEstimate({ min: 10, max: 62 })

    expect(row.get('[data-test="estimate"]').text()).toBe('~10 - 62 Min.')
  })

  it('names the same span in English', () => {
    const row = mountRowWithEstimate({ min: 10, max: 62 }, 'en')

    expect(row.get('[data-test="estimate"]').text()).toBe('~10 - 62 min')
  })

  it('writes the price alone when the item carries no time of its own', () => {
    const row = mountRowWithEstimate(null)

    expect(row.get('[data-test="name"]').text()).toBe('Wasser')
    expect(row.get('[data-test="unit-price"]').text()).toBe('2,00 €')
    expect(row.find('[data-test="estimate"]').exists()).toBe(false)
  })

  it('writes no time on a sold out item, because nobody can order it and wait for it', () => {
    const row = mountRowWithEstimate({ min: 6, max: 6 }, 'de', false)

    expect(row.get('[data-test="name"]').text()).toBe('Wasser')
    expect(row.find('[data-test="estimate"]').exists()).toBe(false)
    expect(row.get('[data-test="unit-price"]').text()).toBe('2,00 €')
    expect(row.get('[data-test="facts"] [data-test="sold-out"]').text()).toBe('Ausverkauft')
  })
})

describe('the counts and totals on an article', () => {
  it('sums every row on the header and prices each row on its own', () => {
    const row = mount(ItemRow, {
      props: {
        item: schnitzel(),
        positions: [
          at(0, 'station-schank', 'Schank'),
          at(1, 'station-schank', 'Schank'),
          at(2, 'station-kueche', 'Küche'),
          at(3, 'station-schank', 'Schank'),
          at(4, 'station-schank', 'Schank'),
          at(5, 'station-schank', 'Schank', 'ABCD'),
          at(6, 'station-schank', 'Schank'),
        ],
        language: 'en' as const,
        estimateRange: null,
        stationNameFor,
      },
      global: { plugins: testPlugins('en') },
      attachTo: document.body,
    })

    expect(row.get('[data-test="unit-price"]').text()).toBe('7 × €3.00')
    expect(row.get('[data-test="article-total"]').text()).toBe('€21.00')
    const rows = row.findAll('[data-test="note-group"]')
    expect(rows.map((entry) => entry.get('[data-test="group-count"]').text())).toEqual(['5', '1', '1'])
    expect(rows.map((entry) => entry.get('[data-test="group-station"]').text())).toEqual(['Schank', 'Küche', 'Schank'])
    expect(
      rows
        .filter((entry) => entry.find('[data-test="group-note"]').exists())
        .map((entry) => [entry.get('[data-test="group-station"]').text(), entry.get('[data-test="group-note"]').text()]),
    ).toEqual([['Schank', 'ABCD']])
  })

  it('puts every plain portion on the one row the header total still sums up', () => {
    const row = mount(ItemRow, {
      props: {
        item: { ...item(true), priceCents: 400 },
        positions: [plain(0), plain(1), plain(2)],
        language: 'en' as const,
        estimateRange: null,
        stationNameFor,
      },
      global: { plugins: testPlugins('en') },
      attachTo: document.body,
    })

    expect(row.get('[data-test="unit-price"]').text()).toBe('3 × €4.00')
    expect(row.get('[data-test="article-total"]').text()).toBe('€12.00')
    expect(row.findAll('[data-test="note-group"]')).toHaveLength(1)
    const onlyRow = row.get('[data-test="note-group"]')
    expect(onlyRow.get('[data-test="group-count"]').text()).toBe('3')
    expect(onlyRow.get('[data-test="group-station-fixed"]').text()).toBe('Theke')
  })

  it('shows the plain unit price and no total while the article is not on the order', () => {
    const row = mountRow(true, [])

    expect(row.get('[data-test="unit-price"]').text()).toBe('2,00 €')
    expect(row.find('[data-test="article-total"]').exists()).toBe(false)
  })
})
