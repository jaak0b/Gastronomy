import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import FestivalPage from '../../../../src/admin/components/festivals/FestivalPage.vue'
import BaseChart from '../../../../src/admin/components/BaseChart.vue'
import { testPlugins } from '../../../support/plugins'
import { anAdminFestivalIngredient } from '../../../support/wireViews'
import { useAdminFestivalStockStore } from '../../../../src/admin/stores/festivalStock'
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
  const FLOUR_STOCK = anAdminFestivalIngredient({
    ingredientId: 'ingredient-mehl',
    name: 'Mehl',
    unitId: 'unit-gram',
    unitName: 'g',
    availableAmount: 25000,
    usedAmount: 1250,
  })
  const BUN_STOCK = anAdminFestivalIngredient({
    ingredientId: 'ingredient-broetchen',
    name: 'Brötchen',
    unitId: 'unit-stueck',
    unitName: 'Stück',
    availableAmount: null,
    usedAmount: 40,
    usage: [
      { atUtc: '2026-07-18T10:00:00Z', usedAmount: 0 },
      { atUtc: '2026-07-18T15:00:00Z', usedAmount: 40 },
      { atUtc: '2026-07-18T18:00:00Z', usedAmount: 40 },
    ],
  })

  async function mountWithStock(stock: unknown[], locale: 'de' | 'en' = 'de') {
    const laptop = festivalLaptop({ stock })
    const page = mount(FestivalPage, {
      props: { festivalId: FESTIVAL_ID },
      global: { plugins: testPlugins(locale), stubs: { BaseChart: true } },
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
    expect(flourRow.get('[data-test="amount-unit"]').text()).toBe('g')
    expect(bunRow.get('[data-test="amount-unit"]').text()).toBe('Stück')
    expect(flourRow.get('[data-test="used-amount"]').text()).toBe('Verbraucht: 1.250 g')
    expect(bunRow.get('[data-test="used-amount"]').text()).toBe('Verbraucht: 40 Stück')
  })

  it('writes the used amount in English', async () => {
    const { page } = await mountWithStock([FLOUR_STOCK, { ...BUN_STOCK, usedAmount: 1 }], 'en')

    expect(page.findAll('[data-test="used-amount"]').map((label) => label.text())).toEqual([
      'Used: 1,250 g',
      'Used: 1 Stück',
    ])
  })

  it('names the weekday and the time the stock runs out when the laptop warns that it runs out soon', async () => {
    const { page } = await mountWithStock([
      {
        ...FLOUR_STOCK,
        runsOutAtUtcFromLastTwoHours: '2026-07-18T19:30:00Z',
        stockWarning: 'runsOutSoon',
        runsOutSoonAtUtc: '2026-07-18T19:30:00Z',
      },
    ])

    expect(ingredientRow(page, 'ingredient-mehl').get('[data-test="stock-warning"]').text()).toBe(
      'Reicht voraussichtlich bis Sa., 21:30',
    )
  })

  it('says in English that an ingredient is used up', async () => {
    const { page } = await mountWithStock([{ ...FLOUR_STOCK, usedAmount: 25000, stockWarning: 'usedUp' }], 'en')

    expect(ingredientRow(page, 'ingredient-mehl').get('[data-test="stock-warning"]').text()).toBe(
      'This ingredient is used up.',
    )
  })

  it('says nothing beside an ingredient while its stock lasts', async () => {
    const { page } = await mountWithStock([FLOUR_STOCK])

    expect(ingredientRow(page, 'ingredient-mehl').get('[data-test="stock-warning"]').text()).toBe('')
  })

  it('opens the history of an ingredient under its row with the lines the laptop sent', async () => {
    const { page } = await mountWithStock([FLOUR_STOCK, BUN_STOCK])

    await ingredientRow(page, 'ingredient-mehl').get('[data-test="history-toggle"]').trigger('click')

    expect(ingredientRow(page, 'ingredient-mehl').getComponent(BaseChart).props('option')).toMatchObject({
      series: [{ name: 'Verbraucht' }, { name: 'Verfügbar' }],
    })
    expect(ingredientRow(page, 'ingredient-broetchen').findComponent(BaseChart).exists()).toBe(false)
  })

  it('closes the history again on a second press', async () => {
    const { page } = await mountWithStock([FLOUR_STOCK])
    await ingredientRow(page, 'ingredient-mehl').get('[data-test="history-toggle"]').trigger('click')

    await ingredientRow(page, 'ingredient-mehl').get('[data-test="history-toggle"]').trigger('click')

    expect(ingredientRow(page, 'ingredient-mehl').find('[data-test="stock-chart"]').exists()).toBe(false)
  })

  it('keeps the history open while the laptop announces new orders', async () => {
    const { page } = await mountWithStock([FLOUR_STOCK])
    await ingredientRow(page, 'ingredient-mehl').get('[data-test="history-toggle"]').trigger('click')
    festivalLaptop({ stock: [{ ...FLOUR_STOCK, usedAmount: 1500 }] })

    await useAdminFestivalStockStore().loadForFestival(FESTIVAL_ID)

    await vi.waitFor(() =>
      expect(ingredientRow(page, 'ingredient-mehl').get('[data-test="used-amount"]').text()).toBe('Verbraucht: 1.500 g'),
    )
    expect(ingredientRow(page, 'ingredient-mehl').find('[data-test="stock-chart"]').exists()).toBe(true)
  })

  it('sends a decimal amount exactly as it was typed', async () => {
    const { page, laptop } = await mountWithStock([
      BUN_STOCK,
      { ...FLOUR_STOCK, availableAmount: null },
    ])
    const flourRow = ingredientRow(page, 'ingredient-mehl')

    await flourRow.get('[data-test="amount-input"] input').setValue('12500,5')
    await flourRow.get('[data-test="amount-input"] input').trigger('blur')

    await vi.waitFor(() =>
      expect(laptop.writes()).toEqual([
        {
          url: `/api/admin/festivals/${FESTIVAL_ID}/ingredients/ingredient-mehl`,
          method: 'PUT',
          body: { availableAmount: 12500.5 },
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
        'Geben Sie null oder mehr ein oder lassen Sie das Feld leer.',
      ),
    )
    expect((page.get('[data-test="amount-input"] input').element as HTMLInputElement).value).toBe('0')
  })

  it('says in German that an unreadable amount cannot be used', async () => {
    const { page } = await mountWithStock([FLOUR_STOCK])

    await page.get('[data-test="amount-input"] input').setValue('viel')
    await page.get('[data-test="amount-input"] input').trigger('blur')

    await vi.waitFor(() =>
      expect(ingredientRow(page, 'ingredient-mehl').get('[data-test="refusal"]').text()).toBe(
        'Geben Sie null oder mehr ein oder lassen Sie das Feld leer.',
      ),
    )
  })

  it('shows the warning under the refused row only', async () => {
    const { page } = await mountWithStock([FLOUR_STOCK, BUN_STOCK])

    await ingredientRow(page, 'ingredient-mehl').get('[data-test="amount-input"] input').setValue('viel')
    await ingredientRow(page, 'ingredient-mehl').get('[data-test="amount-input"] input').trigger('blur')

    await vi.waitFor(() =>
      expect(ingredientRow(page, 'ingredient-mehl').find('[data-test="refusal"]').exists()).toBe(true),
    )
    expect(ingredientRow(page, 'ingredient-broetchen').find('[data-test="refusal"]').exists()).toBe(false)
  })

  it('keeps the warning on a row while the admin types in another row', async () => {
    const { page } = await mountWithStock([FLOUR_STOCK, BUN_STOCK])
    await ingredientRow(page, 'ingredient-mehl').get('[data-test="amount-input"] input').setValue('viel')
    await ingredientRow(page, 'ingredient-mehl').get('[data-test="amount-input"] input').trigger('blur')
    await vi.waitFor(() =>
      expect(ingredientRow(page, 'ingredient-mehl').find('[data-test="refusal"]').exists()).toBe(true),
    )

    await ingredientRow(page, 'ingredient-broetchen').get('[data-test="amount-input"] input').setValue('120')

    expect(ingredientRow(page, 'ingredient-mehl').find('[data-test="refusal"]').exists()).toBe(true)
  })

  it('takes the warning away as soon as the admin types in that row again', async () => {
    const { page } = await mountWithStock([FLOUR_STOCK])
    await page.get('[data-test="amount-input"] input').setValue('viel')
    await page.get('[data-test="amount-input"] input').trigger('blur')
    await vi.waitFor(() =>
      expect(ingredientRow(page, 'ingredient-mehl').find('[data-test="refusal"]').exists()).toBe(true),
    )

    await page.get('[data-test="amount-input"] input').setValue('25000')

    await vi.waitFor(() =>
      expect(ingredientRow(page, 'ingredient-mehl').find('[data-test="refusal"]').exists()).toBe(false),
    )
  })

  it('is hidden while no article on the menu uses an ingredient', async () => {
    festivalLaptop({ stock: [] })

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('[data-test="festival-name"]').exists()).toBe(true))

    expect(page.find('[data-test="festival-stock"]').exists()).toBe(false)
  })
})
