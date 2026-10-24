import { expect } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import type Konva from 'konva'
import type { Plugin } from 'vue'
import TablePlan from '../../../src/phone/views/TablePlan.vue'
import { useCatalogStore } from '../../../src/phone/stores/catalog'
import { useTablePlanStore } from '../../../src/phone/stores/tablePlan'
import type { TablePlanAreaView } from '../../../src/shared/api/generatedSchemas'
import { TOKEN_STORAGE_KEY, useSessionStore } from '../../../src/shared/stores/session'
import { isOnScreen, waitUntil } from '../../support/dom'
import { centreOf, drawingIn, moveFingers, putFingerDown, shapeWithId, tapAt, type Finger } from '../../support/konvaDrawing'
import { answer, stubLaptop, type LaptopReply, type StubbedLaptop } from '../../support/laptop'
import { testPlugins } from '../../support/plugins'
import { aGeometry, aPlanArea, aPlanStationShape, aPlanTable } from '../../support/wireViews'

export const TISCH_12 = aPlanTable({
  tableId: 'table-12',
  name: '12',
  geometry: aGeometry({ x: 20, y: 30, width: 80, height: 40 }),
})

export const TISCH_14 = aPlanTable({
  tableId: 'table-14',
  name: '14',
  geometry: aGeometry({ x: 20, y: 200, width: 80, height: 40 }),
})

export const SCHANK = aPlanStationShape({
  stationName: 'Schank',
  geometry: aGeometry({ x: 200, y: 30, width: 120, height: 40 }),
})

export const HALL = aPlanArea({
  areaId: 'area-halle',
  name: 'Halle',
  tables: [TISCH_12, TISCH_14],
  stationShapes: [SCHANK],
})

export const GARDEN = aPlanArea({ areaId: 'area-garten', name: 'Garten', tables: [], stationShapes: [] })

export const DRAWING = '[data-test="area-plan"]'

export const TABLE_ACTIONS = '[data-test="table-actions"]'

export function planLaptop(areas: TablePlanAreaView[] = [HALL], planReply: LaptopReply = answer({ areas })): StubbedLaptop {
  return stubLaptop().answersEverythingElse(answer({})).answers('GET', '/api/table-plan', planReply)
}

export function theCatalogNamesTheFestivalThatRuns(festival: { festivalId: string; name: string } | null): void {
  const catalog = useCatalogStore()
  catalog.catalog = { festival, categories: [], items: [], stations: [] }
  catalog.hasLoaded = true
}

export async function mountThePlan(locale: 'de' | 'en' = 'de', extraPlugins: Plugin[] = []) {
  localStorage.setItem(TOKEN_STORAGE_KEY, 'lookup.token-here')
  useSessionStore().deviceToken = 'token-here'
  useSessionStore().language = locale
  void useTablePlanStore().load()
  const view = mount(TablePlan, {
    global: { plugins: [...testPlugins(locale), ...extraPlugins], stubs: { transition: false } },
    attachTo: document.body,
  })
  await flushPromises()
  return view
}

export function theDrawing(): Konva.Stage {
  return drawingIn(DRAWING)
}

export function tableNode(tableId: string): Konva.Group {
  return shapeWithId(theDrawing(), `table-${tableId}`)
}

export async function tapTheTable(tableId: string): Promise<void> {
  await tapAt(theDrawing(), centreOf(tableNode(tableId)))
  await waitUntil(() => expect(isOnScreen(TABLE_ACTIONS)).toBe(true))
}

export const ON_TISCH_12: Finger = { id: 1, at: { x: 40, y: 50 } }

export const ALSO_ON_TISCH_12: Finger = { id: 2, at: { x: 80, y: 50 } }

export async function aPinchThatSpreadsTwoFingersOnTisch12() {
  const stage = theDrawing()
  const first = { ...ON_TISCH_12, at: { x: 30, y: 50 } }
  const second = { ...ALSO_ON_TISCH_12, at: { x: 90, y: 50 } }
  await putFingerDown(stage, ON_TISCH_12)
  await putFingerDown(stage, ALSO_ON_TISCH_12, [ON_TISCH_12])
  await moveFingers(stage, [ON_TISCH_12, ALSO_ON_TISCH_12])
  await moveFingers(stage, [first, second])
  await moveFingers(stage, [first, second])
  return { first, second }
}

