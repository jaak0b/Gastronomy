import { flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import type { Component } from 'vue'
import { testPlugins } from './plugins'

export const THE_PAGE_BEFORE_THE_APP = '/the-page-before-the-app'

export function aBrowserThatCountsTheTap(): void {
  Object.defineProperty(window.navigator, 'userActivation', {
    configurable: true,
    value: { isActive: true, hasBeenActive: true },
  })
}

export function aBrowserWithoutUserActivation(): void {
  Reflect.deleteProperty(window.navigator, 'userActivation')
}

export function aTabThatOpenedTheAppAt(path: string): void {
  sessionStorage.clear()
  sessionStorage.setItem('theDoorAnchor', 'yes')
  window.history.replaceState({}, '', THE_PAGE_BEFORE_THE_APP)
  window.history.pushState({}, '', path)
}

export async function shownOnAStartedRouterAt(
  path: string,
  load: () => Promise<{ default: Component }>,
  props: Record<string, unknown>,
): Promise<VueWrapper> {
  aTabThatOpenedTheAppAt(path)
  const { startRouter } = await import('../../src/shared/router/router')
  startRouter()
  const shown = mount((await load()).default, { props, global: { plugins: testPlugins() }, attachTo: document.body })
  await flushPromises()
  return shown
}

export function theNextMoveThroughTheHistory(): Promise<void> {
  return new Promise<void>((resolve) => {
    window.addEventListener('popstate', () => resolve(), { once: true })
  })
}

export async function pressBack(): Promise<void> {
  const arrived = theNextMoveThroughTheHistory()
  window.history.back()
  await arrived
  await flushPromises()
}

export function theAppWasLeftByBack(): boolean {
  return window.location.pathname === THE_PAGE_BEFORE_THE_APP
}
