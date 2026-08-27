import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'

const startResult = { shouldFail: false }

vi.mock('@microsoft/signalr', () => {
  class HubConnectionBuilder {
    withUrl() {
      return this
    }
    withAutomaticReconnect() {
      return this
    }
    build() {
      return {
        state: 'Disconnected',
        on: () => undefined,
        onreconnecting: () => undefined,
        onreconnected: () => undefined,
        onclose: () => undefined,
        start: async () => {
          if (startResult.shouldFail) {
            throw new Error('no hub')
          }
        },
        stop: async () => undefined,
      }
    }
  }
  return { HubConnectionBuilder, HubConnectionState: { Disconnected: 'Disconnected' } }
})

const { useConnectionStore, POLLING_INTERVAL_MS } = await import('../../src/stores/connection')

describe('the connection store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.useFakeTimers()
    startResult.shouldFail = false
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('refetches once as soon as the hub is connected, rather than working out what it missed', async () => {
    const connection = useConnectionStore()
    let refetchCount = 0
    connection.registerRefetch(async () => {
      refetchCount += 1
    })

    await connection.connect('a-token')

    expect(refetchCount).toBe(1)
  })

  it('reports itself connected once the hub is up', async () => {
    const connection = useConnectionStore()

    await connection.connect('a-token')

    expect(connection.state).toBe('connected')
  })

  it('runs no polling timer while the hub is connected', async () => {
    const connection = useConnectionStore()
    let refetchCount = 0
    connection.registerRefetch(async () => {
      refetchCount += 1
    })

    await connection.connect('a-token')
    await vi.advanceTimersByTimeAsync(POLLING_INTERVAL_MS * 4)

    expect(refetchCount).toBe(1)
  })

  it('reports itself offline when the hub never comes up', async () => {
    startResult.shouldFail = true
    const connection = useConnectionStore()

    await connection.connect('a-token')

    expect(connection.state).toBe('offline')
  })

  it('falls back to polling every fifteen seconds when the hub never comes up', async () => {
    startResult.shouldFail = true
    const connection = useConnectionStore()
    let refetchCount = 0
    connection.registerRefetch(async () => {
      refetchCount += 1
    })

    await connection.connect('a-token')
    await vi.advanceTimersByTimeAsync(45000)

    expect(refetchCount).toBe(3)
  })

  it('stops polling once the hub is disconnected deliberately', async () => {
    startResult.shouldFail = true
    const connection = useConnectionStore()
    let refetchCount = 0
    connection.registerRefetch(async () => {
      refetchCount += 1
    })

    await connection.connect('a-token')
    await connection.disconnect()
    await vi.advanceTimersByTimeAsync(45000)

    expect(refetchCount).toBe(0)
  })
})
