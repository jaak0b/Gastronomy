import { describe, expect, it } from 'vitest'
import { defineComponent, h } from 'vue'
import { mount } from '@vue/test-utils'
import { useIngredientAmountText } from '../../../src/admin/composables/useIngredientAmountText'
import type { IngredientUnit } from '../../../src/shared/api/generatedSchemas'
import { testPlugins } from '../../support/plugins'

type AmountText = (baseAmount: number, unit: IngredientUnit) => string

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

describe('the text of an ingredient amount', () => {
  it('writes amounts in German', () => {
    const amountText = amountTextIn('de')

    expect(amountText(250, 'gram')).toBe('250 g')
    expect(amountText(1500, 'gram')).toBe('1,5 kg')
    expect(amountText(2500, 'millilitre')).toBe('2,5 l')
    expect(amountText(1, 'piece')).toBe('1 Stück')
    expect(amountText(3, 'piece')).toBe('3 Stück')
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
