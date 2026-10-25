import { expect } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import type Konva from 'konva'
import type { Plugin } from 'vue'
import TablePlanEditor from '../../../../src/admin/components/tablePlan/TablePlanEditor.vue'
import type { AdminAreaView, AdminFestivalView, AdminStationView } from '../../../../src/shared/api/generatedSchemas'
import { onScreen, waitUntil } from '../../../support/dom'
import { drawingIn, shapeWithId, centreOf, tapAt } from '../../../support/konvaDrawing'
import { answer, stubLaptop, type LaptopReply, type StubbedLaptop } from '../../../support/laptop'
import { testPlugins } from '../../../support/plugins'
import {
  aFestival,
  aGeometry,
  anAdminArea,
  anAdminStation,
  anAdminStationShape,
  anAdminTable,
} from '../../../support/wireViews'

export const FESTIVAL_ID = 'fest-1'

export const PLAN_PATH = '/api/admin/festivals/fest-1/table-plan'

export const TABLE_A1 = anAdminTable({
  tableId: 'table-a1',
  name: 'A1',
  geometry: aGeometry({ x: 20, y: 30, width: 80, height: 40 }),
})

export const TABLE_A2 = anAdminTable({
  tableId: 'table-a2',
  name: 'A2',
  geometry: aGeometry({ x: 20, y: 100, width: 80, height: 40 }),
})

export const SCHANK_SHAPE = anAdminStationShape({
  stationId: 'station-schank',
  stationName: 'Schank',
  geometry: aGeometry({ x: 200, y: 30, width: 120, height: 40 }),
})

export const HALL = anAdminArea({
  areaId: 'area-halle',
  name: 'Halle',
  tables: [TABLE_A1],
  stationShapes: [SCHANK_SHAPE],
})

export const HALL_WITH_TWO_TABLES = anAdminArea({
  areaId: 'area-halle',
  name: 'Halle',
  tables: [TABLE_A1, TABLE_A2],
  stationShapes: [SCHANK_SHAPE],
})

export const TENT = anAdminArea({ areaId: 'area-zelt', name: 'Zelt' })

export const KITCHEN = anAdminStation({ stationId: 'station-kueche', name: 'Küche', isAtAnyFestival: true })

export const SCHANK = anAdminStation({
  stationId: 'station-schank',
  name: 'Schank',
  sortOrder: 2,
  isAtAnyFestival: true,
})

export const TABLE_A1_SHAPE_ID = 'table-table-a1'

export const TABLE_A2_SHAPE_ID = 'table-table-a2'

export const SCHANK_SHAPE_ID = 'station-station-schank'

export const COPY_PATH = '/api/admin/festivals/fest-1/tables/copy'

export const MOVE_PATH = '/api/admin/festivals/fest-1/tables/move'

export const DRAWING = '[data-test="table-plan-drawing"]'

export interface EditorScenario {
  festival?: AdminFestivalView
  areas?: AdminAreaView[]
  planReply?: LaptopReply
  stations?: AdminStationView[]
  writeReply?: LaptopReply
}

export function editorLaptop(scenario: EditorScenario = {}): StubbedLaptop {
  return stubLaptop()
    .answersEverythingElse(scenario.writeReply ?? answer({}))
    .answers('GET', '/api/admin/festivals', answer({ festivals: [scenario.festival ?? aFestival({ festivalId: FESTIVAL_ID })] }))
    .answers('GET', '/api/admin/stations', answer({ stations: scenario.stations ?? [KITCHEN, SCHANK] }))
    .answers('GET', PLAN_PATH, scenario.planReply ?? answer({ areas: scenario.areas ?? [HALL, TENT] }))
}

export async function mountEditor(
  locale: 'de' | 'en' = 'de',
  extraPlugins: Plugin[] = [],
  areaId = HALL.areaId,
) {
  const editor = mount(TablePlanEditor, {
    props: { festivalId: FESTIVAL_ID, areaId },
    global: { plugins: [...testPlugins(locale), ...extraPlugins] },
    attachTo: document.body,
  })
  await waitUntil(() => expect(editor.find(DRAWING).exists()).toBe(true))
  await flushPromises()
  return editor
}

export function theDrawing(): Konva.Stage {
  return drawingIn(DRAWING)
}

export function shapesWithTheHandles(): string[] {
  return theDrawing().findOne<Konva.Transformer>('Transformer')?.nodes().map((node) => node.id()) ?? []
}

export async function selectTheShape(id: string): Promise<void> {
  const stage = theDrawing()
  await tapAt(stage, centreOf(shapeWithId(stage, id)))
  await waitUntil(() => expect(shapesWithTheHandles()).toEqual([id]))
}

export function tableRow(tableId: string): HTMLElement {
  return onScreen(`[data-test="table-row"][data-test-id="${tableId}"]`)
}

export function stationRow(stationId: string): HTMLElement {
  return onScreen(`[data-test="station-row"][data-test-id="${stationId}"]`)
}

export function drawingScrollArea(): HTMLElement {
  return onScreen('[data-test="drawing-scroll"]')
}
