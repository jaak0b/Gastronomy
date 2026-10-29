import { vi } from 'vitest'
import { readFileSync } from 'node:fs'

export type TheStorage = Map<string, string>

export type NavigationType = 'navigate' | 'reload' | 'back_forward'

export interface NavigationTimingOfABrowser {
  getEntriesByType?: (kind: string) => { type: string }[]
  navigation?: { type: number }
}

const LAPTOP_ORIGIN = 'http://laptop.test'

const LEGACY_TYPE_OF: Record<NavigationType, number> = { navigate: 0, reload: 1, back_forward: 2 }

export function aBrowserReporting(type: NavigationType): NavigationTimingOfABrowser {
  return { getEntriesByType: (kind) => (kind === 'navigation' ? [{ type }] : []) }
}

export function anOlderBrowserReporting(type: NavigationType): NavigationTimingOfABrowser {
  return { navigation: { type: LEGACY_TYPE_OF[type] } }
}

export const A_BROWSER_THAT_REPORTS_NO_NAVIGATION_TYPE: NavigationTimingOfABrowser = {}

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

export function runTheDoor(search: string, storage: TheStorage) {
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

export function runTheBoot(
  address: string,
  storage: TheStorage,
  browser: NavigationTimingOfABrowser = aBrowserReporting('navigate'),
) {
  const navigated: string[] = []
  const loaded = new URL(address, LAPTOP_ORIGIN)
  const theWindow = {
    location: {
      pathname: loaded.pathname,
      search: loaded.search,
      hash: loaded.hash,
      assign: (url: string) => navigated.push(url),
    },
    performance: browser,
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

export async function withAFakedTick(run: () => void): Promise<void> {
  vi.useFakeTimers()
  run()
  await vi.advanceTimersByTimeAsync(1)
  vi.useRealTimers()
}

async function throughTheDoors(door: string, storage: TheStorage): Promise<string> {
  const { assigned, handlers } = runTheDoor(new URL(door, LAPTOP_ORIGIN).search, storage)
  const pageshow = handlers.get('pageshow')
  if (pageshow === undefined) {
    throw new Error('the door page listens to no pageshow')
  }
  await withAFakedTick(pageshow)
  const next = assigned[0]
  return next.startsWith('/door.html') ? throughTheDoors(next, storage) : next
}

export async function whereTheTabEndsUp(
  address: string,
  storage: TheStorage,
  browser: NavigationTimingOfABrowser,
): Promise<string> {
  const { navigated } = runTheBoot(address, storage, browser)
  if (navigated.length === 0) {
    return address
  }
  return whereTheTabEndsUp(await throughTheDoors(navigated[0], storage), storage, aBrowserReporting('navigate'))
}

export async function aTabOpenedThroughTheStartButtonAt(address: string): Promise<TheStorage> {
  sessionStorage.clear()
  window.history.replaceState({}, '', address)
  const { buildBackButtonTrap } = await import('../../src/shared/router/backButtonTrap')
  buildBackButtonTrap()
  const storage: TheStorage = new Map()
  for (let index = 0; index < sessionStorage.length; index += 1) {
    const key = sessionStorage.key(index)
    if (key !== null) {
      storage.set(key, sessionStorage.getItem(key) ?? '')
    }
  }
  return storage
}
