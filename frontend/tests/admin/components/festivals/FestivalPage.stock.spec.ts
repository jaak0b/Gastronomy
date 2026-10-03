import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { VSelect } from 'vuetify/components'
import FestivalPage from '../../../../src/admin/components/festivals/FestivalPage.vue'
import { testPlugins } from '../../../support/plugins'
import { FESTIVAL_ID, mountPage, ingredientRow, festivalLaptop } from './festivalPageFixture'

beforeEach(() => {
  setActivePinia(createPinia())
  window.history.replaceState({}, '', `/admin/festivals/${FESTIVAL_ID}`)
  document.body.innerHTML = ''
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('the ingredient stock of the festival', () => {
  const FLOUR_STOCK = {
    ingredientId: 'ingredient-mehl',
    name: 'Mehl',
    unit: 'gram',
    isActive: true,
    availableAmount: 25000,
    usedAmount: 1250,
    runsOutAtUtc: null,
  }
  const BUN_STOCK = {
    ingredientId: 'ingredient-broetchen',
    name: 'Brötchen',
    unit: 'piece',
    isActive: true,
    availableAmount: null,
    usedAmount: 40,
    runsOutAtUtc: null,
  }

  async function mountWithStock(stock: unknown[], locale: 'de' | 'en' = 'de') {
    const laptop = festivalLaptop({ stock })
    const page = mount(FestivalPage, {
      props: { festivalId: FESTIVAL_ID },
      global: { plugins: testPlugins(locale) },
      attachTo: document.body,
    })
    await vi.waitFor(() => expect(page.find('[data-test="festival-ingredient-row"]').exists()).toBe(true))
    return { page, laptop }
  }

  it('lists each ingredient with its available amount, the amount used and the unit', async () => {
    const { page } = await mountWithStock([FLOUR_STOCK, BUN_STOCK])

    const flourRow = ingredientRow(page, 'ingredient-mehl')
    const bunRow = ingredientRow(page, 'ingredient-broetchen')
    expect(page.get('[data-test="festival-stock"] [data-test="section-heading"]').text()).toBe('Zutaten')
    expect(page.findAll('[data-test="ingredient-name"]').map((name) => name.text())).toEqual([
      'Mehl',
      'Brötchen',
    ])
    expect((flourRow.get('[data-test="amount-input"] input').element as HTMLInputElement).value).toBe(
      '25000',
    )
    expect((bunRow.get('[data-test="amount-input"] input').element as HTMLInputElement).value).toBe('')
    expect(
      flourRow.getComponent<typeof VSelect>('[data-test="amount-unit-select"]').props('modelValue'),
    ).toBe('gram')
    expect(flourRow.get('[data-test="amount-unit-select"]').text()).toContain('g')
    expect(bunRow.get('[data-test="amount-unit"]').text()).toBe('Stück')
    expect(flourRow.get('[data-test="used-amount"]').text()).toBe('Verbraucht: 1,25 kg')
    expect(bunRow.get('[data-test="used-amount"]').text()).toBe('Verbraucht: 40 Stück')
  })

  it('writes the used amount in English', async () => {
    const { page } = await mountWithStock([FLOUR_STOCK, { ...BUN_STOCK, usedAmount: 1 }], 'en')

    expect(page.findAll('[data-test="used-amount"]').map((label) => label.text())).toEqual([
      'Used: 1.25 kg',
      'Used: 1 piece',
    ])
  })

  it('names the time of day the stock is expected to run out today', async () => {
    const runsOutAtUtc = new Date(new Date().setHours(21, 30, 0, 0)).toISOString()

    const { page } = await mountWithStock([{ ...FLOUR_STOCK, runsOutAtUtc }])

    expect(page.get('[data-test="runs-out"]').text()).toBe('Reicht voraussichtlich bis 21:30')
  })

  it('adds the date when the stock is expected to run out on another day', async () => {
    const later = new Date(2030, 6, 19, 1, 15).toISOString()

    const { page } = await mountWithStock([{ ...FLOUR_STOCK, runsOutAtUtc: later }], 'en')

    expect(page.get('[data-test="runs-out"]').text()).toBe('Expected to last until 07/19, 01:15 AM')
  })

  it('leaves the run out label blank when the laptop predicts nothing', async () => {
    const { page } = await mountWithStock([FLOUR_STOCK])

    expect(page.get('[data-test="runs-out"]').text()).toBe('')
  })

  it('sends an amount typed in kilograms as grams', async () => {
    const { page, laptop } = await mountWithStock([
      BUN_STOCK,
      { ...FLOUR_STOCK, availableAmount: null },
    ])
    const flourRow = ingredientRow(page, 'ingredient-mehl')

    await flourRow
      .getComponent<typeof VSelect>('[data-test="amount-unit-select"]')
      .setValue('kilogram')
    await flourRow.get('[data-test="amount-input"] input').setValue('12,5')
    await flourRow.get('[data-test="amount-input"] input').trigger('blur')

    await vi.waitFor(() =>
      expect(laptop.writes()).toEqual([
        {
          url: `/api/admin/festivals/${FESTIVAL_ID}/ingredients/ingredient-mehl`,
          method: 'PUT',
          body: { availableAmount: 12500 },
        },
      ]),
    )
  })

  it('sends null when the admin empties the field', async () => {
    const { page, laptop } = await mountWithStock([FLOUR_STOCK])

    await page.get('[data-test="amount-input"] input').setValue('')
    await page.get('[data-test="amount-input"] input').trigger('blur')

    await vi.waitFor(() =>
      expect(laptop.writes().map((call) => call.body)).toEqual([{ availableAmount: null }]),
    )
  })

  it('shows the reason when the laptop refuses the amount and keeps what was typed', async () => {
    const { page } = await mountWithStock([FLOUR_STOCK])
    festivalLaptop({
      stock: [FLOUR_STOCK],
      refusal: {
        status: 422,
        method: 'PUT',
        body: {
          code: 'UnprocessableEntity',
          messageKey: 'errors.admin.ingredients.stockInvalid',
          parameters: {},
          details: null,
        },
      },
    })

    await page.get('[data-test="amount-input"] input').setValue('0')
    await page.get('[data-test="amount-input"] input').trigger('blur')

    await vi.waitFor(() =>
      expect(ingredientRow(page, 'ingredient-mehl').get('[data-test="refusal"]').text()).toBe(
        'Geben Sie null oder mehr an, oder lassen Sie das Feld leer.',
      ),
    )
    expect((page.get('[data-test="amount-input"] input').element as HTMLInputElement).value).toBe('0')
  })

  it('is hidden while no article on the menu uses an ingredient', async () => {
    festivalLaptop({ stock: [] })

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('[data-test="festival-name"]').exists()).toBe(true))

    expect(page.find('[data-test="festival-stock"]').exists()).toBe(false)
  })
})
