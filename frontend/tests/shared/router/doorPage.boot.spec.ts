import { beforeEach, describe, expect, it, vi } from 'vitest'
import {
  BACK_BUTTON_TRAP_ANCHOR_ADDRESS_KEY,
  BACK_BUTTON_TRAP_ANCHOR_KEY,
  BACK_BUTTON_TRAP_FRESH_START_KEY,
  BACK_BUTTON_TRAP_RETURN_PATH_KEY,
} from '../../../src/shared/router/backButtonTrap'
import {
  A_BROWSER_THAT_REPORTS_NO_NAVIGATION_TYPE,
  aBrowserReporting,
  aTabOpenedThroughTheStartButtonAt,
  anOlderBrowserReporting,
  runTheBoot,
  whereTheTabEndsUp,
  type TheStorage,
} from '../../support/doorScripts'

describe('the shell boot script', () => {
  it('waits for the start button on a tab a phone camera opened with a set-up link', () => {
    const storage: TheStorage = new Map()

    const { navigated } = runTheBoot('/j/CODE', storage)

    expect(navigated).toEqual([])
    expect(storage.size).toBe(0)
  })

  it('sends a tab back through the doors when a back press loads its start screen again', () => {
    const storage: TheStorage = new Map([
      [BACK_BUTTON_TRAP_ANCHOR_KEY, 'yes'],
      [BACK_BUTTON_TRAP_ANCHOR_ADDRESS_KEY, '/'],
    ])

    const { navigated } = runTheBoot('/', storage, aBrowserReporting('back_forward'))

    expect(navigated).toEqual(['/door.html?n=10&v=build-a'])
  })

  it('waits for the start button when the stored anchor is not the one the door writes', () => {
    const storage: TheStorage = new Map([[BACK_BUTTON_TRAP_ANCHOR_KEY, '{"opened":true}']])

    const { navigated } = runTheBoot('/', storage)

    expect(navigated).toEqual([])
  })

  it('stays put when the door just handed the app over', () => {
    const storage: TheStorage = new Map([
      [BACK_BUTTON_TRAP_ANCHOR_KEY, 'yes'],
      [BACK_BUTTON_TRAP_FRESH_START_KEY, 'yes'],
    ])

    const { navigated } = runTheBoot('/', storage)

    expect(navigated).toEqual([])
    expect(storage.has(BACK_BUTTON_TRAP_FRESH_START_KEY)).toBe(false)
  })
})

describe('a set-up link loaded into a tab that already shows the app', () => {
  beforeEach(() => {
    vi.resetModules()
  })

  it('ends at that link in a tab whose doors return to the waiter home screen', async () => {
    const storage = await aTabOpenedThroughTheStartButtonAt('/')

    const address = await whereTheTabEndsUp('/j/NEW', storage, aBrowserReporting('navigate'))

    expect(address).toBe('/j/NEW')
  })

  it('ends at that link in a tab whose doors return to an older set-up link', async () => {
    const storage = await aTabOpenedThroughTheStartButtonAt('/j/OLD')

    const address = await whereTheTabEndsUp('/j/NEW', storage, aBrowserReporting('navigate'))

    expect(address).toBe('/j/NEW')
  })

  it('ends at a station tablet\'s link in a tab that shows the station screen', async () => {
    const storage = await aTabOpenedThroughTheStartButtonAt('/j/TABLET-OLD')
    storage.set(BACK_BUTTON_TRAP_RETURN_PATH_KEY, '/stations')

    const address = await whereTheTabEndsUp('/j/TABLET-NEW', storage, aBrowserReporting('navigate'))

    expect(address).toBe('/j/TABLET-NEW')
  })

  it('ends at that link in a tab that shows the admin', async () => {
    const storage = await aTabOpenedThroughTheStartButtonAt('/admin/festivals')

    const address = await whereTheTabEndsUp('/j/NEW', storage, aBrowserReporting('navigate'))

    expect(address).toBe('/j/NEW')
  })

  it('ends at that link in a tab an older build opened, which stored no start address', async () => {
    const storage: TheStorage = new Map([
      [BACK_BUTTON_TRAP_ANCHOR_KEY, 'yes'],
      [BACK_BUTTON_TRAP_RETURN_PATH_KEY, '/j/OLD'],
    ])

    const address = await whereTheTabEndsUp('/j/NEW', storage, aBrowserReporting('navigate'))

    expect(address).toBe('/j/NEW')
  })

  it('ends at that link in a browser that reports the navigation the older way', async () => {
    const storage = await aTabOpenedThroughTheStartButtonAt('/j/OLD')

    const address = await whereTheTabEndsUp('/j/NEW', storage, anOlderBrowserReporting('navigate'))

    expect(address).toBe('/j/NEW')
  })
})

