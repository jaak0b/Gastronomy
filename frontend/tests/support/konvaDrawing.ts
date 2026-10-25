import Konva from 'konva'
import { vi } from 'vitest'
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

interface PointerModifiers {
  ctrlKey?: boolean
  shiftKey?: boolean
  button?: number
}

function mouse(type: string, at: StagePoint, modifiers: PointerModifiers = {}): MouseEvent {
  return new MouseEvent(type, {
    clientX: at.x,
    clientY: at.y,
    button: modifiers.button ?? 0,
    bubbles: true,
    ctrlKey: modifiers.ctrlKey ?? false,
    shiftKey: modifiers.shiftKey ?? false,
  })
}

export async function tapAt(
  stage: Konva.Stage,
  at: StagePoint,
  modifiers: PointerModifiers = {},
): Promise<void> {
  stage.draw()
  stage.content.dispatchEvent(mouse('mousedown', at, modifiers))
  stage.content.dispatchEvent(mouse('mouseup', at, modifiers))
  await flushPromises()
}

export async function rightClickAt(stage: Konva.Stage, at: StagePoint): Promise<void> {
  stage.draw()
  stage.content.dispatchEvent(mouse('mousedown', at, { button: 2 }))
  stage.content.dispatchEvent(
    new MouseEvent('contextmenu', {
      clientX: at.x,
      clientY: at.y,
      button: 2,
      bubbles: true,
      cancelable: true,
    }),
  )
  stage.content.dispatchEvent(mouse('mouseup', at, { button: 2 }))
  await flushPromises()
}

export async function pressAt(
  stage: Konva.Stage,
  at: StagePoint,
  modifiers: PointerModifiers = {},
): Promise<void> {
  stage.draw()
  stage.content.dispatchEvent(mouse('mousedown', at, modifiers))
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

export async function releaseOnTheDrawingAt(stage: Konva.Stage, at: StagePoint): Promise<void> {
  stage.content.dispatchEvent(mouse('mouseup', at))
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

export function layOutTheScreen(size: { width: number; height: number }): void {
  vi.spyOn(Element.prototype, 'clientWidth', 'get').mockReturnValue(size.width)
  vi.spyOn(Element.prototype, 'clientHeight', 'get').mockReturnValue(size.height)
}

export interface Finger {
  id: number
  at: StagePoint
}

function touch(type: string, touches: Finger[], changedTouches: Finger[]): Event {
  const event = new Event(type, { bubbles: true, cancelable: true })
  Object.defineProperties(event, {
    touches: { value: touches.map((finger) => ({ identifier: finger.id, clientX: finger.at.x, clientY: finger.at.y })) },
    changedTouches: {
      value: changedTouches.map((finger) => ({ identifier: finger.id, clientX: finger.at.x, clientY: finger.at.y })),
    },
  })
  return event
}

export async function putFingerDown(stage: Konva.Stage, finger: Finger, alreadyDown: Finger[] = []): Promise<void> {
  stage.draw()
  stage.content.dispatchEvent(touch('touchstart', [...alreadyDown, finger], [finger]))
  await flushPromises()
}

export async function moveFingers(stage: Konva.Stage, fingers: Finger[]): Promise<void> {
  stage.content.dispatchEvent(touch('touchmove', fingers, fingers))
  await flushPromises()
}

export async function liftFinger(stage: Konva.Stage, finger: Finger, stillDown: Finger[] = []): Promise<void> {
  stage.content.dispatchEvent(touch('touchend', stillDown, [finger]))
  await flushPromises()
}

function pointer(type: string, pointerType: 'touch' | 'mouse', id: number, at: StagePoint): PointerEvent {
  return new PointerEvent(type, {
    pointerId: id,
    pointerType,
    isPrimary: true,
    clientX: at.x,
    clientY: at.y,
    button: 0,
    bubbles: true,
    cancelable: true,
  })
}

function whatLiesOnTopOf(stage: Konva.Stage): Element {
  return document.querySelector('.v-overlay--active .v-overlay__scrim') ?? stage.content
}

export async function tapAsAPhoneDoes(stage: Konva.Stage, finger: Finger): Promise<void> {
  const slipped = { ...finger, at: { x: finger.at.x + 1, y: finger.at.y + 1 } }
  stage.draw()
  stage.content.dispatchEvent(pointer('pointerdown', 'touch', finger.id, finger.at))
  stage.content.dispatchEvent(touch('touchstart', [finger], [finger]))
  stage.content.dispatchEvent(pointer('pointermove', 'touch', finger.id, slipped.at))
  stage.content.dispatchEvent(touch('touchmove', [slipped], [slipped]))
  stage.content.dispatchEvent(pointer('pointerup', 'touch', finger.id, slipped.at))
  const lifted = touch('touchend', [], [slipped])
  stage.content.dispatchEvent(lifted)
  await flushPromises()
  if (lifted.defaultPrevented) {
    return
  }
  const underTheFinger = whatLiesOnTopOf(stage)
  underTheFinger.dispatchEvent(mouse('mousedown', slipped.at))
  underTheFinger.dispatchEvent(mouse('mouseup', slipped.at))
  underTheFinger.dispatchEvent(mouse('click', slipped.at))
  await flushPromises()
}

export async function clickAsAMouseDoes(stage: Konva.Stage, at: StagePoint): Promise<void> {
  stage.draw()
  stage.content.dispatchEvent(pointer('pointerdown', 'mouse', 1, at))
  stage.content.dispatchEvent(mouse('mousedown', at))
  stage.content.dispatchEvent(pointer('pointerup', 'mouse', 1, at))
  stage.content.dispatchEvent(mouse('mouseup', at))
  await flushPromises()
  stage.content.dispatchEvent(mouse('click', at))
  await flushPromises()
}
