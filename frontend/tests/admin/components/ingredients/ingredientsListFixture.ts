import { mount } from '@vue/test-utils'
import IngredientsList from '../../../../src/admin/components/ingredients/IngredientsList.vue'
import type { AdminIngredientView, AdminUnitView } from '../../../../src/shared/api/generatedSchemas'
import { testPlugins } from '../../../support/plugins'
import { answer, emptyAnswer, stubLaptop, type LaptopReply, type StubbedLaptop } from '../../../support/laptop'
import { anAdminIngredient, SEEDED_UNITS } from '../../../support/wireViews'

export const MUSTARD = anAdminIngredient({
  ingredientId: 'ingredient-senf',
  name: 'Senf',
  unitId: 'unit-gram',
  unitName: 'g',
})

export const BUN = anAdminIngredient()

export const OLD_MILK = anAdminIngredient({
  ingredientId: 'ingredient-milch',
  name: 'Milch',
  unitId: 'unit-millilitre',
  unitName: 'ml',
  isActive: false,
})

export interface IngredientsListScenario {
  ingredients?: AdminIngredientView[]
  units?: AdminUnitView[]
  writes?: LaptopReply
}

export function ingredientsListLaptop(scenario: IngredientsListScenario = {}): StubbedLaptop {
  return stubLaptop()
    .answersEverythingElse(scenario.writes ?? emptyAnswer(204))
    .answers('GET', '/api/admin/ingredients', answer({ ingredients: scenario.ingredients ?? [BUN, MUSTARD, OLD_MILK] }))
    .answers('GET', '/api/admin/units', answer({ units: scenario.units ?? SEEDED_UNITS }))
}

export function mountList(locale: 'de' | 'en' = 'de') {
  return mount(IngredientsList, { global: { plugins: testPlugins(locale) }, attachTo: document.body })
}
