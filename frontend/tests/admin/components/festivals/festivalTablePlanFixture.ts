import { onScreen } from '../../../support/dom'
import { anAdminArea, anAdminTable } from '../../../support/wireViews'

export const AREAS_PATH = '/api/admin/festivals/fest-1/areas'

export const HALL = anAdminArea({
  areaId: 'area-halle',
  name: 'Halle',
  sortOrder: 1,
  tables: [anAdminTable({ tableId: 'table-a1', name: 'A1' }), anAdminTable({ tableId: 'table-a2', name: 'A2' })],
})

export const TENT = anAdminArea({
  areaId: 'area-zelt',
  name: 'Zelt',
  sortOrder: 2,
  tables: [anAdminTable({ tableId: 'table-b1', name: 'B1' })],
})

export const OLD_TABLES = anAdminArea({ areaId: 'area-alt', name: 'Bisherige Tische', sortOrder: 3, isHidden: true })

export function areaRow(areaId: string): HTMLElement {
  return onScreen(`[data-test="festival-area-row"][data-test-id="${areaId}"]`)
}

export function hideButtonOf(areaId: string): HTMLButtonElement {
  return onScreen('[data-test="hide-entry"]', areaRow(areaId)) as HTMLButtonElement
}
