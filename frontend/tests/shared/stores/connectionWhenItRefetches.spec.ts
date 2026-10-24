import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import {
  forgetHubEvents,
  holdBackGroupsJoined,
  theConnectionCloses,
  theLaptopSendsGroupsJoined,
  theWifiComesBack,
} from '../../support/hubConnection'

vi.mock('@microsoft/signalr', async () => (await import('../../support/hubConnection')).signalrModuleFake())

const { useConnectionStore, POLLING_INTERVAL_MS } = await import('../../../src/shared/stores/connection')

describe('a tablet that connects to the laptop', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    forgetHubEvents()
  })

  it('reloads once the laptop has put it into its groups, not as soon as the connection starts', async () => {
    holdBackGroupsJoined()
    const connection = useConnectionStore()
    let refetchCount = 0
    connection.registerRefetch(async () => {
      refetchCount += 1
    })
    await connection.connect({ deviceToken: 'a-token' })
    const refetchesOnceConnected = refetchCount

    theLaptopSendsGroupsJoined()

    expect([refetchesOnceConnected, refetchCount]).toEqual([0, 1])
  })
})

describe('a tablet that reconnects after the WiFi dropped', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    forgetHubEvents()
  })

  it('reloads once the laptop has put the new connection into its groups, not as soon as it is back', async () => {
    holdBackGroupsJoined()
    const connection = useConnectionStore()
    let refetchCount = 0
    connection.registerRefetch(async () => {
      refetchCount += 1
    })
    await connection.connect({ deviceToken: 'a-token' })
    theLaptopSendsGroupsJoined()

    theWifiComesBack()
    const refetchesOnceBack = refetchCount
    theLaptopSendsGroupsJoined()

    expect([refetchesOnceBack, refetchCount]).toEqual([1, 2])
  })
})

describe('a tablet whose connection to the laptop has closed', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    forgetHubEvents()
    vi.useFakeTimers()
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('falls back to reloading every fifteen seconds once the connection has closed', async () => {
    const connection = useConnectionStore()
    let refetchCount = 0
    connection.registerRefetch(async () => {
      refetchCount += 1
    })
    await connection.connect({ deviceToken: 'a-token' })
    const refetchesBeforeTheConnectionCloses = refetchCount

    theConnectionCloses()
    await vi.advanceTimersByTimeAsync(POLLING_INTERVAL_MS)

    expect([refetchesBeforeTheConnectionCloses, refetchCount]).toEqual([1, 2])
  })
})
