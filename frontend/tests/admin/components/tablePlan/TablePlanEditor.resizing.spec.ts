import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { enableAutoUnmount } from '@vue/test-utils'
import type Konva from 'konva'
import { textOnScreen } from '../../../support/dom'
import { anchorOf, dragAnchorBy, shapeWithId } from '../../../support/konvaDrawing'
import { refusal } from '../../../support/laptop'
import {
  SCHANK_SHAPE_ID,
  TABLE_A1_SHAPE_ID,
  editorLaptop,
  mountEditor,
  selectTheShape,
  theDrawing,
} from './tablePlanEditorFixture'

enableAutoUnmount(afterEach)

interface SavedShape {
  geometry: { x: number; y: number; width: number; height: number; rotation: number }
}

function roundedValuesOf(saved: SavedShape): number[] {
  return Object.values(saved.geometry).map((value) => Math.round(value))
}

function geometryOfTheNode(node: Konva.Node) {
  return { x: node.x(), y: node.y(), width: node.width(), height: node.height(), rotation: node.rotation() }
}

beforeEach(() => {
  setActivePinia(createPinia())
  document.body.innerHTML = ''
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('resizing a table', () => {
  it('saves the new width and height in drawing units and keeps the name at its size', async () => {
    const laptop = editorLaptop()
    await mountEditor()
    await selectTheShape(TABLE_A1_SHAPE_ID)
    const table = shapeWithId(theDrawing(), TABLE_A1_SHAPE_ID)

    await dragAnchorBy(theDrawing(), 'bottom-right', { x: 40.5, y: 19.5 })

    const saved = laptop.writtenBodies()[0] as SavedShape
    expect(roundedValuesOf(saved)).toEqual([20, 30, 110, 50, 0])
    expect(saved.geometry).toEqual(geometryOfTheNode(table))
    expect([table.scaleX(), table.scaleY(), table.findOne<Konva.Text>('Text')?.width()]).toEqual([1, 1, table.width()])
  })

  it('keeps a table at least 20 units wide when the admin drags its side too far', async () => {
    const laptop = editorLaptop()
    await mountEditor()
    await selectTheShape(TABLE_A1_SHAPE_ID)

    await dragAnchorBy(theDrawing(), 'bottom-right', { x: -112.5, y: 0 })

    expect(roundedValuesOf(laptop.writtenBodies()[0] as SavedShape)).toEqual([20, 30, 70, 40, 0])
  })

  it('keeps a table inside the right edge of the drawing', async () => {
    const laptop = editorLaptop()
    await mountEditor()
    await selectTheShape(TABLE_A1_SHAPE_ID)

    await dragAnchorBy(theDrawing(), 'middle-right', { x: 700, y: 0 })

    expect(roundedValuesOf(laptop.writtenBodies()[0] as SavedShape)).toEqual([20, 30, 90, 40, 0])
  })

  it('puts the table and its handles back when the laptop refuses the new size', async () => {
    editorLaptop({ writeReply: refusal('errors.admin.actionFailed', { status: 400 }) })
    await mountEditor()
    await selectTheShape(TABLE_A1_SHAPE_ID)
    const table = shapeWithId(theDrawing(), TABLE_A1_SHAPE_ID)

    await dragAnchorBy(theDrawing(), 'bottom-right', { x: 40.5, y: 19.5 })

    expect(geometryOfTheNode(table)).toEqual({ x: 20, y: 30, width: 80, height: 40, rotation: 0 })
    expect(anchorOf(theDrawing(), 'bottom-right')).toEqual({ x: 156, y: 111 })
    expect(textOnScreen('[data-test="table-plan-drawing-column"] [data-test="refusal"]').trim()).toBe(
      'Die Aktion ist fehlgeschlagen. Versuchen Sie es noch einmal, sonst laden Sie die Seite neu.',
    )
  })
})

describe('resizing a station shape', () => {
  it('saves its new size on the station', async () => {
    const laptop = editorLaptop()
    await mountEditor()
    await selectTheShape(SCHANK_SHAPE_ID)

    await dragAnchorBy(theDrawing(), 'bottom-right', { x: 25.5, y: 34.5 })

    const written = laptop.writes()[0]
    expect(written.url).toBe('/api/admin/festivals/fest-1/stations/station-schank/shape')
    expect(roundedValuesOf(written.body as SavedShape)).toEqual([200, 30, 140, 60, 0])
  })
})
