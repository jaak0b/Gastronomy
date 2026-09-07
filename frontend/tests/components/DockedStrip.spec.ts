import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import DockedStrip from '../../src/components/DockedStrip.vue'
import { MILLISECONDS_A_DOCKED_STRIP_IGNORES_AFTER_A_SCROLL } from '../../src/core/dockedStripTaps'
import { testPlugins } from '../support/plugins'

const StripWithASendButton = {
  components: { DockedStrip },
  data: () => ({ taps: 0 }),
  template: '<DockedStrip><button class="send" @click="taps += 1">Senden</button></DockedStrip>',
}

let now = 100_000

function timePasses(milliseconds: number): void {
  now += milliseconds
}

function aMomentPassesSoTheMountLiesInThePast(): void {
  timePasses(1)
}

function mountStrip() {
  const strip = mount(StripWithASendButton, {
    global: { plugins: testPlugins() },
    attachTo: document.body,
  })
  aMomentPassesSoTheMountLiesInThePast()
  return strip
}

type MountedStrip = ReturnType<typeof mountStrip>

function theSendButton(strip: MountedStrip): HTMLElement {
  return strip.get('.send').element as HTMLElement
}

function theListScrolls(): void {
  window.dispatchEvent(new Event('scroll'))
}

function theFingerComesDownOn(button: HTMLElement, x: number, y: number): void {
  button.dispatchEvent(new MouseEvent('pointerdown', { bubbles: true, clientX: x, clientY: y }))
}

function theFingerSlidesTo(button: HTMLElement, x: number, y: number): void {
  button.dispatchEvent(new MouseEvent('pointermove', { bubbles: true, clientX: x, clientY: y }))
}

function theFingerLifts(button: HTMLElement): void {
  button.dispatchEvent(new MouseEvent('click', { bubbles: true }))
}

describe('a docked strip that carries a button which cannot be undone', () => {
  beforeEach(() => {
    document.body.innerHTML = ''
    now = 100_000
    vi.spyOn(Date, 'now').mockImplementation(() => now)
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('answers a tap from a waiter who never scrolled', () => {
    const strip = mountStrip()
    const button = theSendButton(strip)

    theFingerComesDownOn(button, 100, 700)
    theFingerLifts(button)

    expect(strip.vm.taps).toBe(1)
  })

  it('answers a tap from a waiter who scrolled, stopped, looked and pressed', () => {
    const strip = mountStrip()
    const button = theSendButton(strip)
    theListScrolls()

    timePasses(2000)
    theFingerComesDownOn(button, 100, 700)
    theFingerLifts(button)

    expect(strip.vm.taps).toBe(1)
  })

  it('ignores a finger that comes down to stop a list that is still flying', () => {
    const strip = mountStrip()
    const button = theSendButton(strip)
    theListScrolls()

    timePasses(80)
    theFingerComesDownOn(button, 100, 700)
    theFingerLifts(button)

    expect(strip.vm.taps).toBe(0)
  })

  it('answers again the moment the waiting time after the scroll is over', () => {
    const strip = mountStrip()
    const button = theSendButton(strip)
    theListScrolls()

    timePasses(MILLISECONDS_A_DOCKED_STRIP_IGNORES_AFTER_A_SCROLL)
    theFingerComesDownOn(button, 100, 700)
    theFingerLifts(button)

    expect(strip.vm.taps).toBe(1)
  })

  it('ignores a finger that was dragged across the button before it lifted', () => {
    const strip = mountStrip()
    const button = theSendButton(strip)

    theFingerComesDownOn(button, 100, 700)
    theFingerSlidesTo(button, 100, 620)
    theFingerLifts(button)

    expect(strip.vm.taps).toBe(0)
  })

  it('answers the next tap after it swallowed a dragged one', () => {
    const strip = mountStrip()
    const button = theSendButton(strip)
    theFingerComesDownOn(button, 100, 700)
    theFingerSlidesTo(button, 100, 620)
    theFingerLifts(button)

    theFingerComesDownOn(button, 100, 700)
    theFingerLifts(button)

    expect(strip.vm.taps).toBe(1)
  })
})
