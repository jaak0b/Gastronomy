import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { enableAutoUnmount } from '@vue/test-utils'
import Konva from 'konva'
import { createVuetify } from 'vuetify'
import { appTheme } from '../../../../src/shared/theme'
import { textOnScreen, waitUntil } from '../../../support/dom'
import {
  centreOf,
  dragBy,
  hasShapeWithId,
  movePointerTo,
  pressAt,
  releaseAt,
  shapesNamed,
  shapeWithId,
  tapAt,
} from '../../../support/konvaDrawing'
import { refusal } from '../../../support/laptop'
import {
  HALL,
  MOVE_PATH,
  SCHANK_SHAPE_ID,
  TABLE_A1,
  TABLE_A1_SHAPE_ID,
  drawingScrollArea,
  editorLaptop,
  isHighlighted,
  mountEditor,
  selectTheShape,
  shapesWithTheHandles,
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

describe('the drawing of the area', () => {
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

  it('never draws a hidden table, while the shown one beside it stays', async () => {
    const hidden = anAdminTable({ tableId: 'table-a2', name: 'A2', isHidden: true, geometry: aGeometry({ y: 100 }) })
    editorLaptop({ areas: [{ ...HALL, tables: [TABLE_A1, hidden] }] })

    await mountEditor()

    expect([hasShapeWithId(theDrawing(), TABLE_A1_SHAPE_ID), hasShapeWithId(theDrawing(), 'table-table-a2')]).toEqual([
      true,
      false,
    ])
  })

  it('fits the width of a tall window and fills its height below the shapes', async () => {
    editorLaptop()

    await mountEditor('de', [], HALL.areaId, { width: 612, height: 1000 })

    expect([theDrawing().width(), theDrawing().height(), theDrawing().scaleX()]).toEqual([612, 1000, 1.5])
  })

  it('fits the height of a wide window, so the whole area shows without scrolling', async () => {
    editorLaptop()

    await mountEditor('de', [], HALL.areaId, { width: 1600, height: 556 })

    expect([theDrawing().width(), theDrawing().height(), theDrawing().scaleX()]).toEqual([816, 556, 2])
  })

  it('stops shrinking at half its units in a very small window and scrolls instead', async () => {
    editorLaptop()

    await mountEditor('de', [], HALL.areaId, { width: 100, height: 100 })

    expect([theDrawing().width(), theDrawing().scaleX()]).toEqual([204, 0.5])
  })

  it('keeps the whole border of a table at the top left edge on screen', async () => {
    const atTheEdge = anAdminTable({ geometry: aGeometry({ x: 0, y: 0 }) })
    editorLaptop({ areas: [anAdminArea({ tables: [atTheEdge] })] })

    await mountEditor()

    const border = shapeWithId(theDrawing(), TABLE_A1_SHAPE_ID).getClientRect()
    expect([border.x, border.y]).toEqual([5.25, 5.25])
  })

  it('never breaks the name of a narrow table inside a word, as the phone does', async () => {
    const narrow = anAdminTable({ name: 'Abcd', geometry: aGeometry({ width: 30, height: 80 }) })
    editorLaptop({ areas: [anAdminArea({ tables: [narrow] })] })

    await mountEditor()

    const name = shapeWithId(theDrawing(), TABLE_A1_SHAPE_ID).findOne<Konva.Text>('Text')
    expect(name?.textArr.map((line) => line.text)).toEqual(['Abcd'])
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

describe('the drawing once the editor closes', () => {
  it('lets go of the text it measured the table names with', async () => {
    const destroyedTexts: Konva.Text[] = []
    const destroy = Konva.Text.prototype.destroy
    vi.spyOn(Konva.Text.prototype, 'destroy').mockImplementation(function (this: Konva.Text) {
      destroyedTexts.push(this)
      return destroy.call(this)
    })
    editorLaptop()
    const editor = await mountEditor()

    editor.unmount()

    expect(destroyedTexts.filter((text) => text.text() === '')).toHaveLength(1)
  })
})

describe('dragging a shape', () => {
  it('saves the table where it was dropped, in drawing units on the grid, exactly as the drawing holds it', async () => {
    const laptop = editorLaptop()
    await mountEditor()
    const table = shapeWithId(theDrawing(), TABLE_A1_SHAPE_ID)

    await dragBy(theDrawing(), table, { x: 49.5, y: 175.5 })

    expect(laptop.writes()).toEqual([
      {
        url: MOVE_PATH,
        method: 'POST',
        body: { tables: [{ tableId: 'table-a1', geometry: { x: 50, y: 150, width: 80, height: 40, rotation: 0 } }] },
      },
    ])
    expect(laptop.writtenBodies()).toEqual([{ tables: [{ tableId: 'table-a1', geometry: geometryOfTheNode(table) }] }])
  })

  it('lines the table up with the station beside it', async () => {
    const laptop = editorLaptop()
    await mountEditor()

    await dragBy(theDrawing(), shapeWithId(theDrawing(), TABLE_A1_SHAPE_ID), { x: 145.5, y: 0 })

    expect(laptop.writtenBodies()).toEqual([
      { tables: [{ tableId: 'table-a1', geometry: { x: 120, y: 30, width: 80, height: 40, rotation: 0 } }] },
    ])
  })

  it('shows the line it snaps to while the table is dragged and removes it on the drop', async () => {
    editorLaptop()
    await mountEditor('de', [], HALL.areaId, { width: 612, height: 417 })
    const from = centreOf(shapeWithId(theDrawing(), TABLE_A1_SHAPE_ID))
    await pressAt(theDrawing(), from)
    await movePointerTo({ x: from.x + 10, y: from.y })

    await movePointerTo({ x: from.x + 145.5, y: from.y })

    expect(shapesNamed(theDrawing(), 'snap-guide').map((guide) => (guide as Konva.Line).points())).toEqual([
      [200, 0, 200, 270],
      [0, 30, 400, 30],
    ])
    await releaseAt({ x: from.x + 145.5, y: from.y })
    expect(shapesNamed(theDrawing(), 'snap-guide')).toEqual([])
  })

  it('saves a station shape where it was dropped', async () => {
    const laptop = editorLaptop()
    await mountEditor()

    await dragBy(theDrawing(), shapeWithId(theDrawing(), SCHANK_SHAPE_ID), { x: -36, y: 139.5 })

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

    await dragBy(theDrawing(), table, { x: 49.5, y: 175.5 })

    expect([table.x(), table.y()]).toEqual([20, 30])
    expect(textOnScreen('[data-test="plan-refusal"]').trim()).toBe(
      'Die Aktion ist fehlgeschlagen. Versuchen Sie es noch einmal, sonst laden Sie die Seite neu.',
    )
  })
})

describe('selecting in the drawing', () => {
  it('puts the resize handles on the tapped table and highlights it', async () => {
    editorLaptop()
    await mountEditor()

    await selectTheShape(TABLE_A1_SHAPE_ID)

    expect(shapesWithTheHandles()).toEqual([TABLE_A1_SHAPE_ID])
    expect(isHighlighted('table-table-a1')).toBe(true)
  })

  it('lets go of the table when the admin taps an empty spot of the drawing', async () => {
    editorLaptop()
    await mountEditor()
    await selectTheShape(TABLE_A1_SHAPE_ID)

    await tapAt(theDrawing(), { x: 525, y: 375 })

    await waitUntil(() => expect(isHighlighted('table-table-a1')).toBe(false))
    expect(shapesWithTheHandles()).toEqual([])
  })
})

describe('the view of a long drawing', () => {
  const FAR_DOWN = anAdminTable({ tableId: 'table-b9', name: 'B9', geometry: aGeometry({ x: 20, y: 300 }) })

  it('scrolls a table below the visible part into view when it is selected', async () => {
    editorLaptop({ areas: [{ ...HALL, tables: [TABLE_A1, FAR_DOWN] }] })
    await mountEditor('de', [], HALL.areaId, { width: 612, height: 150 })

    await selectTheShape('table-table-b9')

    expect(drawingScrollArea().scrollTop).toBe(128)
  })

  it('stays where it is when the selected table is already in view', async () => {
    editorLaptop({ areas: [{ ...HALL, tables: [TABLE_A1, FAR_DOWN] }] })
    await mountEditor('de', [], HALL.areaId, { width: 612, height: 150 })

    await selectTheShape(TABLE_A1_SHAPE_ID)

    expect(drawingScrollArea().scrollTop).toBe(0)
  })
})
