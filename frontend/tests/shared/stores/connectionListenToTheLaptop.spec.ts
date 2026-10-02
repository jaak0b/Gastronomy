import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { fireHubEvent, forgetHubEvents, hubEventsRegistered } from '../../support/hubConnection'

vi.mock('@microsoft/signalr', async () => (await import('../../support/hubConnection')).signalrModuleFake())

const { useConnectionStore } = await import('../../../src/shared/stores/connection')

function countingReload(): { reload: () => Promise<void>; reloads: () => number } {
  let count = 0
  return {
    reload: async () => {
      count += 1
    },
    reloads: () => count,
  }
}

describe('a store listening to the laptop', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    forgetHubEvents()
  })

  it('reloads when the connection refetches everything', async () => {
    const connection = useConnectionStore()
    const { reload, reloads } = countingReload()
    connection.listenToTheLaptop(['ConfigurationChanged'], reload)

    await connection.refetchAll()

    expect(reloads()).toBe(1)
  })

  it('reloads on each event it named', async () => {
    const connection = useConnectionStore()
    const { reload, reloads } = countingReload()
    connection.listenToTheLaptop(['ConfigurationChanged', 'OrdersChanged'], reload)
    await connection.connect({})
    const reloadsAfterConnecting = reloads()

    fireHubEvent('ConfigurationChanged')
    fireHubEvent('OrdersChanged')

    expect(reloadsAfterConnecting).toBe(1)
    expect(reloads()).toBe(3)
  })

  it('ignores an event it did not name', async () => {
    const connection = useConnectionStore()
    const { reload, reloads } = countingReload()
    connection.listenToTheLaptop(['ConfigurationChanged'], reload)
    await connection.connect({})

    fireHubEvent('OrdersChanged')

    expect(reloads()).toBe(1)
  })

  it('stops refetching and stops hearing every event once released', async () => {
    const connection = useConnectionStore()
    const { reload, reloads } = countingReload()
    const release = connection.listenToTheLaptop(['ConfigurationChanged', 'OrdersChanged'], reload)
    await connection.connect({})

    release()
    await connection.refetchAll()
    fireHubEvent('ConfigurationChanged')
    fireHubEvent('OrdersChanged')

    expect(reloads()).toBe(1)
    expect(hubEventsRegistered).toEqual([])
  })
})
