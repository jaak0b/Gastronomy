import { mount } from '@vue/test-utils'
import IngredientsList from '../../../../src/admin/components/ingredients/IngredientsList.vue'
import type { AdminIngredientView } from '../../../../src/shared/api/generatedSchemas'
import { testPlugins } from '../../../support/plugins'
import { answer, emptyAnswer, stubLaptop, type LaptopReply, type StubbedLaptop } from '../../../support/laptop'

export const MUSTARD: AdminIngredientView = {
  ingredientId: 'ingredient-senf',
  name: 'Senf',
  unit: 'gram',
  isActive: true,
  activeItemNames: [],
}

export const BUN: AdminIngredientView = {
  ingredientId: 'ingredient-broetchen',
  name: 'Brötchen',
  unit: 'piece',
  isActive: true,
  activeItemNames: [],
}

export const OLD_MILK: AdminIngredientView = {
  ingredientId: 'ingredient-milch',
  name: 'Milch',
  unit: 'millilitre',
  isActive: false,
  activeItemNames: [],
}

export interface IngredientsListScenario {
  ingredients?: AdminIngredientView[]
  writes?: LaptopReply
}

export function ingredientsListLaptop(scenario: IngredientsListScenario = {}): StubbedLaptop {
  return stubLaptop()
    .answersEverythingElse(scenario.writes ?? emptyAnswer(204))
    .answers('GET', '/api/admin/ingredients', answer({ ingredients: scenario.ingredients ?? [BUN, MUSTARD, OLD_MILK] }))
}

export function mountList(locale: 'de' | 'en' = 'de') {
  return mount(IngredientsList, { global: { plugins: testPlugins(locale) }, attachTo: document.body })
}
