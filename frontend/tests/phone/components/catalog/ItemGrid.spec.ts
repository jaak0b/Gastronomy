import { afterEach, describe, expect, it } from 'vitest'
import { enableAutoUnmount, mount } from '@vue/test-utils'
import { testPlugins } from '../../../support/plugins'
import ItemGrid from '../../../../src/phone/components/catalog/ItemGrid.vue'
import { item, schnitzel, stationNameFor } from './itemRowFixture'

enableAutoUnmount(afterEach)

function mountGridOfThree() {
  return mount(ItemGrid, {
    props: {
      items: [item(true), schnitzel(), { ...schnitzel(), id: 'item-pommes', name: 'Pommes' }],
      language: 'de' as const,
      positionsFor: () => [],
      estimateRangeFor: () => null,
      stationNameFor,
    },
    global: { plugins: testPlugins('de') },
  })
}

describe('the article list on the order screen', () => {
  it('gives every article row the light tint', () => {
    const grid = mountGridOfThree()

    const rows = grid.findAll('[data-test="item-row"]')
    expect(rows.map((row) => row.classes().includes('striped-row'))).toEqual([true, true, true])
  })

  it('gives every second article row the stronger tint, beginning with the second', () => {
    const grid = mountGridOfThree()

    const rows = grid.findAll('[data-test="item-row"]')
    expect(rows.map((row) => row.classes().includes('striped-row-strong'))).toEqual([false, true, false])
  })
})
