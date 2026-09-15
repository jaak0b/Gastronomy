import { beforeEach, describe, expect, it, vi } from 'vitest'
import { readFileSync } from 'node:fs'

const thePageBefore = '/the-page-before'

type TheStorage = Map<string, string>

function theSessionStorageOver(storage: TheStorage) {
  return {
    getItem: (key: string) => storage.get(key) ?? null,
    setItem: (key: string, value: string) => storage.set(key, value),
    removeItem: (key: string) => storage.delete(key),
  }
}

function theInlineScriptOf(file: string): string {
  const match = readFileSync(file, 'utf8').match(/<script>([\s\S]*?)<\/script>/)
  if (match === null) {
    throw new Error(`no inline script in ${file}`)
  }
  return match[1]
}

function runTheDoor(search: string, storage: TheStorage) {
  const handlers = new Map<string, () => void>()
  const assigned: string[] = []
  const theWindow = {
    addEventListener: (type: string, handler: () => void) => handlers.set(type, handler),
    location: { search, assign: (url: string) => assigned.push(url) },
  }
  const script = theInlineScriptOf('public/door.html')
  new Function('window', 'sessionStorage', 'URLSearchParams', script)(
    theWindow,
    theSessionStorageOver(storage),
    URLSearchParams,
  )
  return { assigned, handlers }
}

function runTheBoot(pathname: string, storage: TheStorage) {
  const navigated: string[] = []
  const theWindow = {
    location: {
      pathname,
      search: '',
      hash: '',
      assign: (url: string) => navigated.push(url),
    },
  }
  const theDocument = { lastModified: 'build-a' }
  const script = theInlineScriptOf('index.html')
  new Function('window', 'sessionStorage', 'document', script)(
    theWindow,
    theSessionStorageOver(storage),
    theDocument,
  )
  return { navigated }
}

async function withAFakedTick(run: () => void): Promise<void> {
  vi.useFakeTimers()
  run()
  await vi.advanceTimersByTimeAsync(1)
  vi.useRealTimers()
}

describe('the shell boot script', () => {
  it('waits for the start button on a tab where the doors were never opened', () => {
    const storage: TheStorage = new Map()

    const { navigated } = runTheBoot('/', storage)

    expect(navigated).toEqual([])
    expect(storage.size).toBe(0)
  })

  it('sends an already opened tab back through the doors when it is loaded again', () => {
    const storage: TheStorage = new Map([['theDoorAnchor', 'yes']])

    const { navigated } = runTheBoot('/', storage)

    expect(navigated).toEqual(['/door.html?n=10&v=build-a'])
  })

  it('stays put when the door just handed the app over', () => {
    const storage: TheStorage = new Map([
      ['theDoorAnchor', 'yes'],
      ['arrivedThroughTheDoor', 'yes'],
    ])

    const { navigated } = runTheBoot('/', storage)

    expect(navigated).toEqual([])
    expect(storage.has('arrivedThroughTheDoor')).toBe(false)
  })

  it('leaves the admin alone', () => {
    const storage: TheStorage = new Map()

    const { navigated } = runTheBoot('/admin/items', storage)

    expect(navigated).toEqual([])
    expect(storage.size).toBe(0)
  })
})

