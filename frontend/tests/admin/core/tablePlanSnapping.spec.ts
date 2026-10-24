import { describe, expect, it } from 'vitest'
import {
  snapDraggedBox,
  snapResizeCorner,
  snapStopsBeside,
} from '../../../src/admin/core/tablePlanSnapping'

const TABLE_A1 = { x: 20, y: 30, width: 80, height: 40 }

const BAR = { x: 200, y: 30, width: 120, height: 40 }

describe('the lines a shape can snap to', () => {
  it('are the edges and the middle of the drawing and the edges and centres of the other shapes', () => {
    expect(snapStopsBeside([TABLE_A1])).toEqual({
      vertical: [0, 200, 400, 20, 60, 100],
      horizontal: [0, 30, 50, 70],
    })
  })

  it('are only the edges of the drawing when no other shape is drawn', () => {
    expect(snapStopsBeside([])).toEqual({ vertical: [0, 200, 400], horizontal: [0] })
  })
})

describe('a shape the admin drags', () => {
  it('lands on the grid when no other shape is near', () => {
    const snapped = snapDraggedBox({ x: 33, y: 147, width: 60, height: 40 }, snapStopsBeside([]))

    expect(snapped).toEqual({ x: 30, y: 150, guides: [] })
  })

  it('lines its left edge up with the right edge of a table close by and shows that line', () => {
    const snapped = snapDraggedBox({ x: 103, y: 147, width: 60, height: 40 }, snapStopsBeside([TABLE_A1]))

    expect(snapped).toEqual({ x: 100, y: 150, guides: [{ orientation: 'vertical', position: 100 }] })
  })

  it('lines its centre up with the centre of a table close by', () => {
    const snapped = snapDraggedBox({ x: 38, y: 147, width: 40, height: 40 }, snapStopsBeside([TABLE_A1]))

    expect(snapped).toEqual({ x: 40, y: 150, guides: [{ orientation: 'vertical', position: 60 }] })
  })

  it('lines its top up with the bottom of a table close by and shows that line', () => {
    const snapped = snapDraggedBox({ x: 143, y: 74, width: 40, height: 40 }, snapStopsBeside([TABLE_A1]))

    expect(snapped).toEqual({ x: 140, y: 70, guides: [{ orientation: 'horizontal', position: 70 }] })
  })

  it('takes the nearer of two lines it comes close to', () => {
    const snapped = snapDraggedBox({ x: 104, y: 147, width: 94, height: 40 }, snapStopsBeside([TABLE_A1, BAR]))

    expect(snapped).toEqual({ x: 106, y: 150, guides: [{ orientation: 'vertical', position: 200 }] })
  })

  it('lines its centre up with the middle of the drawing', () => {
    const snapped = snapDraggedBox({ x: 157, y: 147, width: 80, height: 40 }, snapStopsBeside([]))

    expect(snapped).toEqual({ x: 160, y: 150, guides: [{ orientation: 'vertical', position: 200 }] })
  })

  it('stays inside the left edge of the drawing', () => {
    const snapped = snapDraggedBox({ x: -14, y: 147, width: 80, height: 40 }, snapStopsBeside([]))

    expect(snapped).toEqual({ x: 0, y: 150, guides: [] })
  })

  it('stays inside the right edge of the drawing', () => {
    const snapped = snapDraggedBox({ x: 335, y: 147, width: 80, height: 40 }, snapStopsBeside([]))

    expect(snapped).toEqual({ x: 320, y: 150, guides: [] })
  })

  it('stays below the top of the drawing', () => {
    const snapped = snapDraggedBox({ x: 33, y: -12, width: 80, height: 40 }, snapStopsBeside([]))

    expect(snapped).toEqual({ x: 30, y: 0, guides: [] })
  })

  it('shows no line it was pushed off to stay inside the drawing', () => {
    const corner = { x: 380, y: 300, width: 20, height: 20 }

    const snapped = snapDraggedBox({ x: 398, y: 147, width: 40, height: 40 }, snapStopsBeside([corner]))

    expect(snapped).toEqual({ x: 360, y: 150, guides: [] })
  })
})

describe('the corner the admin drags to resize a shape', () => {
  it('lands on the grid when no other shape is near', () => {
    expect(snapResizeCorner({ x: 127, y: 83 }, snapStopsBeside([]))).toEqual({ x: 130, y: 80, guides: [] })
  })

  it('lines up with the edges of a station close by and shows both lines', () => {
    expect(snapResizeCorner({ x: 197, y: 72 }, snapStopsBeside([BAR]))).toEqual({
      x: 200,
      y: 70,
      guides: [
        { orientation: 'vertical', position: 200 },
        { orientation: 'horizontal', position: 70 },
      ],
    })
  })
})
