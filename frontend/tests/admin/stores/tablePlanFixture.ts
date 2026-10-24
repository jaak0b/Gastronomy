import { useAdminTablePlanStore } from '../../../src/admin/stores/tablePlan'
import { answer, stubLaptop, type LaptopReply, type StubbedLaptop } from '../../support/laptop'
import { aGeometry, anAdminArea, anAdminTable } from '../../support/wireViews'

export const FESTIVAL_ID = 'fest-1'

export const PLAN_PATH = '/api/admin/festivals/fest-1/table-plan'

export const TABLE_A1 = anAdminTable({ tableId: 'table-a1', name: 'A1', geometry: aGeometry() })

export const HALL = anAdminArea({ areaId: 'area-halle', name: 'Halle', sortOrder: 1, tables: [TABLE_A1] })

export const TENT = anAdminArea({ areaId: 'area-zelt', name: 'Zelt', sortOrder: 2 })

export function planLaptop(writeReply: LaptopReply = answer({})): StubbedLaptop {
  return stubLaptop()
    .answersEverythingElse(writeReply)
    .answers('GET', PLAN_PATH, answer({ areas: [HALL, TENT] }))
}

export async function planOfTheFestival(laptop: StubbedLaptop) {
  const plan = useAdminTablePlanStore()
  await plan.loadForFestival(FESTIVAL_ID)
  laptop.forgetCalls()
  return plan
}