describe('the start button', () => {
  beforeEach(() => {
    vi.resetModules()
    sessionStorage.clear()
    window.history.replaceState({}, '', '/')
  })

  it('is asked for on a tab where the doors were never opened', async () => {
    const { needsTheDoorOpened } = await import('../../src/router')

    expect(needsTheDoorOpened()).toBe(true)
  })

  it('is not asked for again once the tab was opened', async () => {
    const { needsTheDoorOpened, THE_DOOR_ANCHOR_KEY } = await import('../../src/router')
    sessionStorage.setItem(THE_DOOR_ANCHOR_KEY, 'yes')

    expect(needsTheDoorOpened()).toBe(false)
  })

  it('is never asked for on the admin', async () => {
    window.history.replaceState({}, '', '/admin/items')
    const { needsTheDoorOpened } = await import('../../src/router')

    expect(needsTheDoorOpened()).toBe(false)
  })

  it('marks this screen as the anchor and remembers it as the back target', async () => {
    const { openTheDoor, THE_DOOR_ANCHOR_KEY, THE_DOOR_TARGET_KEY } = await import('../../src/router')

    openTheDoor()

    expect(sessionStorage.getItem(THE_DOOR_ANCHOR_KEY)).toBe('yes')
    expect(sessionStorage.getItem(THE_DOOR_TARGET_KEY)).toBe('/')
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

  it('uses the same storage keys the boot script wrote', async () => {
    const storage: TheStorage = new Map()
    runTheBoot('/', storage)

    const { assigned, handlers } = runTheDoor('?n=1', storage)

    await withAFakedTick(() => handlers.get('pageshow')!())

    expect(assigned).toEqual(['/'])
    expect(storage.get('arrivedThroughTheDoor')).toBe('yes')
  })
})

describe('a device that arrived through the door', () => {
  beforeEach(() => {
    vi.resetModules()
    sessionStorage.clear()
    window.history.replaceState({}, '', thePageBefore)
    window.history.pushState({}, '', '/')
  })

  it('replaces its entry when it moves between screens, so the doors stay behind it', async () => {
    const { currentRoute, keepTheDeviceBehindTheDoor, navigate } = await import('../../src/router')
    keepTheDeviceBehindTheDoor()
    const entries = window.history.length

    navigate('/review')
    navigate('/')

    expect(window.history.length).toBe(entries)
    expect(currentRoute.value).toEqual({ name: 'home' })
    expect(window.location.pathname).toBe('/')
  })

  it('remembers the ordering screen as where a back press lands from the review screen', async () => {
    const { keepTheDeviceBehindTheDoor, navigate, THE_DOOR_TARGET_KEY } = await import(
      '../../src/router'
    )
    keepTheDeviceBehindTheDoor()

    navigate('/review')

    expect(sessionStorage.getItem(THE_DOOR_TARGET_KEY)).toBe('/')
  })

  it('remembers the ordering screen as where a back press lands from the open items screen', async () => {
    const { keepTheDeviceBehindTheDoor, navigate, THE_DOOR_TARGET_KEY } = await import(
      '../../src/router'
    )
    keepTheDeviceBehindTheDoor()

    navigate('/open-items')

    expect(sessionStorage.getItem(THE_DOOR_TARGET_KEY)).toBe('/')
  })

  it('remembers the station screen as where a back press lands on a tablet', async () => {
    window.history.replaceState({}, '', '/stations')
    const { keepTheDeviceBehindTheDoor, navigate, THE_DOOR_TARGET_KEY } = await import(
      '../../src/router'
    )
    keepTheDeviceBehindTheDoor()

    navigate('/stations')

    expect(sessionStorage.getItem(THE_DOOR_TARGET_KEY)).toBe('/stations')
  })

  it('remembers the invitation address until the phone is set up', async () => {
    window.history.replaceState({}, '', '/j/abc123')
    const { keepTheDeviceBehindTheDoor, THE_DOOR_TARGET_KEY } = await import('../../src/router')

    keepTheDeviceBehindTheDoor()

    expect(sessionStorage.getItem(THE_DOOR_TARGET_KEY)).toBe('/j/abc123')
  })

  it('moves the back target with a fresh start, so it cannot land on the screen before', async () => {
    const { startOverAt, THE_DOOR_TARGET_KEY, THE_DOOR_ARRIVAL_KEY } = await import(
      '../../src/router'
    )

    startOverAt('/')

    expect(sessionStorage.getItem(THE_DOOR_TARGET_KEY)).toBe('/')
    expect(sessionStorage.getItem(THE_DOOR_ARRIVAL_KEY)).toBe('yes')
  })
})

describe('the admin on the laptop', () => {
  beforeEach(() => {
    vi.resetModules()
    sessionStorage.clear()
    window.history.replaceState({}, '', thePageBefore)
  })

  it('keeps the browser step between its sections', async () => {
    window.history.pushState({}, '', '/admin/overview')
    const { currentRoute, navigate, startRouter } = await import('../../src/router')
    startRouter()
    navigate('/admin/items')

    const popped = new Promise((resolve) => {
      window.addEventListener('popstate', () => resolve(undefined), { once: true })
    })
    window.history.back()
    await popped

    expect(currentRoute.value).toEqual({ name: 'admin', section: 'overview', festivalId: null })
    expect(window.location.pathname).toBe('/admin/overview')
  })

  it('never takes the doors or a back target', async () => {
    window.history.pushState({}, '', '/admin/overview')
    const { navigate, startRouter, THE_DOOR_TARGET_KEY } = await import('../../src/router')
    startRouter()

    navigate('/admin/items')

    expect(sessionStorage.getItem(THE_DOOR_TARGET_KEY)).toBeNull()
  })
})
