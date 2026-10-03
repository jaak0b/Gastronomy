import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { enableAutoUnmount, mount } from '@vue/test-utils'
import { nextTick } from 'vue'
import { theKeyboard, InsetProbe, mountTwoProbes } from './itemRowFixture'

enableAutoUnmount(afterEach)

beforeEach(() => {
  document.body.innerHTML = ''
})

describe('the keyboard inset a screen measures', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
    document.body.innerHTML = ''
  })

  it('follows the visual viewport while the keyboard grows and shrinks', async () => {
    vi.stubGlobal('innerHeight', 800)
    const keyboard = theKeyboard(400)
    vi.stubGlobal('visualViewport', keyboard)
    mount(InsetProbe, { attachTo: document.body })
    await nextTick()

    expect(document.querySelector('[data-test="inset-probe"]')?.textContent).toBe('400')

    keyboard.height = 300
    ;(keyboard.addEventListener.mock.calls[0][1] as () => void)()
    await nextTick()

    expect(document.querySelector('[data-test="inset-probe"]')?.textContent).toBe('500')
  })

  it('takes no room while the waiter is zoomed in', async () => {
    vi.stubGlobal('innerHeight', 800)
    vi.stubGlobal('visualViewport', theKeyboard(400, 2))
    mount(InsetProbe, { attachTo: document.body })
    await nextTick()

    expect(document.querySelector('[data-test="inset-probe"]')?.textContent).toBe('0')
  })

  it('takes no room when the browser holds no visual viewport', async () => {
    vi.stubGlobal('innerHeight', 800)
    vi.stubGlobal('visualViewport', undefined)
    mount(InsetProbe, { attachTo: document.body })
    await nextTick()

    expect(document.querySelector('[data-test="inset-probe"]')?.textContent).toBe('0')
  })

  it('gives every screen its own listener and takes it back when that screen goes', async () => {
    vi.stubGlobal('innerHeight', 800)
    const keyboard = theKeyboard(400)
    vi.stubGlobal('visualViewport', keyboard)
    const probes = mountTwoProbes()

    expect(keyboard.addEventListener).toHaveBeenCalledTimes(2)
    expect(keyboard.addEventListener).toHaveBeenCalledWith('resize', expect.any(Function))

    probes.firstIsThere.value = false
    await nextTick()

    expect(keyboard.removeEventListener).toHaveBeenCalledTimes(1)
    expect(keyboard.removeEventListener).toHaveBeenCalledWith(
      'resize',
      keyboard.addEventListener.mock.calls[0][1],
    )

    probes.secondIsThere.value = false
    await nextTick()

    expect(keyboard.removeEventListener).toHaveBeenCalledTimes(2)
    expect(keyboard.removeEventListener).toHaveBeenCalledWith(
      'resize',
      keyboard.addEventListener.mock.calls[1][1],
    )
  })

  it('shows the same inset in two screens measuring the same viewport', async () => {
    vi.stubGlobal('innerHeight', 800)
    const keyboard = theKeyboard(400)
    vi.stubGlobal('visualViewport', keyboard)
    mountTwoProbes()
    await nextTick()

    const probes = document.querySelectorAll('[data-test="inset-probe"]')

    expect([...probes].map((probe) => probe.textContent)).toEqual(['400', '400'])
  })
})
