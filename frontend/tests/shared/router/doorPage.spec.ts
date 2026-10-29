import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import {
  aBrowserReporting,
  runTheBoot,
  runTheDoor,
  withAFakedTick,
  type TheStorage,
} from '../../support/doorScripts'

const thePageBefore = '/the-page-before'

describe('the start button', () => {
  beforeEach(() => {
    vi.resetModules()
    sessionStorage.clear()
    window.history.replaceState({}, '', '/')
  })

  it('is asked for on a tab where the doors were never opened', async () => {
    const { backButtonTrapNeedsBuilding } = await import('../../../src/shared/router/backButtonTrap')

    expect(backButtonTrapNeedsBuilding()).toBe(true)
  })

  it('is not asked for again once the tab was opened', async () => {
    const { BACK_BUTTON_TRAP_ANCHOR_KEY, backButtonTrapNeedsBuilding } = await import('../../../src/shared/router/backButtonTrap')
    sessionStorage.setItem(BACK_BUTTON_TRAP_ANCHOR_KEY, 'yes')

    expect(backButtonTrapNeedsBuilding()).toBe(false)
  })

  it('is asked for when the stored answer is not the one the door writes', async () => {
    const { BACK_BUTTON_TRAP_ANCHOR_KEY, backButtonTrapNeedsBuilding } = await import('../../../src/shared/router/backButtonTrap')
    sessionStorage.setItem(BACK_BUTTON_TRAP_ANCHOR_KEY, '{"opened":true}')

    expect(backButtonTrapNeedsBuilding()).toBe(true)
  })

  it('stores the address it was tapped at, so the boot script can tell a back press onto it', async () => {
    window.history.replaceState({}, '', '/j/CODE')
    const { BACK_BUTTON_TRAP_ANCHOR_ADDRESS_KEY, buildBackButtonTrap } = await import('../../../src/shared/router/backButtonTrap')

    buildBackButtonTrap()

    expect(sessionStorage.getItem(BACK_BUTTON_TRAP_ANCHOR_ADDRESS_KEY)).toBe('/j/CODE')
  })

  it('is marked with a target the boot script can use', async () => {
    const { BACK_BUTTON_TRAP_ANCHOR_KEY, BACK_BUTTON_TRAP_RETURN_PATH_KEY, buildBackButtonTrap } = await import('../../../src/shared/router/backButtonTrap')

    buildBackButtonTrap()

    expect(sessionStorage.getItem(BACK_BUTTON_TRAP_ANCHOR_KEY)).toBe('yes')
    expect(sessionStorage.getItem(BACK_BUTTON_TRAP_RETURN_PATH_KEY)).toBe('/')
  })
})

describe('the door page', () => {
  it('sends a back press on to the next door', async () => {
    const { assigned, handlers } = runTheDoor('?n=3&v=abc', new Map())

    await withAFakedTick(() => handlers.get('pageshow')!())

    expect(assigned).toEqual(['/door.html?n=2&v=abc'])
  })

  it('hands a back press back to the app at the last step', async () => {
    const storage: TheStorage = new Map([['theDoorTarget', '/review']])
    const { assigned, handlers } = runTheDoor('?n=1', storage)

    await withAFakedTick(() => handlers.get('pageshow')!())

    expect(assigned).toEqual(['/review'])
    expect(storage.get('arrivedThroughTheDoor')).toBe('yes')
  })

  it('falls back to the ordering screen when no target was stored', async () => {
    const { assigned, handlers } = runTheDoor('?n=1', new Map())

    await withAFakedTick(() => handlers.get('pageshow')!())

    expect(assigned).toEqual(['/'])
  })

  it('agrees with the open button and the boot script on the storage keys', async () => {
    vi.resetModules()
    sessionStorage.clear()
    window.history.replaceState({}, '', '/')
    const { buildBackButtonTrap } = await import('../../../src/shared/router/backButtonTrap')

    buildBackButtonTrap()
    const storage: TheStorage = new Map()
    for (let index = 0; index < sessionStorage.length; index += 1) {
      const key = sessionStorage.key(index)!
      storage.set(key, sessionStorage.getItem(key)!)
    }

    const { navigated } = runTheBoot('/', storage, aBrowserReporting('back_forward'))
    expect(navigated).toHaveLength(1)

    const { assigned, handlers } = runTheDoor('?n=1&v=build-a', storage)
    await withAFakedTick(() => handlers.get('pageshow')!())
    expect(assigned).toEqual(['/'])

    const { navigated: theBootAfterTheDoor } = runTheBoot('/', storage)
    expect(theBootAfterTheDoor).toEqual([])
    expect(storage.has('arrivedThroughTheDoor')).toBe(false)
  })
})

