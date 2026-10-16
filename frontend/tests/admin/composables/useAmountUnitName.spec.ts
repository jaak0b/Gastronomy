import { describe, expect, it } from 'vitest'
import { defineComponent, h } from 'vue'
import { mount } from '@vue/test-utils'
import { useAmountUnitName } from '../../../src/admin/composables/useAmountUnitName'
import type { AmountEntryUnit } from '../../../src/admin/core/ingredientAmounts'
import { testPlugins } from '../../support/plugins'

const EVERY_ENTRY_UNIT: AmountEntryUnit[] = ['piece', 'gram', 'kilogram', 'millilitre', 'litre']

function unitNameIn(locale: 'de' | 'en'): (entryUnit: AmountEntryUnit) => string {
  let unitName: (entryUnit: AmountEntryUnit) => string = () => ''
  mount(
    defineComponent({
      setup() {
        unitName = useAmountUnitName()
        return () => h('div')
      },
    }),
    { global: { plugins: testPlugins(locale) } },
  )
  return unitName
}

describe('the name of an amount unit', () => {
  it('is the short German name', () => {
    const unitName = unitNameIn('de')

    expect(EVERY_ENTRY_UNIT.map((unit) => unitName(unit))).toEqual([
      'Stück',
      'g',
      'kg',
      'ml',
      'l',
    ])
  })

  it('is the short English name', () => {
    const unitName = unitNameIn('en')

    expect(EVERY_ENTRY_UNIT.map((unit) => unitName(unit))).toEqual([
      'pieces',
      'g',
      'kg',
      'ml',
      'l',
    ])
  })
})
