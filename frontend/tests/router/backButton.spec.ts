import { beforeEach, describe, expect, it, vi } from 'vitest'

const thePageThePhoneWasOnBefore = '/the-page-the-phone-was-on-before'

function nextPopstate(): Promise<void> {
  return new Promise((resolve) => {
    window.addEventListener('popstate', () => resolve(), { once: true })
  })
}

async function pressBack(): Promise<void> {
  const popped = nextPopstate()
  window.history.back()
  await popped
}

function touchTheScreen(): void {
  window.dispatchEvent(new Event('pointerdown'))
}

describe('the back button of the phone the server holds', () => {
  beforeEach(() => {
    vi.resetModules()
    window.history.replaceState({}, '', thePageThePhoneWasOnBefore)
  })

  it('stays on the ordering screen when the server presses back there', async () => {
    window.history.pushState({}, '', '/')
    const { currentRoute, startRouter } = await import('../../src/router')
    startRouter()
    touchTheScreen()

    await pressBack()

    expect(currentRoute.value).toEqual({ name: 'home' })
    expect(window.location.pathname).toBe('/')
  })

  it('stays on the ordering screen when the server presses back again', async () => {
    window.history.pushState({}, '', '/')
    const { currentRoute, startRouter } = await import('../../src/router')
    startRouter()
    touchTheScreen()

    await pressBack()
    await pressBack()

    expect(currentRoute.value).toEqual({ name: 'home' })
    expect(window.location.pathname).toBe('/')
  })

  it('leaves the app on back while the server has not touched the screen yet', async () => {
    window.history.pushState({}, '', '/')
    const { startRouter } = await import('../../src/router')
    startRouter()

    await pressBack()

    expect(window.location.pathname).toBe(thePageThePhoneWasOnBefore)
  })

  it('returns to the ordering screen when the server presses back on the review screen', async () => {
    window.history.pushState({}, '', '/')
    const { currentRoute, navigate, startRouter } = await import('../../src/router')
    startRouter()
    touchTheScreen()
    navigate('/review')

    await pressBack()

    expect(currentRoute.value).toEqual({ name: 'home' })
    expect(window.location.pathname).toBe('/')
  })

  it('returns to the ordering screen when the server presses back on the open items screen', async () => {
    window.history.pushState({}, '', '/')
    const { currentRoute, navigate, startRouter } = await import('../../src/router')
    startRouter()
    touchTheScreen()
    navigate('/open-items')

    await pressBack()

    expect(currentRoute.value).toEqual({ name: 'home' })
    expect(window.location.pathname).toBe('/')
  })

  it('lets the laptop leave the admin by going back', async () => {
    window.history.pushState({}, '', '/admin/items')
    const { startRouter } = await import('../../src/router')
    startRouter()
    touchTheScreen()

    await pressBack()

    expect(window.location.pathname).toBe(thePageThePhoneWasOnBefore)
  })

  it('lets a phone leave the station backlog by going back', async () => {
    window.history.pushState({}, '', '/stations')
    const { startRouter } = await import('../../src/router')
    startRouter()
    touchTheScreen()

    await pressBack()

    expect(window.location.pathname).toBe(thePageThePhoneWasOnBefore)
  })

  it('leaves no way back to the invitation address once the router replaces it', async () => {
    window.history.pushState({}, '', '/j/abc123')
    const { currentRoute, replace } = await import('../../src/router')

    replace('/')
    touchTheScreen()
    await pressBack()

    expect(currentRoute.value).toEqual({ name: 'home' })
    expect(window.location.pathname).toBe('/')
  })
})
