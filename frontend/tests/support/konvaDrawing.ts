import Konva from 'konva'
import { flushPromises } from '@vue/test-utils'
import { onScreen } from './dom'

interface StagePoint {
  x: number
  y: number
}

type TransformerAnchor =
  | 'top-left'
  | 'top-center'
  | 'top-right'
  | 'middle-right'
  | 'middle-left'
  | 'bottom-left'
  | 'bottom-center'
  | 'bottom-right'

const STEP_BEYOND_THE_DRAG_DISTANCE = 10

export function drawingIn(selector: string): Konva.Stage {
  const frame = onScreen(selector)
  const stage = Konva.stages.find((candidate) => frame.contains(candidate.container()))
  if (stage === undefined) {
    throw new Error(`no Konva drawing is on screen inside ${selector}`)
  }
  return stage
}

export function shapeWithId<T extends Konva.Node = Konva.Group>(stage: Konva.Stage, id: string): T {
  const found = stage.findOne<T>(`#${id}`)
  if (found === undefined) {
    throw new Error(`the drawing has no shape with the id ${id}`)
  }
  return found
}

export function hasShapeWithId(stage: Konva.Stage, id: string): boolean {
  return stage.findOne(`#${id}`) !== undefined
}

export function shapesNamed(stage: Konva.Stage, name: string): Konva.Node[] {
  return stage.find(`.${name}`)
}

export function centreOf(node: Konva.Node): StagePoint {
  const box = node.getClientRect()
  return { x: box.x + box.width / 2, y: box.y + box.height / 2 }
}

export function anchorOf(stage: Konva.Stage, anchor: TransformerAnchor): StagePoint {
  const handle = stage.findOne<Konva.Transformer>('Transformer')?.findOne(`.${anchor}`)
  if (handle === undefined) {
    throw new Error(`the drawing shows no transformer with the anchor ${anchor}`)
  }
  return handle.getAbsolutePosition()
}

function mouse(type: string, at: StagePoint): MouseEvent {
  return new MouseEvent(type, { clientX: at.x, clientY: at.y, button: 0, bubbles: true })
}

export async function tapAt(stage: Konva.Stage, at: StagePoint): Promise<void> {
  stage.draw()
  stage.content.dispatchEvent(mouse('mousedown', at))
  stage.content.dispatchEvent(mouse('mouseup', at))
  await flushPromises()
}

export async function pressAt(stage: Konva.Stage, at: StagePoint): Promise<void> {
  stage.draw()
  stage.content.dispatchEvent(mouse('mousedown', at))
  await flushPromises()
}

export async function movePointerTo(at: StagePoint): Promise<void> {
  window.dispatchEvent(mouse('mousemove', at))
  await flushPromises()
}

export async function releaseAt(at: StagePoint): Promise<void> {
  window.dispatchEvent(mouse('mouseup', at))
  await flushPromises()
}

async function dragFromBy(stage: Konva.Stage, from: StagePoint, by: StagePoint): Promise<void> {
  const to = { x: from.x + by.x, y: from.y + by.y }
  await pressAt(stage, from)
  await movePointerTo({
    x: from.x + Math.sign(by.x) * STEP_BEYOND_THE_DRAG_DISTANCE,
    y: from.y + Math.sign(by.y) * STEP_BEYOND_THE_DRAG_DISTANCE,
  })
  await movePointerTo(to)
  await releaseAt(to)
}

export async function dragBy(stage: Konva.Stage, node: Konva.Node, by: StagePoint): Promise<void> {
  await dragFromBy(stage, centreOf(node), by)
}

export async function dragAnchorBy(
  stage: Konva.Stage,
  anchor: TransformerAnchor,
  by: StagePoint,
): Promise<void> {
  await dragFromBy(stage, anchorOf(stage, anchor), by)
}
