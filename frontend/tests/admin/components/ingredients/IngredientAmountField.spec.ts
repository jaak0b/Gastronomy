import { beforeEach, describe, expect, it } from 'vitest'
import { mount, type VueWrapper } from '@vue/test-utils'
import IngredientAmountField from '../../../../src/admin/components/ingredients/IngredientAmountField.vue'
import { testPlugins } from '../../../support/plugins'

function mountField(
  unitName: string,
  savedAmount: number | null,
  locale: 'de' | 'en' = 'de',
): VueWrapper {
  return mount(IngredientAmountField, {
    props: { unitName, savedAmount, label: 'Menge' },
    global: { plugins: testPlugins(locale) },
    attachTo: document.body,
  })
}

function amountInput(field: VueWrapper): HTMLInputElement {
  return field.get('[data-test="amount-input"] input').element as HTMLInputElement
}

beforeEach(() => {
  document.body.innerHTML = ''
})

describe('the unit beside an amount', () => {
  it('shows the unit name the ingredient carries', () => {
    const field = mountField('g', 2)

    expect(field.get('[data-test="amount-unit"]').text()).toBe('g')
    expect(amountInput(field).value).toBe('2')
  })

  it('shows the saved amount with the decimal comma of the language', () => {
    const field = mountField('Stück', 0.5)

    expect(amountInput(field).value).toBe('0,5')
  })

  it('leaves the field empty when there is no amount', () => {
    const field = mountField('ml', null)

    expect(amountInput(field).value).toBe('')
  })
})

describe('leaving the field', () => {
  it('commits the typed number unchanged in the unit the ingredient carries', async () => {
    const field = mountField('g', null)
    await field.get('[data-test="amount-input"] input').setValue('1500')

    await field.get('[data-test="amount-input"] input').trigger('blur')

    expect(field.emitted('amountChanged')).toEqual([[{ kind: 'amount', amount: 1500 }]])
  })

  it('commits a decimal amount with a German comma unchanged', async () => {
    const field = mountField('ml', null)
    await field.get('[data-test="amount-input"] input').setValue('1,5')

    await field.get('[data-test="amount-input"] input').trigger('blur')

    expect(field.emitted('amountChanged')).toEqual([[{ kind: 'amount', amount: 1.5 }]])
  })

  it('commits unreadable text so the screen can refuse it', async () => {
    const field = mountField('g', null)
    await field.get('[data-test="amount-input"] input').setValue('viel')

    await field.get('[data-test="amount-input"] input').trigger('blur')

    expect(field.emitted('amountChanged')).toEqual([[{ kind: 'unreadable' }]])
  })

  it('commits nothing when the saved amount is unchanged', async () => {
    const field = mountField('g', 250)

    await field.get('[data-test="amount-input"] input').trigger('blur')

    expect(field.emitted('amountChanged')).toBeUndefined()
  })
})
