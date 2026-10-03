import { afterEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { defineComponent, h, nextTick } from 'vue'
import { useKeyboardInset } from '../../../src/phone/composables/useKeyboardInset'
import { textOnScreen } from '../../support/dom'

class PhoneViewport extends EventTarget {
  height: number
  scale: number

  constructor(height: number, scale = 1) {
    super()
    this.height = height
    this.scale = scale
  }

  async resizeTo(height: number): Promise<void> {
    this.height = height
    this.dispatchEvent(new Event('resize'))
    await nextTick()
  }
}

const InsetReading = defineComponent({
  setup() {
    const inset = useKeyboardInset()
    return () => h('div', { 'data-test': 'inset-reading' }, String(inset.value))
  },
})

async function mountTheReading() {
  const reading = mount(InsetReading, { attachTo: document.body })
  await nextTick()
  return reading
}

function theReading(): string {
  return textOnScreen('[data-test="inset-reading"]')
}

describe('a phone screen that watches for the on-screen keyboard', () => {
  afterEach(() => {
    document.body.innerHTML = ''
    vi.unstubAllGlobals()
  })

  it('reports the height the keyboard already covers when the screen opens', async () => {
    vi.stubGlobal('innerHeight', 800)
    vi.stubGlobal('visualViewport', new PhoneViewport(500))

    await mountTheReading()

    expect(theReading()).toBe('300')
  })

  it('follows the keyboard when it slides up after the screen opened', async () => {
    vi.stubGlobal('innerHeight', 800)
    const viewport = new PhoneViewport(800)
    vi.stubGlobal('visualViewport', viewport)
    await mountTheReading()

    await viewport.resizeTo(420)

    expect(theReading()).toBe('380')
  })

  it('reports nothing covered while the waiter has zoomed into the page', async () => {
    vi.stubGlobal('innerHeight', 800)
    vi.stubGlobal('visualViewport', new PhoneViewport(400, 2))

    await mountTheReading()

    expect(theReading()).toBe('0')
  })

  it('reports nothing covered on a browser that cannot tell where the keyboard is', async () => {
    vi.stubGlobal('innerHeight', 800)
    vi.stubGlobal('visualViewport', undefined)

    await mountTheReading()

    expect(theReading()).toBe('0')
  })

  it('stops listening for the keyboard once the screen is gone', async () => {
    vi.stubGlobal('innerHeight', 800)
    const viewport = new PhoneViewport(800)
    const startsListening = vi.spyOn(viewport, 'addEventListener')
    const stopsListening = vi.spyOn(viewport, 'removeEventListener')
    vi.stubGlobal('visualViewport', viewport)
    const reading = await mountTheReading()

    reading.unmount()

    expect(stopsListening).toHaveBeenCalledWith('resize', startsListening.mock.calls[0][1])
  })
})
