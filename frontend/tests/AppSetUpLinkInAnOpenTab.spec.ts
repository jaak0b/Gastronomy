import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { enableAutoUnmount, flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { BACK_BUTTON_TRAP_RETURN_PATH_KEY } from '../src/shared/router/backButtonTrap'
import {
  aBrowserReporting,
  aTabOpenedThroughTheStartButtonAt,
  whereTheTabEndsUp,
  type TheStorage,
} from './support/doorScripts'
import { answer, stubLaptopAt, type StubbedLaptop } from './support/laptop'
import { testPlugins } from './support/plugins'
import { waitUntil } from './support/dom'

vi.mock('@microsoft/signalr', async () => (await import('./support/hubConnection')).signalrModuleFake())

enableAutoUnmount(afterEach)

const A_WAITER_PHONE = {
  deviceId: 'device-of-the-phone',
  deviceToken: 'token-of-the-phone',
  staffMember: { id: 'staff-1', name: 'Anna' },
  station: null,
  language: 'de',
}

const A_STATION_TABLET = {
  deviceId: 'device-of-the-tablet',
  deviceToken: 'token-of-the-tablet',
  staffMember: null,
  station: { id: 'station-kueche', name: 'Küche' },
  language: 'de',
}

function aLaptopThatSetsUp(device: typeof A_WAITER_PHONE | typeof A_STATION_TABLET): StubbedLaptop {
  return stubLaptopAt({
    '/api/enrolment/redeem': answer(device),
    '/api/session': answer(A_STATION_TABLET),
  }).answersEverythingElse(
    answer({ festival: null, categories: [], items: [], stations: [], station: A_STATION_TABLET.station, orders: [], asItComes: [] }),
  )
}

async function theTabLoads(address: string, storage: TheStorage): Promise<void> {
  const landedAt = await whereTheTabEndsUp(address, storage, aBrowserReporting('navigate'))
  sessionStorage.clear()
  for (const [key, value] of storage) {
    sessionStorage.setItem(key, value)
  }
  window.history.replaceState({}, '', landedAt)
  vi.resetModules()
  const App = (await import('../src/App.vue')).default
  mount(App, { global: { plugins: testPlugins() }, attachTo: document.body })
  await flushPromises()
}

beforeEach(() => {
  vi.resetModules()
  setActivePinia(createPinia())
  localStorage.clear()
  document.body.innerHTML = ''
})

describe('a set-up link pasted into a tab that already shows the app', () => {
  it('redeems its own code for a waiter, not the older link the tab was opened with', async () => {
    const laptop = aLaptopThatSetsUp(A_WAITER_PHONE)
    const storage = await aTabOpenedThroughTheStartButtonAt('/j/OLD-WAITER')

    await theTabLoads('/j/NEW-WAITER', storage)

    await waitUntil(() =>
      expect(laptop.bodiesSentTo('POST', '/api/enrolment/redeem')).toEqual([expect.objectContaining({ code: 'NEW-WAITER' })]),
    )
  })

  it('redeems its own code for a station tablet that shows the station screen', async () => {
    localStorage.setItem('deviceToken', 'lookup.of-the-tablet')
    const laptop = aLaptopThatSetsUp(A_STATION_TABLET)
    const storage = await aTabOpenedThroughTheStartButtonAt('/j/OLD-TABLET')
    storage.set(BACK_BUTTON_TRAP_RETURN_PATH_KEY, '/stations')

    await theTabLoads('/j/NEW-TABLET', storage)

    await waitUntil(() =>
      expect(laptop.bodiesSentTo('POST', '/api/enrolment/redeem')).toEqual([expect.objectContaining({ code: 'NEW-TABLET' })]),
    )
  })
})
