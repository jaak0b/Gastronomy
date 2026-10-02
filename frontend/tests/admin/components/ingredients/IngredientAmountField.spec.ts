import { beforeEach, describe, expect, it } from 'vitest'
import { mount, type VueWrapper } from '@vue/test-utils'
import { VSelect } from 'vuetify/components'
import IngredientAmountField from '../../../../src/admin/components/ingredients/IngredientAmountField.vue'
import type { AmountEntryUnit } from '../../../../src/admin/core/ingredientAmounts'
import { testPlugins } from '../../../support/plugins'

function mountField(
  entryUnits: readonly AmountEntryUnit[],
  savedAmount: number | null,
  locale: 'de' | 'en' = 'de',
): VueWrapper {
  return mount(IngredientAmountField, {
    props: { entryUnits, savedAmount, label: 'Menge' },
    global: { plugins: testPlugins(locale) },
    attachTo: document.body,
  })
}

function amountInput(field: VueWrapper): HTMLInputElement {
  return field.get('.amount-input input').element as HTMLInputElement
}

beforeEach(() => {
  document.body.innerHTML = ''
})

describe('the unit beside an amount', () => {
  it('offers grams and kilograms for an ingredient kept in grams', () => {
    const field = mountField(['gram', 'kilogram'], null)

    expect(field.getComponent(VSelect).props('items')).toEqual([
      { value: 'gram', title: 'g' },
      { value: 'kilogram', title: 'kg' },
    ])
  })

  it('offers all five units in English for a new ingredient', () => {
    const field = mountField(['piece', 'gram', 'kilogram', 'millilitre', 'litre'], null, 'en')

    expect(
      (field.getComponent(VSelect).props('items') as { title: string }[]).map(
        (choice) => choice.title,
      ),
    ).toEqual(['pieces', 'g', 'kg', 'ml', 'l'])
  })

  it('is plain text when only one unit fits', () => {
    const field = mountField(['piece'], 2)

    expect(field.findComponent(VSelect).exists()).toBe(false)
    expect(field.get('.amount-unit').text()).toBe('Stück')
    expect(amountInput(field).value).toBe('2')
  })
})

describe('switching the unit', () => {
  it('writes 1,5 kilograms as 1500 grams and commits nothing', async () => {
    const field = mountField(['kilogram', 'gram'], null)
    await field.get('.amount-input input').setValue('1,5')

    await field.getComponent(VSelect).setValue('gram')

    expect(amountInput(field).value).toBe('1500')
    expect(field.emitted('amountChanged')).toBeUndefined()
  })

  it('keeps unreadable text exactly as typed', async () => {
    const field = mountField(['gram', 'kilogram'], null)
    await field.get('.amount-input input').setValue('viel')

    await field.getComponent(VSelect).setValue('kilogram')

    expect(amountInput(field).value).toBe('viel')
    expect(field.emitted('amountTyped')?.at(-1)).toEqual([{ kind: 'unreadable' }, 'kilogram'])
  })

  it('keeps the typed number when the offered units change to another kind', async () => {
    const field = mountField(['piece', 'gram', 'kilogram', 'millilitre', 'litre'], null)
    await field.getComponent(VSelect).setValue('kilogram')
    await field.get('.amount-input input').setValue('2')

    await field.setProps({ entryUnits: ['millilitre', 'litre'] })

    expect(amountInput(field).value).toBe('2')
    expect(field.emitted('amountTyped')?.at(-1)).toEqual([
      { kind: 'amount', baseAmount: 2 },
      'millilitre',
    ])
  })
})

describe('leaving the field', () => {
  it('commits an amount typed in kilograms in grams', async () => {
    const field = mountField(['gram', 'kilogram'], null)
    await field.getComponent(VSelect).setValue('kilogram')
    await field.get('.amount-input input').setValue('0,25')

    await field.get('.amount-input input').trigger('blur')

    expect(field.emitted('amountChanged')).toEqual([[{ kind: 'amount', baseAmount: 250 }]])
  })

  it('commits nothing when the saved amount is unchanged', async () => {
    const field = mountField(['gram'], 250)

    await field.get('.amount-input input').trigger('blur')

    expect(field.emitted('amountChanged')).toBeUndefined()
  })
})
