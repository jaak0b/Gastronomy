import { beforeEach, describe, expect, it, vi } from 'vitest'
import { readFileSync } from 'node:fs'

const thePageBefore = '/the-page-before'

describe('the back guard pad', () => {
  beforeEach(() => {
    vi.resetModules()
    delete (window as Window & { theBackGuardPadIsPlanted?: boolean }).theBackGuardPadIsPlanted
    window.history.replaceState({}, '', thePageBefore)
  })

  it('plants twenty copies of the current address', async () => {
    window.history.pushState({}, '', '/')
    const { plantTheBackGuardPad, THE_BACK_GUARD_PAD_SIZE } = await import('../src/theBackGuardPad')
    const before = window.history.length

    plantTheBackGuardPad()

    expect(THE_BACK_GUARD_PAD_SIZE).toBe(20)
    expect(window.history.length).toBe(before + 20)
  })

  it('absorbs a whole burst at once, so the page before is never reached', async () => {
    window.history.pushState({}, '', '/')
    const { plantTheBackGuardPad, THE_BACK_GUARD_PAD_SIZE } = await import('../src/theBackGuardPad')
    plantTheBackGuardPad()

    const popped = new Promise((resolve) => {
      window.addEventListener('popstate', () => resolve(undefined), { once: true })
    })
    window.history.go(-THE_BACK_GUARD_PAD_SIZE)
    await popped

    expect(window.location.pathname).toBe('/')
  })

  it('plants nothing for the admin on the laptop', async () => {
    window.history.pushState({}, '', '/admin/items')
    const { plantTheBackGuardPad } = await import('../src/theBackGuardPad')
    const before = window.history.length

    plantTheBackGuardPad()

    expect(window.history.length).toBe(before)
  })

  it('plants the pad once, however often the app asks for it', async () => {
    window.history.pushState({}, '', '/')
    const { plantTheBackGuardPad } = await import('../src/theBackGuardPad')
    const before = window.history.length

    plantTheBackGuardPad()
    plantTheBackGuardPad()

    expect(window.history.length).toBe(before + 20)
  })

  it('plants nothing when the page already planted its own pad', async () => {
    window.history.pushState({}, '', '/')
    const { plantTheBackGuardPad } = await import('../src/theBackGuardPad')
    ;(window as Window & { theBackGuardPadIsPlanted?: boolean }).theBackGuardPadIsPlanted = true
    const before = window.history.length

    plantTheBackGuardPad()

    expect(window.history.length).toBe(before)
  })

  it('is planted by the page itself before the app bundle', async () => {
    const { THE_BACK_GUARD_PAD_SIZE } = await import('../src/theBackGuardPad')
    const html = readFileSync('index.html', 'utf8')
    const inlinePad = html.match(/copy < (\d+)/)
    const theInlinePlant = html.indexOf('window.history.pushState')
    const theAppBundle = html.indexOf('/src/main.ts')

    expect(inlinePad).not.toBeNull()
    expect(Number(inlinePad![1])).toBe(THE_BACK_GUARD_PAD_SIZE)
    expect(html).toContain('window.theBackGuardPadIsPlanted = true')
    expect(theInlinePlant).toBeGreaterThan(-1)
    expect(theAppBundle).toBeGreaterThan(-1)
    expect(theInlinePlant).toBeLessThan(theAppBundle)
  })
})
