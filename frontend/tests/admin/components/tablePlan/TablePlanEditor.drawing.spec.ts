import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { enableAutoUnmount } from '@vue/test-utils'
import Konva from 'konva'
import { createVuetify } from 'vuetify'
import { appTheme } from '../../../../src/shared/theme'
import { HEAVY_WAIT_TIMEOUT, isOnScreen, textOnScreen, waitUntil } from '../../../support/dom'
import {
  aFrameThatCanBeResized,
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
  EDITOR_TEST_TIMEOUT,
  HALL,
  MOVE_PATH,
  SCHANK_SHAPE_ID,
  TABLE_A1,
  TABLE_A1_SHAPE_ID,
  drawingScrollArea,
  editorLaptop,
  isDisabledInTheToolbar,
  isHighlighted,
  mountEditor,
  pressInTheToolbar,
  pressInTheToolbarRepeatedly,
  scrollLikeABrowser,
  selectTheShape,
  shapesWithTheHandles,
  theDrawing,
} from './tablePlanEditorFixture'
import { aGeometry, anAdminArea, anAdminTable } from '../../../support/wireViews'

enableAutoUnmount(afterEach)

vi.setConfig({ testTimeout: EDITOR_TEST_TIMEOUT })

function geometryOfTheNode(node: Konva.Node) {
  return { x: node.x(), y: node.y(), width: node.width(), height: node.height(), rotation: node.rotation() }
}