describe('a tab that already shows the app', () => {
  beforeEach(() => {
    vi.resetModules()
  })

  it('returns to where the device was when a back press reloads its start screen', async () => {
    const storage = await aTabOpenedThroughTheStartButtonAt('/j/OLD')
    storage.set(BACK_BUTTON_TRAP_RETURN_PATH_KEY, '/')

    const address = await whereTheTabEndsUp('/j/OLD', storage, aBrowserReporting('back_forward'))

    expect(address).toBe('/')
  })

  it('returns to where the device was when a back press reloads the start screen of a tab an older build opened', async () => {
    const storage: TheStorage = new Map([
      [BACK_BUTTON_TRAP_ANCHOR_KEY, 'yes'],
      [BACK_BUTTON_TRAP_RETURN_PATH_KEY, '/stations'],
    ])

    const address = await whereTheTabEndsUp('/', storage, aBrowserReporting('back_forward'))

    expect(address).toBe('/stations')
  })

  it('returns to where the device was when the browser cannot say how its start screen was loaded', async () => {
    const storage = await aTabOpenedThroughTheStartButtonAt('/j/OLD')
    storage.set(BACK_BUTTON_TRAP_RETURN_PATH_KEY, '/admin/festivals')

    const address = await whereTheTabEndsUp('/j/OLD', storage, A_BROWSER_THAT_REPORTS_NO_NAVIGATION_TYPE)

    expect(address).toBe('/admin/festivals')
  })

  it('keeps a waiter on the order screen when it is reloaded', async () => {
    const storage = await aTabOpenedThroughTheStartButtonAt('/j/OLD')
    storage.set(BACK_BUTTON_TRAP_RETURN_PATH_KEY, '/')

    const address = await whereTheTabEndsUp('/catalog', storage, aBrowserReporting('reload'))

    expect(address).toBe('/catalog')
  })

  it('stays at a link at another address than its start screen when the browser cannot say how it was loaded', async () => {
    const storage = await aTabOpenedThroughTheStartButtonAt('/j/OLD')

    const address = await whereTheTabEndsUp('/j/NEW', storage, A_BROWSER_THAT_REPORTS_NO_NAVIGATION_TYPE)

    expect(address).toBe('/j/NEW')
  })

  it('stays on an app screen that is reloaded', async () => {
    const storage = await aTabOpenedThroughTheStartButtonAt('/j/OLD')
    storage.set(BACK_BUTTON_TRAP_RETURN_PATH_KEY, '/admin/festivals/fest-1')

    const address = await whereTheTabEndsUp('/admin/festivals/fest-1', storage, aBrowserReporting('reload'))

    expect(address).toBe('/admin/festivals/fest-1')
  })

  it('stays on the screen it showed when the start screen\'s own address is reloaded', async () => {
    const storage = await aTabOpenedThroughTheStartButtonAt('/')

    const address = await whereTheTabEndsUp('/', storage, aBrowserReporting('reload'))

    expect(address).toBe('/')
  })

  it('stays in the app when back leaves a newly opened set-up link for the screen before it', async () => {
    const storage = await aTabOpenedThroughTheStartButtonAt('/j/OLD')
    storage.set(BACK_BUTTON_TRAP_RETURN_PATH_KEY, '/')

    const address = await whereTheTabEndsUp('/', storage, aBrowserReporting('back_forward'))

    expect(address).toBe('/')
  })

  it('still sends a back press onto its start screen through the doors after a newly opened set-up link', async () => {
    const storage = await aTabOpenedThroughTheStartButtonAt('/j/OLD')
    await whereTheTabEndsUp('/j/NEW', storage, aBrowserReporting('navigate'))
    storage.set(BACK_BUTTON_TRAP_RETURN_PATH_KEY, '/')

    const { navigated } = runTheBoot('/j/OLD', storage, aBrowserReporting('back_forward'))

    expect(navigated).toEqual(['/door.html?n=10&v=build-a'])
  })
})
