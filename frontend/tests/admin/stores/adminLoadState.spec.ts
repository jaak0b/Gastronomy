import { readFileSync } from 'node:fs'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { ref } from 'vue'
import { sourceFilesUnder } from '../../support/sourceFiles'

vi.mock('@microsoft/signalr', async () => (await import('../../support/hubConnection')).signalrModuleFake())

const { useConnectionStore } = await import('../../../src/shared/stores/connection')
const { useAdminLoadStateStore } = await import('../../../src/admin/stores/adminLoadState')

const ADMIN_STORES = `${process.cwd()}/src/admin/stores`

function reloadThatDoesNothing(): Promise<void> {
  return Promise.resolve()
}

describe('the load state of the admin while the laptop is connected', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    useConnectionStore().state = 'connected'
  })

  it('shows no failure while every list on screen loaded', () => {
    useAdminLoadStateStore().listenToTheLaptop(['ConfigurationChanged'], reloadThatDoesNothing, ref(false))

    expect(useAdminLoadStateStore().showsLoadFailure).toBe(false)
  })

  it('shows a failure while the last load of one list on screen failed', () => {
    useAdminLoadStateStore().listenToTheLaptop(['ConfigurationChanged'], reloadThatDoesNothing, ref(false))
    useAdminLoadStateStore().listenToTheLaptop(['ConfigurationChanged'], reloadThatDoesNothing, ref(true))

    expect(useAdminLoadStateStore().showsLoadFailure).toBe(true)
  })

  it('stops showing the failure once that list loads again', () => {
    const lastLoadFailed = ref(true)
    useAdminLoadStateStore().listenToTheLaptop(['ConfigurationChanged'], reloadThatDoesNothing, lastLoadFailed)

    lastLoadFailed.value = false

    expect(useAdminLoadStateStore().showsLoadFailure).toBe(false)
  })

  it('forgets a failed list once the screen that showed it has closed', () => {
    const stopListening = useAdminLoadStateStore().listenToTheLaptop(
      ['ConfigurationChanged'],
      reloadThatDoesNothing,
      ref(true),
    )

    stopListening()

    expect(useAdminLoadStateStore().showsLoadFailure).toBe(false)
  })

  it('keeps a failed list while a second screen part still shows it', () => {
    const lastLoadFailed = ref(true)
    const stopTheFirst = useAdminLoadStateStore().listenToTheLaptop(['ConfigurationChanged'], reloadThatDoesNothing, lastLoadFailed)
    useAdminLoadStateStore().listenToTheLaptop(['ConfigurationChanged'], reloadThatDoesNothing, lastLoadFailed)

    stopTheFirst()

    expect(useAdminLoadStateStore().showsLoadFailure).toBe(true)
  })
})

describe('the load state of the admin while the connection to the laptop changes', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it.each([
    ['connecting', false],
    ['connected', false],
    ['reconnecting', true],
    ['offline', true],
  ] as const)('shows a failure while the connection is %s: %s', (state, showsLoadFailure) => {
    useConnectionStore().state = state

    expect(useAdminLoadStateStore().showsLoadFailure).toBe(showsLoadFailure)
  })
})

describe('the admin stores that can fail to load', () => {
  it('all listen through the admin load state, so the bar covers them', () => {
    const storesListeningPastIt = sourceFilesUnder(ADMIN_STORES).filter((file) => {
      const source = readFileSync(file, 'utf8')
      return (
        source.includes('defineStore(')
        && /\bloadFailed\b/.test(source)
        && !source.includes('useAdminLoadStateStore().listenToTheLaptop(')
      )
    })

    expect(storesListeningPastIt).toEqual([])
  })
})
