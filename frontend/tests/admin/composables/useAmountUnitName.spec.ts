import { describe, expect, it } from 'vitest'
import { defineComponent, h } from 'vue'
import { mount } from '@vue/test-utils'
import { useAmountUnitName } from '../../../src/admin/composables/useAmountUnitName'
import {
  ALL_ENTRY_UNITS,
  type AmountEntryUnit,
} from '../../../src/admin/core/ingredientAmounts'
import { testPlugins } from '../../support/plugins'

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

    expect(ALL_ENTRY_UNITS.map((unit) => unitName(unit))).toEqual([
      'Stück',
      'g',
      'kg',
      'ml',
      'l',
    ])
  })

  it('is the short English name', () => {
    const unitName = unitNameIn('en')

    expect(ALL_ENTRY_UNITS.map((unit) => unitName(unit))).toEqual([
      'pieces',
      'g',
      'kg',
      'ml',
      'l',
    ])
  })
})
