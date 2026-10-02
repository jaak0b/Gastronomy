import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mount, type VueWrapper } from '@vue/test-utils'
import { VSelect } from 'vuetify/components'
import { createPinia, setActivePinia } from 'pinia'
import FestivalStock from '../../../../src/admin/components/festivals/FestivalStock.vue'
import { useAdminFestivalStockStore } from '../../../../src/admin/stores/festivalStock'
import type { AdminFestivalIngredientView } from '../../../../src/shared/api/generatedSchemas'
import { testPlugins } from '../../../support/plugins'

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

function stubLaptop(ingredients: AdminFestivalIngredientView[]): Call[] {
  const calls: Call[] = []
  vi.stubGlobal(
    'fetch',
    vi.fn(async (url: string, init?: RequestInit) => {
      const method = init?.method ?? 'GET'
      calls.push({
        url,
        method,
        body: init?.body === undefined ? null : JSON.parse(String(init.body)),
      })
      if (method !== 'GET') {
        return new Response(JSON.stringify({}), { status: 200 })
      }
      return new Response(JSON.stringify({ ingredients }), { status: 200 })
    }),
  )
  return calls
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
  return [...document.querySelectorAll<HTMLInputElement>('.festival-ingredient-row .amount-input input')]
}

function typeInto(input: HTMLInputElement, value: string): void {
  input.value = value
  input.dispatchEvent(new Event('input'))
}

function leave(input: HTMLInputElement): void {
  input.dispatchEvent(new FocusEvent('blur'))
}

function written(calls: Call[]): Call[] {
  return calls.filter((call) => call.method !== 'GET')
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
    stubLaptop([FLOUR, BUN])

    await mountStock()

    expect(allTexts('.festival-ingredient-row .name')).toEqual(['Mehl', 'Brötchen'])
    expect(amountInputs().map((input) => input.value)).toEqual(['1500', ''])
    expect(allTexts('.festival-ingredient-row .amount-unit')).toEqual(['Stück'])
    expect(allTexts('.used-amount')).toEqual(['Verbraucht: 250 g', 'Verbraucht: 3 Stück'])
    expect(allTexts('.runs-out')).toEqual(['Reicht voraussichtlich bis 12.07., 18:30', ''])
  })

  it('shows what was used and when it runs out in English', async () => {
    stubLaptop([FLOUR, BUN])

    await mountStock('en')

    expect(amountInputs().map((input) => input.value)).toEqual(['1500', ''])
    expect(allTexts('.festival-ingredient-row .amount-unit')).toEqual(['pieces'])
    expect(allTexts('.used-amount')).toEqual(['Used: 250 g', 'Used: 3 pieces'])
    expect(allTexts('.runs-out')).toEqual(['Expected to last until 07/12, 06:30 PM', ''])
  })

  it('is hidden when the festival has no ingredients', async () => {
    stubLaptop([])

    await mountStock()

    expect(document.querySelector('.festival-stock')).toBeNull()
  })

  it('sends an entered amount in grams', async () => {
    const calls = stubLaptop([FLOUR, BUN])
    await mountStock()
    const flourAmount = amountInputs()[0]

    typeInto(flourAmount, '2000,5')
    leave(flourAmount)

    await vi.waitFor(() =>
      expect(written(calls)).toEqual([
        {
          url: '/api/admin/festivals/festival-sommerfest/ingredients/ingredient-mehl',
          method: 'PUT',
          body: { availableAmount: 2000.5 },
        },
      ]),
    )
  })

  it('sends no limit when the admin clears the field', async () => {
    const calls = stubLaptop([FLOUR, BUN])
    await mountStock()
    const flourAmount = amountInputs()[0]

    typeInto(flourAmount, '')
    leave(flourAmount)

    await vi.waitFor(() =>
      expect(written(calls)).toEqual([
        {
          url: '/api/admin/festivals/festival-sommerfest/ingredients/ingredient-mehl',
          method: 'PUT',
          body: { availableAmount: null },
        },
      ]),
    )
  })

  it('offers grams and kilograms for a gram ingredient and shows a saved 1500 grams in grams', async () => {
    stubLaptop([FLOUR, BUN])

    const stock = await mountStock()

    const selects = stock.findAllComponents(VSelect)
    expect(selects).toHaveLength(1)
    expect(selects[0].props('items')).toEqual([
      { value: 'gram', title: 'g' },
      { value: 'kilogram', title: 'kg' },
    ])
    expect(selects[0].props('modelValue')).toBe('gram')
    expect(amountInputs()[0].value).toBe('1500')
  })

  it('sends 2 kilograms as 2000 grams', async () => {
    const calls = stubLaptop([FLOUR, BUN])
    const stock = await mountStock()
    await stock.getComponent(VSelect).setValue('kilogram')
    const flourAmount = amountInputs()[0]

    typeInto(flourAmount, '2')
    leave(flourAmount)

    await vi.waitFor(() =>
      expect(written(calls)).toEqual([
        {
          url: '/api/admin/festivals/festival-sommerfest/ingredients/ingredient-mehl',
          method: 'PUT',
          body: { availableAmount: 2000 },
        },
      ]),
    )
  })
})
