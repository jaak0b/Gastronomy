import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { enableAutoUnmount } from '@vue/test-utils'
import type Konva from 'konva'
import { createVuetify } from 'vuetify'
import { appTheme } from '../../../../src/shared/theme'
import { textOnScreen, waitUntil } from '../../../support/dom'
import {
  centreOf,
  dragBy,
  movePointerTo,
  pressAt,
  releaseAt,
  shapeWithId,
  shapesNamed,
  tapAt,
} from '../../../support/konvaDrawing'
import { refusal } from '../../../support/laptop'
import {
  HALL,
  SCHANK_SHAPE_ID,
  TABLE_A1,
  TABLE_A1_SHAPE_ID,
  drawingScrollArea,
  editorLaptop,
  layOutTheScreen,
  mountEditor,
  selectTheShape,
  theDrawing,
} from './tablePlanEditorFixture'
import { aGeometry, anAdminArea, anAdminTable } from '../../../support/wireViews'

enableAutoUnmount(afterEach)

function geometryOfTheNode(node: Konva.Node) {
  return { x: node.x(), y: node.y(), width: node.width(), height: node.height(), rotation: node.rotation() }
}

beforeEach(() => {
  setActivePinia(createPinia())
  document.body.innerHTML = ''
})

afterEach(() => {
  vi.unstubAllGlobals()
  vi.restoreAllMocks()
})

describe('the drawing of the selected area', () => {
  it('draws a table where the laptop keeps it', async () => {
    editorLaptop()

    await mountEditor()

    expect(geometryOfTheNode(shapeWithId(theDrawing(), TABLE_A1_SHAPE_ID))).toEqual({
      x: 20,
      y: 30,
      width: 80,
      height: 40,
      rotation: 0,
    })
  })

  it('draws a station shape where the laptop keeps it, with the station name inside', async () => {
    editorLaptop()

    await mountEditor()

    const schank = shapeWithId(theDrawing(), SCHANK_SHAPE_ID)
    expect(geometryOfTheNode(schank)).toEqual({ x: 200, y: 30, width: 120, height: 40, rotation: 0 })
    expect(schank.findOne<Konva.Text>('Text')?.text()).toBe('Schank')
  })

  it('draws the turn the laptop keeps for a table', async () => {
    const turned = anAdminTable({ geometry: aGeometry({ x: 120, y: 60, rotation: 30 }) })
    editorLaptop({ areas: [anAdminArea({ tables: [turned] })] })

    await mountEditor()

    expect(shapeWithId(theDrawing(), TABLE_A1_SHAPE_ID).rotation()).toBe(30)
  })

  it('draws a hidden table with the word hidden under its name and a dashed border', async () => {
    const hidden = anAdminTable({ name: 'A1', isHidden: true })
    editorLaptop({ areas: [anAdminArea({ tables: [hidden] })] })

    await mountEditor()

    const table = shapeWithId(theDrawing(), TABLE_A1_SHAPE_ID)
    expect(table.findOne<Konva.Text>('Text')?.text()).toBe('A1\nAusgeblendet')
    expect(table.findOne<Konva.Rect>('Rect')?.dash()).toEqual([6, 4])
  })

  it('is the fixed width and reaches below the lowest shape so there is room to drag into', async () => {
    editorLaptop()

    await mountEditor()

    expect([theDrawing().width(), theDrawing().height()]).toEqual([400, 270])
  })

  it('fills a table in the colour of the light theme', async () => {
    editorLaptop()

    await mountEditor()

    expect(shapeWithId(theDrawing(), TABLE_A1_SHAPE_ID).findOne<Konva.Rect>('Rect')?.fill()).toBe('#ebedf0')
  })

  it('draws the table again in the colour of the dark theme when the theme switches', async () => {
    editorLaptop()
    const vuetify = createVuetify({ theme: appTheme })
    await mountEditor('de', [vuetify])

    await vuetify.theme.change('dark')

    await waitUntil(() =>
      expect(shapeWithId(theDrawing(), TABLE_A1_SHAPE_ID).findOne<Konva.Rect>('Rect')?.fill()).toBe('#2e343d'),
    )
  })
})

