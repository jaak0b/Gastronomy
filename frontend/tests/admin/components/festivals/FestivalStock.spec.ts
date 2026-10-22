import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mount, type VueWrapper } from '@vue/test-utils'
import { VSelect } from 'vuetify/components'
import { createPinia, setActivePinia } from 'pinia'
import FestivalStock from '../../../../src/admin/components/festivals/FestivalStock.vue'
import BaseChart from '../../../../src/admin/components/BaseChart.vue'
import { useAdminFestivalStockStore } from '../../../../src/admin/stores/festivalStock'
import type {
  AdminFestivalIngredientView,
  AdminFestivalView,
} from '../../../../src/shared/api/generatedSchemas'
import { testPlugins } from '../../../support/plugins'
import { stubLaptop, answer, type StubbedLaptop } from '../../../support/laptop'
import { typeInto, leave, inputOf, isOnScreen, onScreen, textOnScreen } from '../../../support/dom'
import { aFestival, anAdminFestivalIngredient } from '../../../support/wireViews'

const FESTIVAL_ID = 'festival-sommerfest'

const SUMMER = aFestival({
  festivalId: FESTIVAL_ID,
  startsAtUtc: '2026-07-18T10:00:00Z',
  endsAtUtc: '2026-07-19T02:00:00Z',
})

const ONE_DAY = aFestival({
  festivalId: FESTIVAL_ID,
  startsAtUtc: '2026-07-18T08:00:00Z',
  endsAtUtc: '2026-07-18T20:00:00Z',
})

const FLOUR = anAdminFestivalIngredient({
  ingredientId: 'ingredient-mehl',
  name: 'Mehl',
  unit: 'gram',
  availableAmount: 1500,
  usedAmount: 250,
  usage: [
    { atUtc: '2026-07-18T10:00:00Z', usedAmount: 0 },
    { atUtc: '2026-07-18T15:00:00Z', usedAmount: 250 },
    { atUtc: '2026-07-18T18:00:00Z', usedAmount: 250 },
  ],
})

const BUN = anAdminFestivalIngredient({
  ingredientId: 'ingredient-broetchen',
  name: 'Brötchen',
  unit: 'piece',
  availableAmount: null,
  usedAmount: 3,
  usage: [
    { atUtc: '2026-07-18T10:00:00Z', usedAmount: 0 },
    { atUtc: '2026-07-18T15:00:00Z', usedAmount: 3 },
    { atUtc: '2026-07-18T18:00:00Z', usedAmount: 3 },
  ],
})

const MUSTARD_NOT_USED_YET = anAdminFestivalIngredient({
  ingredientId: 'ingredient-senf',
  name: 'Senf',
  unit: 'gram',
  availableAmount: 500,
  usedAmount: 0,
  usage: [
    { atUtc: '2026-07-18T10:00:00Z', usedAmount: 0 },
    { atUtc: '2026-07-18T18:00:00Z', usedAmount: 0 },
  ],
})

const RUNS_OUT_SOON: Partial<AdminFestivalIngredientView> = {
  runsOutAtUtcFromLastTwoHours: '2026-07-18T19:30:00Z',
  stockWarning: 'runsOutSoon',
  runsOutSoonAtUtc: '2026-07-18T19:30:00Z',
}

function stockLaptop(ingredients: AdminFestivalIngredientView[]): StubbedLaptop {
  return stubLaptop()
    .answersEverythingElse(answer({}))
    .answers('GET', /./, answer({ ingredients }))
}

async function mountStock(
  locale: 'de' | 'en' = 'de',
  festival: AdminFestivalView = SUMMER,
): Promise<VueWrapper> {
  await useAdminFestivalStockStore().loadForFestival(FESTIVAL_ID)
  return mount(FestivalStock, {
    props: { festival },
    global: { plugins: testPlugins(locale), stubs: { BaseChart: true } },
    attachTo: document.body,
  })
}

