import { beforeEach, describe, expect, it } from 'vitest'
import { mount, type VueWrapper } from '@vue/test-utils'
import IngredientAmountField from '../../../../src/admin/components/ingredients/IngredientAmountField.vue'
import { testPlugins } from '../../../support/plugins'

function mountField(
  unitName: string,
  savedAmount: number | null,
  locale: 'de' | 'en' = 'de',
  options: { amountWhenCleared?: number | null; focusOnMount?: boolean } = {},
): VueWrapper {
  return mount(IngredientAmountField, {
    props: { unitName, savedAmount, label: 'Menge', ...options },
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

describe('the unit of an amount', () => {
  it('shows the unit name the ingredient carries', () => {
    const field = mountField('g', 2)

    expect(field.get('[data-test="amount-unit"]').text()).toBe('g')
    expect(amountInput(field).value).toBe('2')
  })

  it('sits inside the field, after the typed number', () => {
    const field = mountField('ml', 6)

    expect(field.find('[data-test="amount-input"] [data-test="amount-unit"]').exists()).toBe(true)
  })

  it('stays inside an empty field, so the admin sees the unit before typing', () => {
    const field = mountField('Stück', null)

    expect(field.get('[data-test="amount-input"] [data-test="amount-unit"]').text()).toBe('Stück')
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

describe('an amount that must not stay empty', () => {
  it('sets a cleared amount to the named number and sends it', async () => {
    const field = mountField('g', 1500, 'de', { amountWhenCleared: 1 })
    await field.get('[data-test="amount-input"] input').setValue('')

    await field.get('[data-test="amount-input"] input').trigger('blur')

    expect(amountInput(field).value).toBe('1')
    expect(field.emitted('amountChanged')).toEqual([[{ kind: 'amount', amount: 1 }]])
  })

  it('restores the named number without sending when it is already saved', async () => {
    const field = mountField('g', 1, 'de', { amountWhenCleared: 1 })
    await field.get('[data-test="amount-input"] input').setValue('')

    await field.get('[data-test="amount-input"] input').trigger('blur')

    expect(amountInput(field).value).toBe('1')
    expect(field.emitted('amountChanged')).toBeUndefined()
  })

  it('sets and sends the named number when Enter leaves the field', async () => {
    const field = mountField('g', 1500, 'de', { amountWhenCleared: 1 })
    const input = field.get('[data-test="amount-input"] input')
    const inputElement = input.element as HTMLInputElement
    await input.setValue('')
    inputElement.focus()

    await input.trigger('keydown.enter')

    expect(amountInput(field).value).toBe('1')
    expect(field.emitted('amountChanged')).toEqual([[{ kind: 'amount', amount: 1 }]])
  })

  it('leaves an empty field empty when no number is named', async () => {
    const field = mountField('g', 1500)
    await field.get('[data-test="amount-input"] input').setValue('')

    await field.get('[data-test="amount-input"] input').trigger('blur')

    expect(amountInput(field).value).toBe('')
    expect(field.emitted('amountChanged')).toEqual([[{ kind: 'empty' }]])
  })
})

describe('an amount field asked to be focused', () => {
  it('focuses the input and selects the saved amount when mounted', () => {
    const field = mountField('Stück', 1, 'de', { focusOnMount: true })
    const input = amountInput(field)

    expect(document.activeElement).toBe(input)
    expect(input.selectionStart).toBe(0)
    expect(input.selectionEnd).toBe(1)
    expect(field.emitted('focused')).toHaveLength(1)
  })

  it('focuses the input and selects the saved amount when asked after mounting', async () => {
    const field = mountField('Stück', 1)
    expect(document.activeElement).not.toBe(amountInput(field))

    await field.setProps({ focusOnMount: true })

    const input = amountInput(field)
    expect(document.activeElement).toBe(input)
    expect(input.selectionStart).toBe(0)
    expect(input.selectionEnd).toBe(1)
    expect(field.emitted('focused')).toHaveLength(1)
  })
})
