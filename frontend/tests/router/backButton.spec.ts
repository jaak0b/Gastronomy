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

describe('the admin on the laptop, where the back button stays the browser one', () => {
  beforeEach(() => {
    vi.resetModules()
    window.history.replaceState({}, '', thePageThePhoneWasOnBefore)
  })

  it('lets the laptop leave the admin by going back', async () => {
    window.history.pushState({}, '', '/admin/items')
    const { startRouter } = await import('../../src/router')
    startRouter()

    await pressBack()

    expect(window.location.pathname).toBe(thePageThePhoneWasOnBefore)
  })

  it('shows the admin section the browser landed on', async () => {
    window.history.pushState({}, '', '/admin/overview')
    const { currentRoute, navigate, startRouter } = await import('../../src/router')
    startRouter()
    navigate('/admin/items')

    await pressBack()

    expect(currentRoute.value).toEqual({ name: 'admin', section: 'overview', festivalId: null })
    expect(window.location.pathname).toBe('/admin/overview')
  })
})

describe('the back button of the phone the server holds', () => {
  beforeEach(() => {
    vi.resetModules()
    window.history.replaceState({}, '', thePageThePhoneWasOnBefore)
  })

  it('stays on the ordering screen when the server presses back there', async () => {
    window.history.pushState({}, '', '/')
    const { currentRoute, keepTheDeviceInsideTheApp } = await import('../../src/router')
    keepTheDeviceInsideTheApp()

    await pressBack()

    expect(currentRoute.value).toEqual({ name: 'home' })
    expect(window.location.pathname).toBe('/')
  })

  it('stays on the ordering screen when the server presses back again', async () => {
    window.history.pushState({}, '', '/')
    const { currentRoute, keepTheDeviceInsideTheApp } = await import('../../src/router')
    keepTheDeviceInsideTheApp()

    await pressBack()
    await pressBack()

    expect(currentRoute.value).toEqual({ name: 'home' })
    expect(window.location.pathname).toBe('/')
  })

  it('keeps the waiter on the ordering screen before the screen has been touched', async () => {
    window.history.pushState({}, '', '/')
    const { currentRoute, keepTheDeviceInsideTheApp } = await import('../../src/router')
    keepTheDeviceInsideTheApp()

    await pressBack()
    await pressBack()
    await pressBack()

    expect(currentRoute.value).toEqual({ name: 'home' })
    expect(window.location.pathname).toBe('/')
  })

  it('returns to the ordering screen when the server presses back on the review screen', async () => {
    window.history.pushState({}, '', '/')
    const { currentRoute, keepTheDeviceInsideTheApp, navigate } = await import('../../src/router')
    keepTheDeviceInsideTheApp()
    navigate('/review')

    await pressBack()

    expect(currentRoute.value).toEqual({ name: 'home' })
    expect(window.location.pathname).toBe('/')
  })

  it('returns to the ordering screen when the server presses back on the open items screen', async () => {
    window.history.pushState({}, '', '/')
    const { currentRoute, keepTheDeviceInsideTheApp, navigate } = await import('../../src/router')
    keepTheDeviceInsideTheApp()
    navigate('/open-items')

    await pressBack()

    expect(currentRoute.value).toEqual({ name: 'home' })
    expect(window.location.pathname).toBe('/')
  })

  it('keeps the station tablet on its screen however often back is pressed', async () => {
    window.history.pushState({}, '', '/stations')
    const { currentRoute, keepTheDeviceInsideTheApp } = await import('../../src/router')
    keepTheDeviceInsideTheApp()

    await pressBack()
    await pressBack()
    await pressBack()

    expect(currentRoute.value).toEqual({ name: 'stations' })
    expect(window.location.pathname).toBe('/stations')
  })

  it('leaves no way back to the invitation address once the router replaces it', async () => {
    window.history.pushState({}, '', '/j/abc123')
    const { currentRoute, keepTheDeviceInsideTheApp, navigate } = await import('../../src/router')

    keepTheDeviceInsideTheApp()
    navigate('/')
    await pressBack()
    await pressBack()

    expect(currentRoute.value).toEqual({ name: 'home' })
    expect(window.location.pathname).toBe('/')
  })

  it('keeps the browser history to one screen entry however much the waiter taps around', async () => {
    window.history.pushState({}, '', '/')
    const { currentRoute, keepTheDeviceInsideTheApp, navigate } = await import('../../src/router')
    keepTheDeviceInsideTheApp()
    const entriesWithTheGuardInPlace = window.history.length

    for (let tap = 0; tap < 60; tap++) {
      navigate(tap % 2 === 0 ? '/review' : '/')
    }
    navigate('/')

    expect(window.history.length).toBe(entriesWithTheGuardInPlace)

    await pressBack()

    expect(currentRoute.value).toEqual({ name: 'home' })
    expect(window.location.pathname).toBe('/')
  })

})

