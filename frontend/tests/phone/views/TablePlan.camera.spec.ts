import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { enableAutoUnmount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { navigate } from '../../../src/shared/router/router'
import { isOnScreen } from '../../support/dom'
import { dragBy, layOutTheScreen } from '../../support/konvaDrawing'
import { HALL, mountThePlan, planLaptop, tableNode, theDrawing } from './tablePlanFixture'

enableAutoUnmount(afterEach)

beforeEach(() => {
  setActivePinia(createPinia())
  localStorage.clear()
  document.body.innerHTML = ''
  navigate('/')
})

afterEach(() => {
  vi.restoreAllMocks()
  vi.unstubAllGlobals()
})

function theCamera() {
  return { scale: theDrawing().scaleX(), left: theDrawing().x(), top: theDrawing().y() }
}

function aFrameThatCanBeResized() {
  const measureAgain: ResizeObserverCallback[] = []
  vi.stubGlobal(
    'ResizeObserver',
    class {
      constructor(callback: ResizeObserverCallback) {
        measureAgain.push(callback)
      }

      observe(): void {}

      unobserve(): void {}

      disconnect(): void {}
    },
  )
  return {
    async resizeTo(size: { width: number; height: number }): Promise<void> {
      layOutTheScreen(size)
      measureAgain.forEach((callback) => callback([], {} as ResizeObserver))
      await flushPromises()
    },
  }
}

describe('the plan on a phone, held upright or sideways', () => {
  it.each([
    [844, 2.11],
    [900, 2.25],
    [932, 2.33],
    [959, 2.3975],
  ])('fills the width of a screen %i pixels wide and starts at the top', async (width, scale) => {
    layOutTheScreen({ width, height: 412 })
    planLaptop([HALL])

    await mountThePlan()

    expect(theCamera()).toEqual({ scale, left: 0, top: 0 })
  })
})

describe('the plan on a screen wider than a phone', () => {
  it('shows the whole plan in the middle of the screen', async () => {
    layOutTheScreen({ width: 1200, height: 520 })
    planLaptop([HALL])

    await mountThePlan()

    expect({ ...theCamera(), scrollBarShown: isOnScreen('[data-test="plan-scroll-bar"]') }).toEqual({
      scale: 2,
      left: 200,
      top: 0,
      scrollBarShown: false,
    })
  })

  it('shows the whole plan from a screen 960 pixels wide on', async () => {
    layOutTheScreen({ width: 960, height: 520 })
    planLaptop([HALL])

    await mountThePlan()

    expect(theCamera()).toEqual({ scale: 2, left: 80, top: 0 })
  })
})

describe('the plan when the frame under the header changes its height', () => {
  it('shows the whole plan again on a screen wider than a phone', async () => {
    const frame = aFrameThatCanBeResized()
    layOutTheScreen({ width: 1200, height: 520 })
    planLaptop([HALL])
    await mountThePlan()

    await frame.resizeTo({ width: 1200, height: 260 })

    expect(theCamera()).toEqual({ scale: 1, left: 400, top: 0 })
  })

  it('keeps the place the waiter panned to on a phone', async () => {
    const frame = aFrameThatCanBeResized()
    layOutTheScreen({ width: 400, height: 150 })
    planLaptop([HALL])
    await mountThePlan()
    await dragBy(theDrawing(), tableNode('table-12'), { x: 0, y: -60 })

    await frame.resizeTo({ width: 400, height: 120 })

    expect(theCamera()).toEqual({ scale: 1, left: 0, top: -60 })
  })
})
