import { describe, expect, it } from 'vitest'
import { defineComponent, h } from 'vue'
import { mount } from '@vue/test-utils'
import {
  useIngredientUnitChoices,
  type IngredientUnitChoice,
} from '../../../src/admin/composables/useIngredientUnitChoices'
import { testPlugins } from '../../support/plugins'

function unitChoicesIn(locale: 'de' | 'en'): IngredientUnitChoice[] {
  let choices: IngredientUnitChoice[] = []
  mount(
    defineComponent({
      setup() {
        choices = useIngredientUnitChoices().value
        return () => h('div')
      },
    }),
    { global: { plugins: testPlugins(locale) } },
  )
  return choices
}

describe('the unit choices of an ingredient', () => {
  it('name the three units in German', () => {
    expect(unitChoicesIn('de')).toEqual([
      { value: 'piece', title: 'Stück' },
      { value: 'gram', title: 'Gramm' },
      { value: 'millilitre', title: 'Milliliter' },
    ])
  })

  it('name the three units in English', () => {
    expect(unitChoicesIn('en')).toEqual([
      { value: 'piece', title: 'Pieces' },
      { value: 'gram', title: 'Grams' },
      { value: 'millilitre', title: 'Millilitres' },
    ])
  })
})
