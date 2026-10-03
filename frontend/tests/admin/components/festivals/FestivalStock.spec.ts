import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mount, type VueWrapper } from '@vue/test-utils'
import { VSelect } from 'vuetify/components'
import { createPinia, setActivePinia } from 'pinia'
import FestivalStock from '../../../../src/admin/components/festivals/FestivalStock.vue'
import { useAdminFestivalStockStore } from '../../../../src/admin/stores/festivalStock'
import type { AdminFestivalIngredientView } from '../../../../src/shared/api/generatedSchemas'
import { testPlugins } from '../../../support/plugins'
import { stubLaptop, answer, type StubbedLaptop } from '../../../support/laptop'
import { typeInto, leave, inputOf } from '../../../support/dom'

const FESTIVAL_ID = 'festival-sommerfest'

const FLOUR: AdminFestivalIngredientView = {
  ingredientId: 'ingredient-mehl',
  name: 'Mehl',
  unit: 'gram',
  isActive: true,
  availableAmount: 1500,
  usedAmount: 250,
  runsOutAtUtc: '2026-07-12T16:30:00Z',
}

const BUN: AdminFestivalIngredientView = {
  ingredientId: 'ingredient-broetchen',
  name: 'Brötchen',
  unit: 'piece',
  isActive: true,
  availableAmount: null,
  usedAmount: 3,
  runsOutAtUtc: null,
}

interface Call {
  url: string
  method: string
  body: unknown
}

function stockLaptop(ingredients: AdminFestivalIngredientView[]): StubbedLaptop {
  return stubLaptop()
    .answersEverythingElse(answer({}))
    .answers('GET', /./, answer({ ingredients }))
}

async function mountStock(locale: 'de' | 'en' = 'de'): Promise<VueWrapper> {
  await useAdminFestivalStockStore().loadForFestival(FESTIVAL_ID)
  return mount(FestivalStock, {
    props: { festivalId: FESTIVAL_ID },
    global: { plugins: testPlugins(locale) },
    attachTo: document.body,
  })
}

function allTexts(selector: string): string[] {
  return [...document.querySelectorAll(selector)].map((element) => element.textContent?.trim() ?? '')
}

function amountInputs(): HTMLInputElement[] {
  return [...document.querySelectorAll<HTMLInputElement>('[data-test="festival-ingredient-row"] [data-test="amount-input"] input')]
}

beforeEach(() => {
  setActivePinia(createPinia())
  document.body.innerHTML = ''
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('the stock of a festival', () => {
  it('shows each ingredient with its amount, what was used and when it runs out in German', async () => {
    stockLaptop([FLOUR, BUN])

    await mountStock()

    expect(allTexts('[data-test="festival-ingredient-row"] [data-test="ingredient-name"]')).toEqual(['Mehl', 'Brötchen'])
    expect(amountInputs().map((input) => input.value)).toEqual(['1500', ''])
    expect(allTexts('[data-test="festival-ingredient-row"] [data-test="amount-unit"]')).toEqual(['Stück'])
    expect(allTexts('[data-test="used-amount"]')).toEqual(['Verbraucht: 250 g', 'Verbraucht: 3 Stück'])
    expect(allTexts('[data-test="runs-out"]')).toEqual(['Reicht voraussichtlich bis 12.07., 18:30', ''])
  })

  it('shows what was used and when it runs out in English', async () => {
    stockLaptop([FLOUR, BUN])

    await mountStock('en')

    expect(amountInputs().map((input) => input.value)).toEqual(['1500', ''])
    expect(allTexts('[data-test="festival-ingredient-row"] [data-test="amount-unit"]')).toEqual(['pieces'])
    expect(allTexts('[data-test="used-amount"]')).toEqual(['Used: 250 g', 'Used: 3 pieces'])
    expect(allTexts('[data-test="runs-out"]')).toEqual(['Expected to last until 07/12, 06:30 PM', ''])
  })

  it('is hidden when the festival has no ingredients', async () => {
    stockLaptop([])

    await mountStock()

    expect(document.querySelector('[data-test="festival-stock"]')).toBeNull()
  })

  it('sends an entered amount in grams', async () => {
    const laptop = stockLaptop([FLOUR, BUN])
    await mountStock()
    const flourAmount = inputOf('[data-test="festival-ingredient-row"][data-test-id="ingredient-mehl"] [data-test="amount-input"]')

    typeInto(flourAmount, '2000,5')
    leave(flourAmount)

    await vi.waitFor(() =>
      expect(laptop.writes()).toEqual([
        {
          url: '/api/admin/festivals/festival-sommerfest/ingredients/ingredient-mehl',
          method: 'PUT',
          body: { availableAmount: 2000.5 },
        },
      ]),
    )
  })

  it('sends no limit when the admin clears the field', async () => {
    const laptop = stockLaptop([FLOUR, BUN])
    await mountStock()
    const flourAmount = inputOf('[data-test="festival-ingredient-row"][data-test-id="ingredient-mehl"] [data-test="amount-input"]')

    typeInto(flourAmount, '')
    leave(flourAmount)

    await vi.waitFor(() =>
      expect(laptop.writes()).toEqual([
        {
          url: '/api/admin/festivals/festival-sommerfest/ingredients/ingredient-mehl',
          method: 'PUT',
          body: { availableAmount: null },
        },
      ]),
    )
  })

  it('offers grams and kilograms for a gram ingredient and shows a saved 1500 grams in grams', async () => {
    stockLaptop([FLOUR, BUN])

    const stock = await mountStock()

    const selects = stock.findAllComponents(VSelect)
    expect(selects).toHaveLength(1)
    expect(selects[0].props('items')).toEqual([
      { value: 'gram', title: 'g' },
      { value: 'kilogram', title: 'kg' },
    ])
    expect(selects[0].props('modelValue')).toBe('gram')
    expect(inputOf('[data-test="festival-ingredient-row"][data-test-id="ingredient-mehl"] [data-test="amount-input"]').value).toBe('1500')
  })

  it('sends 2 kilograms as 2000 grams', async () => {
    const laptop = stockLaptop([FLOUR, BUN])
    const stock = await mountStock()
    await stock.getComponent(VSelect).setValue('kilogram')
    const flourAmount = inputOf('[data-test="festival-ingredient-row"][data-test-id="ingredient-mehl"] [data-test="amount-input"]')

    typeInto(flourAmount, '2')
    leave(flourAmount)

    await vi.waitFor(() =>
      expect(laptop.writes()).toEqual([
        {
          url: '/api/admin/festivals/festival-sommerfest/ingredients/ingredient-mehl',
          method: 'PUT',
          body: { availableAmount: 2000 },
        },
      ]),
    )
  })
})