describe('dragging a shape', () => {
  it('saves the table where it was dropped, on the grid, exactly as the drawing holds it', async () => {
    const laptop = editorLaptop()
    await mountEditor()
    const table = shapeWithId(theDrawing(), TABLE_A1_SHAPE_ID)

    await dragBy(theDrawing(), table, { x: 33, y: 117 })

    expect(laptop.writtenBodies()).toEqual([
      { areaId: 'area-halle', name: 'A1', geometry: { x: 50, y: 150, width: 80, height: 40, rotation: 0 } },
    ])
    expect(laptop.writtenBodies()).toEqual([{ areaId: 'area-halle', name: 'A1', geometry: geometryOfTheNode(table) }])
  })

  it('saves the drop in drawing units when the drawing is shown twice as large', async () => {
    layOutTheScreen({ width: 800, height: 0 })
    const laptop = editorLaptop()
    await mountEditor()

    await dragBy(theDrawing(), shapeWithId(theDrawing(), TABLE_A1_SHAPE_ID), { x: 66, y: 234 })

    expect(laptop.writtenBodies()).toEqual([
      { areaId: 'area-halle', name: 'A1', geometry: { x: 50, y: 150, width: 80, height: 40, rotation: 0 } },
    ])
  })

  it('lines the table up with the station beside it', async () => {
    const laptop = editorLaptop()
    await mountEditor()

    await dragBy(theDrawing(), shapeWithId(theDrawing(), TABLE_A1_SHAPE_ID), { x: 97, y: 0 })

    expect(laptop.writes().map((write) => write.body)).toEqual([
      { areaId: 'area-halle', name: 'A1', geometry: { x: 120, y: 30, width: 80, height: 40, rotation: 0 } },
    ])
  })

  it('shows the line it snaps to while the table is dragged and removes it on the drop', async () => {
    editorLaptop()
    await mountEditor()
    const from = centreOf(shapeWithId(theDrawing(), TABLE_A1_SHAPE_ID))
    await pressAt(theDrawing(), from)
    await movePointerTo({ x: from.x + 10, y: from.y })

    await movePointerTo({ x: from.x + 97, y: from.y })

    expect(shapesNamed(theDrawing(), 'snap-guide').map((guide) => (guide as Konva.Line).points())).toEqual([
      [200, 0, 200, 270],
      [0, 30, 400, 30],
    ])
    await releaseAt({ x: from.x + 97, y: from.y })
    expect(shapesNamed(theDrawing(), 'snap-guide')).toEqual([])
  })

  it('saves a station shape where it was dropped', async () => {
    const laptop = editorLaptop()
    await mountEditor()

    await dragBy(theDrawing(), shapeWithId(theDrawing(), SCHANK_SHAPE_ID), { x: -24, y: 93 })

    expect(laptop.writes()).toEqual([
      {
        url: '/api/admin/festivals/fest-1/stations/station-schank/shape',
        method: 'PUT',
        body: { areaId: 'area-halle', geometry: { x: 180, y: 120, width: 120, height: 40, rotation: 0 } },
      },
    ])
  })

  it('puts the table back where the laptop keeps it when the laptop refuses the move', async () => {
    editorLaptop({ writeReply: refusal('errors.admin.actionFailed', { status: 400 }) })
    await mountEditor()
    const table = shapeWithId(theDrawing(), TABLE_A1_SHAPE_ID)

    await dragBy(theDrawing(), table, { x: 33, y: 117 })

    expect([table.x(), table.y()]).toEqual([20, 30])
    expect(textOnScreen('[data-test="table-plan-drawing-column"] [data-test="refusal"]').trim()).toBe(
      'Die Aktion ist fehlgeschlagen. Versuchen Sie es noch einmal, sonst laden Sie die Seite neu.',
    )
  })
})

describe('selecting in the drawing', () => {
  it('shows the tapped table beside the drawing', async () => {
    editorLaptop()
    await mountEditor()

    await selectTheShape(TABLE_A1_SHAPE_ID)

    expect((document.querySelector('[data-test="table-name-field"] input') as HTMLInputElement).value).toBe('A1')
  })

  it('lets go of the table when the admin taps an empty spot of the drawing', async () => {
    editorLaptop()
    await mountEditor()
    await selectTheShape(TABLE_A1_SHAPE_ID)

    await tapAt(theDrawing(), { x: 350, y: 250 })

    await waitUntil(() => expect(document.querySelector('[data-test="shape-details"]')).toBeNull())
  })
})

describe('the view of a long drawing', () => {
  const FAR_DOWN = anAdminTable({ tableId: 'table-b9', name: 'B9', geometry: aGeometry({ x: 20, y: 300 }) })

  it('scrolls a table below the visible part into view when it is selected', async () => {
    layOutTheScreen({ width: 0, height: 100 })
    editorLaptop({ areas: [{ ...HALL, tables: [TABLE_A1, FAR_DOWN] }] })
    await mountEditor()

    await selectTheShape('table-table-b9')

    expect(drawingScrollArea().scrollTop).toBe(275.5)
  })

  it('stays where it is when the selected table is already in view', async () => {
    layOutTheScreen({ width: 0, height: 100 })
    editorLaptop({ areas: [{ ...HALL, tables: [TABLE_A1, FAR_DOWN] }] })
    await mountEditor()

    await selectTheShape(TABLE_A1_SHAPE_ID)

    expect(drawingScrollArea().scrollTop).toBe(0)
  })
})