describe('a step the ordering screen has opened inside itself', () => {
  beforeEach(() => {
    vi.resetModules()
    window.history.replaceState({}, '', thePageThePhoneWasOnBefore)
  })

  it('closes on a back press instead of taking the waiter off the ordering screen', async () => {
    window.history.pushState({}, '', '/')
    const { currentRoute, keepTheDeviceInsideTheApp, openAStepInsideTheScreen } = await import(
      '../../src/router'
    )
    keepTheDeviceInsideTheApp()
    let timesClosed = 0
    openAStepInsideTheScreen(() => {
      timesClosed += 1
    })

    await pressBack()

    expect(timesClosed).toBe(1)
    expect(currentRoute.value).toEqual({ name: 'home' })
    expect(window.location.pathname).toBe('/')
  })

  it('leaves the next back press to the ordering screen, which stays inside the app', async () => {
    window.history.pushState({}, '', '/')
    const { keepTheDeviceInsideTheApp, openAStepInsideTheScreen } = await import('../../src/router')
    keepTheDeviceInsideTheApp()
    let timesClosed = 0
    openAStepInsideTheScreen(() => {
      timesClosed += 1
    })

    await pressBack()
    await pressBack()

    expect(timesClosed).toBe(1)
    expect(window.location.pathname).toBe('/')
  })

  it('closes the same way when the screen asks for it, so both ways back run one path', async () => {
    window.history.pushState({}, '', '/')
    const {
      closeTheStepInsideTheScreen,
      keepTheDeviceInsideTheApp,
      openAStepInsideTheScreen,
    } = await import('../../src/router')
    keepTheDeviceInsideTheApp()
    let timesClosed = 0
    openAStepInsideTheScreen(() => {
      timesClosed += 1
    })

    closeTheStepInsideTheScreen()
    await pressBack()

    expect(timesClosed).toBe(1)
    expect(window.location.pathname).toBe('/')
  })
})

describe('the back gesture once the summary has sent the waiter on to a second order', () => {
  beforeEach(() => {
    vi.resetModules()
    window.history.replaceState({}, '', thePageThePhoneWasOnBefore)
  })

  async function aWaiterOnTheirSecondOrder() {
    window.history.pushState({}, '', '/')
    const router = await import('../../src/router')
    router.keepTheDeviceInsideTheApp()
    router.navigate('/review')
    router.navigate('/')
    return router
  }

  it('closes the open category in one press, without leaving the ordering screen', async () => {
    const { currentRoute, openAStepInsideTheScreen } = await aWaiterOnTheirSecondOrder()
    let timesClosed = 0
    openAStepInsideTheScreen(() => {
      timesClosed += 1
    })

    await pressBack()

    expect(timesClosed).toBe(1)
    expect(currentRoute.value).toEqual({ name: 'home' })
    expect(window.location.pathname).toBe('/')
  })
})

describe('a move to another screen while a step is open inside the ordering screen', () => {
  beforeEach(() => {
    vi.resetModules()
    window.history.replaceState({}, '', thePageThePhoneWasOnBefore)
  })

  it('closes the step, so the ordering screen answers a tap that is already on it', async () => {
    window.history.pushState({}, '', '/')
    const { keepTheDeviceInsideTheApp, navigate, openAStepInsideTheScreen } = await import(
      '../../src/router'
    )
    keepTheDeviceInsideTheApp()
    let timesClosed = 0
    openAStepInsideTheScreen(() => {
      timesClosed += 1
    })

    navigate('/')

    expect(timesClosed).toBe(1)
  })
})
