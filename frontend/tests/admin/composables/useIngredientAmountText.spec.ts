import { describe, expect, it } from 'vitest'
import { defineComponent, h } from 'vue'
import { mount } from '@vue/test-utils'
import { useIngredientAmountText } from '../../../src/admin/composables/useIngredientAmountText'
import { testPlugins } from '../../support/plugins'

type AmountText = (amount: number, unitName: string) => string

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
  it('writes the stored number and the unit name in German', () => {
    const amountText = amountTextIn('de')

    expect(amountText(250, 'g')).toBe('250 g')
    expect(amountText(1500, 'g')).toBe('1.500 g')
    expect(amountText(2500, 'ml')).toBe('2.500 ml')
    expect(amountText(0.5, 'l')).toBe('0,5 l')
  })

  it('writes the stored number and the unit name in English', () => {
    const amountText = amountTextIn('en')

    expect(amountText(250, 'g')).toBe('250 g')
    expect(amountText(1500, 'g')).toBe('1,500 g')
    expect(amountText(2500, 'ml')).toBe('2,500 ml')
    expect(amountText(1.25, 'g')).toBe('1.25 g')
  })

  it('uses the unit name as data, without a plural form', () => {
    const amountText = amountTextIn('en')

    expect(amountText(1, 'piece')).toBe('1 piece')
    expect(amountText(3, 'piece')).toBe('3 piece')
    expect(amountText(3, 'Stück')).toBe('3 Stück')
  })
})
