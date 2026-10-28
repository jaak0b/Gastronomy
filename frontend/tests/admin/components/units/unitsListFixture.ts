import { mount } from '@vue/test-utils'
import UnitsList from '../../../../src/admin/components/units/UnitsList.vue'
import type { AdminUnitView } from '../../../../src/shared/api/generatedSchemas'
import { testPlugins } from '../../../support/plugins'
import { answer, emptyAnswer, stubLaptop, type LaptopReply, type StubbedLaptop } from '../../../support/laptop'
import { SEEDED_UNITS, anAdminUnit } from '../../../support/wireViews'

const [PIECE_UNIT, GRAM_UNIT, , MILLILITRE_UNIT] = SEEDED_UNITS

export { GRAM_UNIT, PIECE_UNIT }

export const OLD_KILOGRAM = anAdminUnit({
  unitId: 'unit-kilogram',
  name: 'kg',
  isActive: false,
})

export const LISTED_UNITS = [PIECE_UNIT, GRAM_UNIT, MILLILITRE_UNIT, OLD_KILOGRAM]

export interface UnitsListScenario {
  units?: AdminUnitView[]
  writes?: LaptopReply
}

export function unitsListLaptop(scenario: UnitsListScenario = {}): StubbedLaptop {
  return stubLaptop()
    .answersEverythingElse(scenario.writes ?? emptyAnswer(204))
    .answers('GET', '/api/admin/units', answer({ units: scenario.units ?? LISTED_UNITS }))
}

export function mountList(locale: 'de' | 'en' = 'de') {
  return mount(UnitsList, { global: { plugins: testPlugins(locale) }, attachTo: document.body })
}
