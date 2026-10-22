import { expect, vi } from 'vitest'
import { mount, type VueWrapper } from '@vue/test-utils'
import { VAutocomplete } from 'vuetify/components'
import ItemIngredientsDialog from '../../../../src/admin/components/items/ItemIngredientsDialog.vue'
import { useAdminIngredientsStore } from '../../../../src/admin/stores/ingredients'
import { testPlugins } from '../../../support/plugins'
import { answer, emptyAnswer, noConnection, stubLaptop, type StubbedLaptop } from '../../../support/laptop'
import { inputOf, onScreen } from '../../../support/dom'
import { anAdminIngredient, anAdminItem, anAdminItemIngredient } from '../../../support/wireViews'
import { BUN } from '../ingredients/ingredientsListFixture'

const FLOUR = anAdminIngredient({ ingredientId: 'ingredient-mehl', name: 'Mehl', unit: 'gram' })

const MILK = anAdminIngredient({ ingredientId: 'ingredient-milch', name: 'Milch', unit: 'millilitre' })

const CREAM = anAdminIngredient({ ingredientId: 'ingredient-sahne', name: 'Sahne', unit: 'millilitre' })

const OLD_HONEY = anAdminIngredient({ ingredientId: 'ingredient-honig', name: 'Honig', unit: 'gram', isActive: false })

const CREATED_EGG = anAdminIngredient({ ingredientId: 'ingredient-ei', name: 'Ei' })

const PANCAKE = anAdminItem({
  itemId: 'item-pfannkuchen',
  name: 'Pfannkuchen',
  ingredients: [
    anAdminItemIngredient({ ingredientId: 'ingredient-mehl', name: 'Mehl', unit: 'gram', amount: 1500 }),
    anAdminItemIngredient({ amount: 2 }),
  ],
})

export const FLOUR_LINE = '[data-test="recipe-line"][data-test-id="ingredient-mehl"]'

export const BUN_LINE = '[data-test="recipe-line"][data-test-id="ingredient-broetchen"]'

export function recipeLaptop(): StubbedLaptop {
  return stubLaptop()
    .answersEverythingElse(answer({}))
    .answers('GET', /./, answer({ items: [PANCAKE] }))
    .answers('DELETE', /./, emptyAnswer(204))
    .answers('GET', '/api/admin/ingredients', answer({ ingredients: [BUN, OLD_HONEY, FLOUR, MILK, CREAM] }))
    .answers('POST', '/api/admin/ingredients', answer(CREATED_EGG, 201))
}

export function recipeLaptopWithNothingLeftToAdd(): StubbedLaptop {
  return recipeLaptop().answers('GET', '/api/admin/ingredients', answer({ ingredients: [BUN, OLD_HONEY, FLOUR] }))
}

export function recipeLaptopWithoutTheIngredientList(): StubbedLaptop {
  return recipeLaptop().answers('GET', '/api/admin/ingredients', noConnection())
}

export function mountDialog(locale: 'de' | 'en' = 'de'): VueWrapper {
  return mount(ItemIngredientsDialog, {
    props: { item: PANCAKE },
    global: { plugins: testPlugins(locale) },
    attachTo: document.body,
  })
}

export async function loadIngredientsAndMountDialog(locale: 'de' | 'en' = 'de'): Promise<VueWrapper> {
  await useAdminIngredientsStore().load()
  const dialog = mountDialog(locale)
  await vi.waitFor(() => expect(document.querySelector('[data-test="recipe-line"]')).not.toBeNull())
  return dialog
}

export function ingredientSearch(dialog: VueWrapper) {
  return dialog.getComponent<typeof VAutocomplete>('[data-test="ingredient-search"]')
}

export function addedAmountInput(): HTMLInputElement {
  return inputOf('[data-test="add-recipe-line"] [data-test="amount-input"]')
}

export function addButtonIsDisabled(): boolean {
  return onScreen('[data-test="add-to-recipe"]').hasAttribute('disabled')
}

export async function chooseIngredient(dialog: VueWrapper, ingredientId: string): Promise<void> {
  await ingredientSearch(dialog).setValue(ingredientId)
  await vi.waitFor(() => expect(addedAmountInput().disabled).toBe(false))
}
