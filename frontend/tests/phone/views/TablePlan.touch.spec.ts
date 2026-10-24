import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { enableAutoUnmount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { navigate } from '../../../src/shared/router/router'
import { isOnScreen } from '../../support/dom'
import { layOutTheScreen, liftFinger, moveFingers, putFingerDown, type Finger } from '../../support/konvaDrawing'
import { HALL, TABLE_ACTIONS, mountThePlan, planLaptop, theDrawing } from './tablePlanFixture'

enableAutoUnmount(afterEach)

const ON_TISCH_12: Finger = { id: 1, at: { x: 40, y: 50 } }

const ALSO_ON_TISCH_12: Finger = { id: 2, at: { x: 80, y: 50 } }

beforeEach(() => {
  setActivePinia(createPinia())
  localStorage.clear()
  document.body.innerHTML = ''
  navigate('/')
  layOutTheScreen({ width: 400, height: 400 })
  planLaptop([HALL])
})

afterEach(() => {
  vi.restoreAllMocks()
  vi.unstubAllGlobals()
})

async function aPinchThatSpreadsTwoFingersOnTisch12() {
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

describe('a finger on the plan', () => {
  it('opens the table it taps', async () => {
    await mountThePlan()

    await putFingerDown(theDrawing(), ON_TISCH_12)
    await liftFinger(theDrawing(), ON_TISCH_12)

    expect(isOnScreen(TABLE_ACTIONS)).toBe(true)
  })

  it('pans the plan and opens nothing when it drags across a table', async () => {
    layOutTheScreen({ width: 400, height: 150 })
    await mountThePlan()
    const moved = { ...ON_TISCH_12, at: { x: 40, y: 30 } }

    await putFingerDown(theDrawing(), ON_TISCH_12)
    await moveFingers(theDrawing(), [{ ...ON_TISCH_12, at: { x: 40, y: 40 } }])
    await moveFingers(theDrawing(), [moved])
    await liftFinger(theDrawing(), moved)

    expect({ panned: theDrawing().y(), tableOpened: isOnScreen(TABLE_ACTIONS) }).toEqual({
      panned: -20,
      tableOpened: false,
    })
  })
})

describe('two fingers on the plan', () => {
  it('zoom in by the factor they spread', async () => {
    await mountThePlan()

    await aPinchThatSpreadsTwoFingersOnTisch12()

    expect(theDrawing().scaleX()).toBe(1.5)
  })

  it('open no table when the second finger lifts over the table they started on', async () => {
    await mountThePlan()
    const { first, second } = await aPinchThatSpreadsTwoFingersOnTisch12()

    await liftFinger(theDrawing(), second, [first])
    await liftFinger(theDrawing(), first)

    expect({ zoomed: theDrawing().scaleX() > 1, tableOpened: isOnScreen(TABLE_ACTIONS) }).toEqual({
      zoomed: true,
      tableOpened: false,
    })
  })

  it('leave the next tap working, even when the fingertip slips by a pixel', async () => {
    await mountThePlan()
    const stage = theDrawing()
    const left: Finger = { id: 1, at: { x: 170, y: 130 } }
    const right: Finger = { id: 2, at: { x: 230, y: 130 } }
    const spreadLeft = { ...left, at: { x: 160, y: 130 } }
    const spreadRight = { ...right, at: { x: 240, y: 130 } }
    await putFingerDown(stage, left)
    await putFingerDown(stage, right, [left])
    await moveFingers(stage, [left, right])
    await moveFingers(stage, [spreadLeft, spreadRight])
    await moveFingers(stage, [spreadLeft, spreadRight])
    await liftFinger(stage, spreadRight, [spreadLeft])
    await liftFinger(stage, spreadLeft)
    const tap: Finger = { id: 3, at: { x: 30, y: 60 } }

    await putFingerDown(stage, tap)
    await moveFingers(stage, [{ ...tap, at: { x: 31, y: 60 } }])
    await liftFinger(stage, { ...tap, at: { x: 31, y: 60 } })

    expect(isOnScreen(TABLE_ACTIONS)).toBe(true)
  })
})