beforeEach(() => {
  setActivePinia(createPinia())
  localStorage.clear()
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

  it('draws the width of the plan across a third of the frame when the editor opens', async () => {
    editorLaptop()

    await mountEditor('de', [], HALL.areaId, { width: 1800, height: 1000 })

    expect([theDrawing().width(), theDrawing().scaleX()]).toEqual([612, 1.5])
  })

  it('ends 200 units below the lowest shape instead of filling a taller frame', async () => {
    editorLaptop()

    await mountEditor('de', [], HALL.areaId, { width: 1800, height: 1000 })

    expect(theDrawing().height()).toBe(417)
  })

  it('keeps its length in a frame lower than the drawing, so the frame scrolls', async () => {
    editorLaptop()

    await mountEditor('de', [], HALL.areaId, { width: 1800, height: 300 })

    expect([theDrawing().height(), theDrawing().scaleX()]).toEqual([417, 1.5])
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
    await mountEditor('de', [], HALL.areaId, { width: 1800, height: 417 })
    const from = centreOf(shapeWithId(theDrawing(), TABLE_A1_SHAPE_ID))
    await pressAt(theDrawing(), from)
    await movePointerTo({ x: from.x + 10, y: from.y })

    await movePointerTo({ x: from.x + 145.5, y: from.y })

    expect(shapesNamed(theDrawing(), 'snap-guide').map((guide) => (guide as Konva.Line).points())).toEqual([
      [200, 0, 200, 270],
      [0, 30, 400, 30],
    ])
    await releaseAt({ x: from.x + 145.5, y: from.y })
    await waitUntil(() => expect(shapesNamed(theDrawing(), 'snap-guide')).toEqual([]), HEAVY_WAIT_TIMEOUT)
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
    editorLaptop({ writeReply: refusal('common.errors.actionFailed', { status: 400 }) })
    await mountEditor()
    const table = shapeWithId(theDrawing(), TABLE_A1_SHAPE_ID)

    await dragBy(theDrawing(), table, { x: 49.5, y: 175.5 })

    expect([table.x(), table.y()]).toEqual([20, 30])
    expect(textOnScreen('[data-test="plan-refusal"]').trim()).toBe(
      'Die Aktion ist fehlgeschlagen.',
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
    await waitUntil(() => expect(shapesWithTheHandles()).toEqual([]), HEAVY_WAIT_TIMEOUT)
  })
})

describe('the view of a long drawing', () => {
  const FAR_DOWN = anAdminTable({ tableId: 'table-b9', name: 'B9', geometry: aGeometry({ x: 20, y: 300 }) })

  it('scrolls a table below the visible part into view when it is selected', async () => {
    editorLaptop({ areas: [{ ...HALL, tables: [TABLE_A1, FAR_DOWN] }] })
    await mountEditor('de', [], HALL.areaId, { width: 1800, height: 150 })

    await selectTheShape('table-table-b9')

    expect(drawingScrollArea().scrollTop).toBe(432)
  })

  it('stays where it is when the selected table is already in view', async () => {
    editorLaptop({ areas: [{ ...HALL, tables: [TABLE_A1, FAR_DOWN] }] })
    await mountEditor('de', [], HALL.areaId, { width: 1800, height: 150 })

    await selectTheShape(TABLE_A1_SHAPE_ID)

    expect(drawingScrollArea().scrollTop).toBe(0)
  })
})

describe('zooming the drawing', () => {
  it('draws the plan a quarter larger when the admin zooms in', async () => {
    editorLaptop()
    await mountEditor()

    await pressInTheToolbar('zoom-in')

    expect([theDrawing().width(), theDrawing().scaleX()]).toEqual([765, 1.875])
  })

  it('draws the plan smaller again when the admin zooms out', async () => {
    editorLaptop()
    await mountEditor()

    await pressInTheToolbar('zoom-out')

    expect(theDrawing().scaleX()).toBeCloseTo(1.2)
  })

  it('stops zooming out once the plan is about a sixth of the frame wide', async () => {
    editorLaptop()
    await mountEditor()

    await pressInTheToolbarRepeatedly('zoom-out', 3)

    expect([isDisabledInTheToolbar('zoom-out'), isDisabledInTheToolbar('zoom-in')]).toEqual([true, false])
  })

  it('stops zooming in once the plan is about two frames wide', async () => {
    editorLaptop()
    await mountEditor()

    await pressInTheToolbarRepeatedly('zoom-in', 8)

    expect([isDisabledInTheToolbar('zoom-in'), isDisabledInTheToolbar('zoom-out')]).toEqual([true, false])
  })

  it('saves a table dragged after zooming in at its place in plan units', async () => {
    const laptop = editorLaptop()
    await mountEditor()
    await pressInTheToolbar('zoom-in')

    await dragBy(theDrawing(), shapeWithId(theDrawing(), TABLE_A1_SHAPE_ID), { x: 75, y: 187.5 })

    expect(laptop.writtenBodies()).toEqual([
      { tables: [{ tableId: 'table-a1', geometry: { x: 60, y: 130, width: 80, height: 40, rotation: 0 } }] },
    ])
  })
})

describe('the view while the admin zooms or the window changes', () => {
  const FAR_BELOW = anAdminTable({ tableId: 'table-b9', name: 'B9', geometry: aGeometry({ x: 20, y: 1000 }) })

  it('keeps the point in the middle of the view in the middle when the admin zooms out far down the plan', async () => {
    editorLaptop({ areas: [{ ...HALL, tables: [TABLE_A1, FAR_BELOW] }] })
    await mountEditor('de', [], HALL.areaId, { width: 1800, height: 300 })
    const scroller = scrollLikeABrowser()
    scroller.scrollTop = 1400

    await pressInTheToolbar('zoom-out')

    expect(scroller.scrollTop).toBeCloseTo(1090)
  })

  it('keeps the point in the middle of the view in the middle when the zoomed window gets wider', async () => {
    const frame = aFrameThatCanBeResized()
    editorLaptop({ areas: [{ ...HALL, tables: [TABLE_A1, FAR_BELOW] }] })
    await mountEditor('de', [], HALL.areaId, { width: 1800, height: 300 })
    const scroller = scrollLikeABrowser()
    await pressInTheToolbar('zoom-in')
    scroller.scrollTop = 600

    await frame.resizeTo({ width: 2400, height: 300 })

    expect(scroller.scrollTop).toBeCloseTo(850)
  })

  it('brings a selected table at the left edge back into view when the zoomed plan is scrolled right', async () => {
    editorLaptop()
    await mountEditor()
    const scroller = scrollLikeABrowser()
    await pressInTheToolbarRepeatedly('zoom-in', 8)
    scroller.scrollLeft = 1800
    scroller.scrollTop = 0

    await selectTheShape(TABLE_A1_SHAPE_ID)

    expect(scroller.scrollLeft).toBeCloseTo(190.58, 2)
  })

  it('draws nothing while the frame has no width yet', async () => {
    aFrameThatCanBeResized()
    editorLaptop()

    await mountEditor('de', [], HALL.areaId, { width: 0, height: 1000 })

    expect(isOnScreen('.konvajs-content')).toBe(false)
  })

  it('draws the plan at a third of the frame once the frame has its width', async () => {
    const frame = aFrameThatCanBeResized()
    editorLaptop()
    await mountEditor('de', [], HALL.areaId, { width: 0, height: 1000 })

    await frame.resizeTo({ width: 1800, height: 1000 })

    expect(theDrawing().scaleX()).toBe(1.5)
  })
})
