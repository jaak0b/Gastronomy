import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { enableAutoUnmount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { navigate } from '../../../src/shared/router/router'
import { isOnScreen, onScreen } from '../../support/dom'
import { dragBy, layOutTheScreen } from '../../support/konvaDrawing'
import {
  HALL,
  aPinchThatSpreadsTwoFingersOnTisch12,
  mountThePlan,
  planLaptop,
  tableNode,
  theDrawing,
} from './tablePlanFixture'

enableAutoUnmount(afterEach)

const VERTICAL_BAR = '[data-test="plan-scroll-bar"][data-test-id="vertical"]'

const HORIZONTAL_BAR = '[data-test="plan-scroll-bar"][data-test-id="horizontal"]'

beforeEach(() => {
  setActivePinia(createPinia())
  localStorage.clear()
  document.body.innerHTML = ''
  navigate('/')
  planLaptop([HALL])
})

afterEach(() => {
  vi.restoreAllMocks()
  vi.unstubAllGlobals()
})

describe('the scroll bars of the waiter\'s plan', () => {
  it('show no bar while the whole area fits on the screen', async () => {
    layOutTheScreen({ width: 400, height: 400 })

    await mountThePlan()

    expect({ vertical: isOnScreen(VERTICAL_BAR), horizontal: isOnScreen(HORIZONTAL_BAR) }).toEqual({
      vertical: false,
      horizontal: false,
    })
  })

  it('show only the vertical bar, as long as the share on screen, for an area twice as long as the screen', async () => {
    layOutTheScreen({ width: 400, height: 130 })

    await mountThePlan()

    expect({
      top: onScreen(VERTICAL_BAR).style.top,
      height: onScreen(VERTICAL_BAR).style.height,
      horizontal: isOnScreen(HORIZONTAL_BAR),
    }).toEqual({ top: '0px', height: '65px', horizontal: false })
  })

  it('move the vertical bar down as the waiter pans the plan up', async () => {
    layOutTheScreen({ width: 400, height: 130 })
    await mountThePlan()

    await dragBy(theDrawing(), tableNode('table-12'), { x: 0, y: -60 })

    expect(onScreen(VERTICAL_BAR).style.top).toBe('30px')
  })

  it('show the horizontal bar once two fingers zoom in', async () => {
    layOutTheScreen({ width: 400, height: 400 })
    await mountThePlan()

    await aPinchThatSpreadsTwoFingersOnTisch12()

    expect(isOnScreen(HORIZONTAL_BAR)).toBe(true)
  })
})
