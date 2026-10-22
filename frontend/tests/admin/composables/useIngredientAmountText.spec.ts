import { describe, expect, it } from 'vitest'
import { defineComponent, h } from 'vue'
import { mount } from '@vue/test-utils'
import {
  useAmountTextInEntryUnit,
  useIngredientAmountText,
} from '../../../src/admin/composables/useIngredientAmountText'
import type { AmountEntryUnit } from '../../../src/admin/core/ingredientAmounts'
import type { IngredientUnit } from '../../../src/shared/api/generatedSchemas'
import { testPlugins } from '../../support/plugins'

type AmountText = (baseAmount: number, unit: IngredientUnit) => string

type AmountTextInEntryUnit = (baseAmount: number, entryUnit: AmountEntryUnit) => string

function amountTextIn(locale: 'de' | 'en'): AmountText {
  let amountText: AmountText = () => ''
  mount(
    defineComponent({
      setup() {
        amountText = useIngredientAmountText()
        return () => h('div')
      },
    }),
    { global: { plugins: testPlugins(locale) } },
  )
  return amountText
}

function amountTextInEntryUnitIn(locale: 'de' | 'en'): AmountTextInEntryUnit {
  let amountText: AmountTextInEntryUnit = () => ''
  mount(
    defineComponent({
      setup() {
        amountText = useAmountTextInEntryUnit()
        return () => h('div')
      },
    }),
    { global: { plugins: testPlugins(locale) } },
  )
  return amountText
}

describe('the text of an ingredient amount', () => {
  it('writes amounts in German', () => {
    const amountText = amountTextIn('de')

    expect(amountText(250, 'gram')).toBe('250 g')
    expect(amountText(1500, 'gram')).toBe('1,5 kg')
    expect(amountText(2500, 'millilitre')).toBe('2,5 l')
    expect(amountText(1, 'piece')).toBe('1 Stück')
    expect(amountText(3, 'piece')).toBe('3 Stück')
  })

  it('writes an amount that rounds up to a whole litre in litres', () => {
    const amountText = amountTextIn('de')

    expect(amountText(999.9996, 'millilitre')).toBe('1 l')
  })

  it('writes amounts in English', () => {
    const amountText = amountTextIn('en')

    expect(amountText(250, 'gram')).toBe('250 g')
    expect(amountText(1500, 'gram')).toBe('1.5 kg')
    expect(amountText(2500, 'millilitre')).toBe('2.5 l')
    expect(amountText(250, 'millilitre')).toBe('250 ml')
    expect(amountText(1, 'piece')).toBe('1 piece')
    expect(amountText(3, 'piece')).toBe('3 pieces')
  })
})

describe('the text of an amount in a unit chosen beforehand', () => {
  it('writes a small amount in the larger unit in German', () => {
    const amountText = amountTextInEntryUnitIn('de')

    expect(amountText(250, 'kilogram')).toBe('0,25 kg')
    expect(amountText(500, 'litre')).toBe('0,5 l')
  })

  it('writes pieces with the right plural in English', () => {
    const amountText = amountTextInEntryUnitIn('en')

    expect(amountText(1, 'piece')).toBe('1 piece')
    expect(amountText(1500, 'piece')).toBe('1,500 pieces')
  })
})
