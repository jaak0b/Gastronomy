import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { defineComponent, h, nextTick, ref } from 'vue'
import { useDockedStripTapGuard } from '../../../src/phone/composables/useDockedStripTapGuard'
import { onScreen, textOnScreen } from '../../support/dom'

const GuardedSendButton = defineComponent({
  setup() {
    const taps = ref(0)
    const { beginPressTracking, trackPressMovement, suppressTapIfItWasAScrollRelease } =
      useDockedStripTapGuard()
    return () =>
      h('div', [
        h(
          'button',
          {
            'data-test': 'send',
            onPointerdown: beginPressTracking,
            onPointermove: trackPressMovement,
            onClickCapture: suppressTapIfItWasAScrollRelease,
            onClick: () => {
              taps.value += 1
            },
          },
          'Senden',
        ),
        h('span', { 'data-test': 'taps' }, String(taps.value)),
      ])
  },
})

function mountTheGuardedButton() {
  const button = mount(GuardedSendButton, { attachTo: document.body })
  vi.advanceTimersByTime(1)
  return button
}

function theSendButton(): HTMLElement {
  return onScreen('[data-test="send"]')
}

async function tapsCounted(): Promise<string> {
  await nextTick()
  return textOnScreen('[data-test="taps"]')
}

function theFingerComesDownAt(x: number, y: number): void {
  theSendButton().dispatchEvent(new MouseEvent('pointerdown', { bubbles: true, clientX: x, clientY: y }))
}

function theFingerSlidesTo(x: number, y: number): void {
  theSendButton().dispatchEvent(new MouseEvent('pointermove', { bubbles: true, clientX: x, clientY: y }))
}

function theFingerLifts(): boolean {
  return theSendButton().dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true }))
}

beforeEach(() => {
  vi.useFakeTimers({ toFake: ['Date'], now: 100_000 })
})

afterEach(() => {
  vi.useRealTimers()
  vi.restoreAllMocks()
  document.body.innerHTML = ''
})

describe('a send button guarded against taps that were really scrolls', () => {
  it('still answers a finger that wobbled a few pixels while pressing', async () => {
    mountTheGuardedButton()

    theFingerComesDownAt(100, 700)
    theFingerSlidesTo(106, 706)
    theFingerLifts()

    expect(await tapsCounted()).toBe('1')
  })

  it('ignores a finger that slid away and came back to where it pressed', async () => {
    mountTheGuardedButton()

    theFingerComesDownAt(100, 700)
    theFingerSlidesTo(100, 620)
    theFingerSlidesTo(100, 700)
    theFingerLifts()

    expect(await tapsCounted()).toBe('0')
  })

  it('answers a fresh press after a drag the browser cancelled without a tap', async () => {
    mountTheGuardedButton()
    theFingerComesDownAt(100, 700)
    theFingerSlidesTo(100, 620)

    theFingerComesDownAt(100, 700)
    theFingerLifts()

    expect(await tapsCounted()).toBe('1')
  })

  it('keeps the browser from acting on a tap it swallowed', () => {
    mountTheGuardedButton()

    theFingerComesDownAt(100, 700)
    theFingerSlidesTo(100, 620)
    const theBrowserMayAct = theFingerLifts()

    expect(theBrowserMayAct).toBe(false)
  })
})

describe('a guarded send button that leaves the screen', () => {
  it('stops listening for the list scrolling', () => {
    const startsListening = vi.spyOn(window, 'addEventListener')
    const stopsListening = vi.spyOn(window, 'removeEventListener')
    const button = mountTheGuardedButton()
    const scrollListener = startsListening.mock.calls.find(([type]) => type === 'scroll')?.[1]

    button.unmount()

    expect(stopsListening).toHaveBeenCalledWith('scroll', scrollListener, { capture: true })
  })
})
