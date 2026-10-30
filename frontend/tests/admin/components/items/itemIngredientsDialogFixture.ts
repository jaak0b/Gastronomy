import { expect, vi } from 'vitest'
import { nextTick } from 'vue'
import { mount, type VueWrapper } from '@vue/test-utils'
import ItemIngredientsDialog from '../../../../src/admin/components/items/ItemIngredientsDialog.vue'
import { useAdminIngredientsStore } from '../../../../src/admin/stores/ingredients'
import { useAdminUnitsStore } from '../../../../src/admin/stores/units'
import type { AdminIngredientView, AdminItemView } from '../../../../src/shared/api/generatedSchemas'
import { testPlugins } from '../../../support/plugins'
import { answer, emptyAnswer, noConnection, stubLaptop, type StubbedLaptop } from '../../../support/laptop'
import { allOnScreen, inputOf, onScreen } from '../../../support/dom'
import { anAdminIngredient, anAdminItem, anAdminItemIngredient, SEEDED_UNITS } from '../../../support/wireViews'

const BUN = anAdminIngredient({ usedByItemNames: ['Bratwurst', 'Pfannkuchen'] })

const FLOUR = anAdminIngredient({
  ingredientId: 'ingredient-mehl',
  name: 'Mehl',
  unitId: 'unit-gram',
  unitName: 'g',
  usedByItemNames: ['Pfannkuchen'],
})

const MILK = anAdminIngredient({
  ingredientId: 'ingredient-milch',
  name: 'Milch',
  unitId: 'unit-millilitre',
  unitName: 'ml',
})

const CREAM = anAdminIngredient({
  ingredientId: 'ingredient-sahne',
  name: 'Sahne',
  unitId: 'unit-millilitre',
  unitName: 'ml',
  usedByItemNames: ['Kaiserschmarrn'],
})

export const EVERY_INGREDIENT: AdminIngredientView[] = [BUN, FLOUR, MILK, CREAM]

export const CREATED_EGG = anAdminIngredient({ ingredientId: 'ingredient-ei', name: 'Ei' })

export const PANCAKE = anAdminItem({
  itemId: 'item-pfannkuchen',
  name: 'Pfannkuchen',
  ingredients: [
    anAdminItemIngredient({
      ingredientId: 'ingredient-mehl',
      name: 'Mehl',
      unitId: 'unit-gram',
      unitName: 'g',
      amount: 1500,
    }),
    anAdminItemIngredient({ amount: 2 }),
  ],
})

export const FLOUR_LINE = '[data-test="recipe-line"][data-test-id="ingredient-mehl"]'

export const BUN_LINE = '[data-test="recipe-line"][data-test-id="ingredient-broetchen"]'

export function ingredientRow(ingredientId: string): string {
  return `[data-test="ingredient-row"][data-test-id="${ingredientId}"]`
}

export function recipeLaptop(): StubbedLaptop {
  return stubLaptop()
    .answersEverythingElse(answer({}))
    .answers('GET', /./, answer({ items: [PANCAKE] }))
    .answers('DELETE', /./, emptyAnswer(204))
    .answers('GET', '/api/admin/ingredients', answer({ ingredients: EVERY_INGREDIENT }))
    .answers('GET', '/api/admin/units', answer({ units: SEEDED_UNITS }))
    .answers('POST', '/api/admin/ingredients', answer(CREATED_EGG, 201))
}

export function recipeLaptopWithoutTheIngredientList(): StubbedLaptop {
  return recipeLaptop().answers('GET', '/api/admin/ingredients', noConnection())
}

export function mountDialog(locale: 'de' | 'en' = 'de', item: AdminItemView = PANCAKE): VueWrapper {
  return mount(ItemIngredientsDialog, {
    props: { item },
    global: { plugins: testPlugins(locale) },
    attachTo: document.body,
  })
}

export async function loadIngredientsAndMountDialog(locale: 'de' | 'en' = 'de'): Promise<VueWrapper> {
  await useAdminIngredientsStore().load()
  await useAdminUnitsStore().load()
  const dialog = mountDialog(locale)
  await vi.waitFor(() => expect(document.querySelector('[data-test="recipe-line"]')).not.toBeNull())
  return dialog
}

export function namesInTheIngredientList(): string[] {
  return allOnScreen('[data-test="ingredient-row"] [data-test="ingredient-name"]').map((name) => name.textContent ?? '')
}

export function ingredientIdsOfRowsHolding(selector: string): string[] {
  return allOnScreen('[data-test="ingredient-row"]')
    .filter((row) => row.querySelector(selector) !== null)
    .map((row) => row.dataset.testId ?? '')
}

export function searchInput(): HTMLInputElement {
  return inputOf('[data-test="ingredient-search"]')
}

export async function hover(selector: string): Promise<void> {
  onScreen(selector).dispatchEvent(new MouseEvent('mouseenter'))
  await nextTick()
}