function allTexts(selector: string): string[] {
  return [...document.querySelectorAll(selector)].map((element) => element.textContent?.trim() ?? '')
}

function amountInputs(): HTMLInputElement[] {
  return [...document.querySelectorAll<HTMLInputElement>('[data-test="festival-ingredient-row"] [data-test="amount-input"] input')]
}

function rowPart(ingredientId: string, part: string): string {
  return `[data-test="festival-ingredient-row"][data-test-id="${ingredientId}"] [data-test="${part}"]`
}

beforeEach(() => {
  setActivePinia(createPinia())
  document.body.innerHTML = ''
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('the stock of a festival', () => {
  it('shows each ingredient with its amount and what was used in German', async () => {
    stockLaptop([FLOUR, BUN])

    await mountStock()

    expect(allTexts('[data-test="festival-ingredient-row"] [data-test="ingredient-name"]')).toEqual(['Mehl', 'Brötchen'])
    expect(amountInputs().map((input) => input.value)).toEqual(['1500', ''])
    expect(allTexts('[data-test="festival-ingredient-row"] [data-test="amount-unit"]')).toEqual(['Stück'])
    expect(allTexts('[data-test="used-amount"]')).toEqual(['Verbraucht: 250 g', 'Verbraucht: 3 Stück'])
  })

  it('shows what was used in English', async () => {
    stockLaptop([FLOUR, BUN])

    await mountStock('en')

    expect(amountInputs().map((input) => input.value)).toEqual(['1500', ''])
    expect(allTexts('[data-test="festival-ingredient-row"] [data-test="amount-unit"]')).toEqual(['pieces'])
    expect(allTexts('[data-test="used-amount"]')).toEqual(['Used: 250 g', 'Used: 3 pieces'])
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

describe('the warning beside an ingredient', () => {
  it('stays empty while the stock lasts', async () => {
    stockLaptop([FLOUR, BUN])

    await mountStock()

    expect(allTexts('[data-test="stock-warning"]')).toEqual(['', ''])
  })

  it('names the weekday and the time the stock runs out when the festival runs past midnight', async () => {
    stockLaptop([{ ...FLOUR, ...RUNS_OUT_SOON }, BUN])

    await mountStock()

    expect(textOnScreen(rowPart('ingredient-mehl', 'stock-warning'))).toBe('Reicht voraussichtlich bis Sa., 21:30')
  })

  it('writes the sentence in the warning colour', async () => {
    stockLaptop([{ ...FLOUR, ...RUNS_OUT_SOON }, BUN])

    await mountStock()

    expect(onScreen(rowPart('ingredient-mehl', 'stock-warning-text')).classList).toContain('text-warning')
  })

  it('names only the time the stock runs out when the festival ends on the day it starts', async () => {
    stockLaptop([{ ...FLOUR, ...RUNS_OUT_SOON }])

    await mountStock('de', ONE_DAY)

    expect(textOnScreen(rowPart('ingredient-mehl', 'stock-warning'))).toBe('Reicht voraussichtlich bis 21:30')
  })

  it('names the weekday and the time in English', async () => {
    stockLaptop([{ ...FLOUR, ...RUNS_OUT_SOON }])

    await mountStock('en')

    expect(textOnScreen(rowPart('ingredient-mehl', 'stock-warning'))).toBe('Expected to last until Sat 09:30 PM')
  })

  it('says the ingredient is used up', async () => {
    stockLaptop([{ ...FLOUR, usedAmount: 1500, stockWarning: 'usedUp' }])

    await mountStock()

    expect(textOnScreen(rowPart('ingredient-mehl', 'stock-warning'))).toBe('Die Zutat ist aufgebraucht.')
  })

  it('says the ingredient is used up in English', async () => {
    stockLaptop([{ ...FLOUR, usedAmount: 1500, stockWarning: 'usedUp' }])

    await mountStock('en')

    expect(textOnScreen(rowPart('ingredient-mehl', 'stock-warning'))).toBe('This ingredient is used up.')
  })
})

describe('the history of an ingredient', () => {
  it('stays closed until the admin asks for it', async () => {
    stockLaptop([FLOUR, BUN])

    await mountStock()

    expect(allTexts('[data-test="history-toggle"]')).toEqual(['Verlauf', 'Verlauf'])
    expect(isOnScreen('[data-test="stock-chart"]')).toBe(false)
  })

  it('opens under the row whose button was pressed', async () => {
    stockLaptop([FLOUR, BUN])
    await mountStock()

    onScreenButton('ingredient-mehl').click()

    await vi.waitFor(() => expect(isOnScreen(rowPart('ingredient-mehl', 'stock-chart'))).toBe(true))
    expect(isOnScreen(rowPart('ingredient-broetchen', 'stock-chart'))).toBe(false)
  })

  it('draws the usage of the ingredient whose button was pressed', async () => {
    stockLaptop([FLOUR, BUN])
    const stock = await mountStock()

    onScreenButton('ingredient-broetchen').click()

    await vi.waitFor(() => expect(stock.findComponent(BaseChart).exists()).toBe(true))
    expect(stock.getComponent(BaseChart).props('option')).toMatchObject({
      series: [
        {
          name: 'Verbraucht',
          data: [
            [1_784_368_800_000, 0],
            [1_784_386_800_000, 0],
            [1_784_386_800_000, 3],
            [1_784_397_600_000, 3],
          ],
        },
      ],
    })
  })

  it('closes under one row as soon as the admin opens it under another', async () => {
    stockLaptop([FLOUR, BUN])
    await mountStock()
    onScreenButton('ingredient-mehl').click()
    await vi.waitFor(() => expect(isOnScreen(rowPart('ingredient-mehl', 'stock-chart'))).toBe(true))

    onScreenButton('ingredient-broetchen').click()

    await vi.waitFor(() => expect(isOnScreen(rowPart('ingredient-broetchen', 'stock-chart'))).toBe(true))
    expect(isOnScreen(rowPart('ingredient-mehl', 'stock-chart'))).toBe(false)
  })

  it('says that nothing was used yet instead of drawing an empty chart', async () => {
    stockLaptop([MUSTARD_NOT_USED_YET])
    await mountStock()

    onScreenButton('ingredient-senf').click()

    await vi.waitFor(() =>
      expect(textOnScreen(rowPart('ingredient-senf', 'nothing-used-yet'))).toBe('Bisher wurde nichts verbraucht.'),
    )
    expect(isOnScreen(rowPart('ingredient-senf', 'stock-chart'))).toBe(false)
  })

  it('says in English that nothing was used yet', async () => {
    stockLaptop([MUSTARD_NOT_USED_YET])
    await mountStock('en')

    onScreenButton('ingredient-senf').click()

    await vi.waitFor(() =>
      expect(textOnScreen(rowPart('ingredient-senf', 'nothing-used-yet'))).toBe('Nothing has been used yet.'),
    )
  })

  it('closes again when the admin presses the button a second time', async () => {
    stockLaptop([FLOUR, BUN])
    await mountStock()
    onScreenButton('ingredient-mehl').click()
    await vi.waitFor(() => expect(isOnScreen(rowPart('ingredient-mehl', 'stock-chart'))).toBe(true))

    onScreenButton('ingredient-mehl').click()

    await vi.waitFor(() => expect(isOnScreen(rowPart('ingredient-mehl', 'stock-chart'))).toBe(false))
  })

  it('is offered in English', async () => {
    stockLaptop([FLOUR])

    await mountStock('en')

    expect(allTexts('[data-test="history-toggle"]')).toEqual(['History'])
  })
})

function onScreenButton(ingredientId: string): HTMLElement {
  const button = document.querySelector<HTMLElement>(rowPart(ingredientId, 'history-toggle'))
  if (button === null) {
    throw new Error(`No history button for ${ingredientId}.`)
  }
  return button
}