describe('a browser that comes back to the anchor', () => {
  beforeEach(() => {
    vi.resetModules()
    sessionStorage.clear()
    window.history.replaceState({}, '', '/')
  })

  it('rebuilds the doors when the browser restores the anchor', async () => {
    const { backButtonTrapNeedsRebuildOnRestore, buildBackButtonTrap } = await import('../../../src/shared/router/backButtonTrap')

    buildBackButtonTrap()

    expect(backButtonTrapNeedsRebuildOnRestore(true)).toBe(true)
  })

  it('leaves the first show of the screen alone', async () => {
    const { backButtonTrapNeedsRebuildOnRestore, buildBackButtonTrap } = await import('../../../src/shared/router/backButtonTrap')

    buildBackButtonTrap()

    expect(backButtonTrapNeedsRebuildOnRestore(false)).toBe(false)
  })

  it('has nothing to rebuild in a document that never opened the doors', async () => {
    const { backButtonTrapNeedsRebuildOnRestore } = await import('../../../src/shared/router/backButtonTrap')

    expect(backButtonTrapNeedsRebuildOnRestore(true)).toBe(false)
  })
})

describe('a device that arrived through the door', () => {
  beforeEach(() => {
    vi.resetModules()
    sessionStorage.clear()
    window.history.replaceState({}, '', thePageBefore)
    window.history.pushState({}, '', '/')
  })

  it('replaces its entry when a browser cannot say whether a tap started the move, so the doors stay behind it', async () => {
    const { currentRoute, navigate } = await import('../../../src/shared/router/router')
    const { markBackButtonTrapActive } = await import('../../../src/shared/router/backButtonTrap')
    markBackButtonTrapActive(currentRoute.value)
    const entries = window.history.length

    navigate('/review')
    await flushPromises()
    navigate('/')
    await flushPromises()

    expect(window.history.length).toBe(entries)
    expect(currentRoute.value).toEqual({ name: 'home' })
    expect(window.location.pathname).toBe('/')
  })

  it('remembers the ordering screen as where a back press lands from the review screen', async () => {
    const { currentRoute, navigate } = await import('../../../src/shared/router/router')
    const { BACK_BUTTON_TRAP_RETURN_PATH_KEY, markBackButtonTrapActive } = await import('../../../src/shared/router/backButtonTrap')
    markBackButtonTrapActive(currentRoute.value)

    navigate('/review')
    await flushPromises()

    expect(sessionStorage.getItem(BACK_BUTTON_TRAP_RETURN_PATH_KEY)).toBe('/')
  })

  it('remembers the ordering screen as where a back press lands from the open items screen', async () => {
    const { currentRoute, navigate } = await import('../../../src/shared/router/router')
    const { BACK_BUTTON_TRAP_RETURN_PATH_KEY, markBackButtonTrapActive } = await import('../../../src/shared/router/backButtonTrap')
    markBackButtonTrapActive(currentRoute.value)

    navigate('/open-items')
    await flushPromises()

    expect(sessionStorage.getItem(BACK_BUTTON_TRAP_RETURN_PATH_KEY)).toBe('/')
  })

  it('remembers the station screen as where a back press lands on a tablet', async () => {
    window.history.replaceState({}, '', '/stations')
    const { currentRoute, navigate } = await import('../../../src/shared/router/router')
    const { BACK_BUTTON_TRAP_RETURN_PATH_KEY, markBackButtonTrapActive } = await import('../../../src/shared/router/backButtonTrap')
    markBackButtonTrapActive(currentRoute.value)

    navigate('/stations')
    await flushPromises()

    expect(sessionStorage.getItem(BACK_BUTTON_TRAP_RETURN_PATH_KEY)).toBe('/stations')
  })

  it('remembers the invitation address until the phone is set up', async () => {
    window.history.replaceState({}, '', '/j/abc123')
    const { currentRoute } = await import('../../../src/shared/router/router')
    const { BACK_BUTTON_TRAP_RETURN_PATH_KEY, markBackButtonTrapActive } = await import('../../../src/shared/router/backButtonTrap')

    markBackButtonTrapActive(currentRoute.value)

    expect(sessionStorage.getItem(BACK_BUTTON_TRAP_RETURN_PATH_KEY)).toBe('/j/abc123')
  })

  it('moves the back target with a fresh start, so it cannot land on the screen before', async () => {
    const { startOverAt } = await import('../../../src/shared/router/router')
    const {
      BACK_BUTTON_TRAP_ANCHOR_KEY,
      BACK_BUTTON_TRAP_FRESH_START_KEY,
      BACK_BUTTON_TRAP_RETURN_PATH_KEY,
    } = await import('../../../src/shared/router/backButtonTrap')

    startOverAt('/')

    expect(sessionStorage.getItem(BACK_BUTTON_TRAP_ANCHOR_KEY)).toBe('yes')
    expect(sessionStorage.getItem(BACK_BUTTON_TRAP_RETURN_PATH_KEY)).toBe('/')
    expect(sessionStorage.getItem(BACK_BUTTON_TRAP_FRESH_START_KEY)).toBe('yes')
  })
})

describe('a tab where the doors were never opened', () => {
  beforeEach(() => {
    vi.resetModules()
    sessionStorage.clear()
    window.history.replaceState({}, '', thePageBefore)
  })

  it('never takes the doors or a back target', async () => {
    window.history.pushState({}, '', '/admin/overview')
    const { navigate, startRouter } = await import('../../../src/shared/router/router')
    const { BACK_BUTTON_TRAP_RETURN_PATH_KEY } = await import('../../../src/shared/router/backButtonTrap')
    startRouter()

    navigate('/admin/items')
    await flushPromises()

    expect(sessionStorage.getItem(BACK_BUTTON_TRAP_RETURN_PATH_KEY)).toBeNull()
  })
})
