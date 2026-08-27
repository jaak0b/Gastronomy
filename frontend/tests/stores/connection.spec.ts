import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'

const startResult = { shouldFail: false }
const lastUrl = { value: '' }
const registeredHandlers: { eventName: string; handler: (payload: unknown) => void }[] = []

vi.mock('@microsoft/signalr', () => {
  class HubConnectionBuilder {
    withUrl(url: string) {
      lastUrl.value = url
      return this
    }
    withAutomaticReconnect() {
      return this
    }
    build() {
      return {
        state: 'Disconnected',
        on: (eventName: string, handler: (payload: unknown) => void) => {
          registeredHandlers.push({ eventName, handler })
        },
        off: (eventName: string, handler: (payload: unknown) => void) => {
          const index = registeredHandlers.findIndex(
            (entry) => entry.eventName === eventName && entry.handler === handler,
          )
          if (index !== -1) {
            registeredHandlers.splice(index, 1)
          }
        },
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

describe('the credential the hub is opened with', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    lastUrl.value = ''
  })

  it('is offered as a device token when a phone connects', async () => {
    const connection = useConnectionStore()

    await connection.connect({ deviceToken: 'a-token' })

    expect(lastUrl.value).toContain('access_token=a-token')
  })

})

describe('a handler that is no longer wanted', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    registeredHandlers.length = 0
  })

  it('can be taken off again, so a screen that is reopened does not answer twice', async () => {
    const connection = useConnectionStore()
    await connection.connect({ deviceToken: 'a-token' })

    const stopListening = connection.onEvent('TicketStatusChanged', () => undefined)
    stopListening()

    expect(registeredHandlers.filter((entry) => entry.eventName === 'TicketStatusChanged')).toEqual(
      [],
    )
  })

  it('is not registered a second time when the same screen is opened again', async () => {
    const connection = useConnectionStore()
    const stopListening = connection.onEvent('TicketStatusChanged', () => undefined)
    stopListening()

    await connection.connect({ deviceToken: 'a-token' })

    expect(registeredHandlers.filter((entry) => entry.eventName === 'TicketStatusChanged')).toEqual(
      [],
    )
  })
})

describe('the connection store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.useFakeTimers()
    startResult.shouldFail = false
    lastUrl.value = ''
    registeredHandlers.length = 0
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

    await connection.connect({ deviceToken: 'a-token' })

    expect(refetchCount).toBe(1)
  })

  it('reports itself connected once the hub is up', async () => {
    const connection = useConnectionStore()

    await connection.connect({ deviceToken: 'a-token' })

    expect(connection.state).toBe('connected')
  })

  it('runs no polling timer while the hub is connected', async () => {
    const connection = useConnectionStore()
    let refetchCount = 0
    connection.registerRefetch(async () => {
      refetchCount += 1
    })

    await connection.connect({ deviceToken: 'a-token' })
    await vi.advanceTimersByTimeAsync(POLLING_INTERVAL_MS * 4)

    expect(refetchCount).toBe(1)
  })

  it('reports itself offline when the hub never comes up', async () => {
    startResult.shouldFail = true
    const connection = useConnectionStore()

    await connection.connect({ deviceToken: 'a-token' })

    expect(connection.state).toBe('offline')
  })

  it('falls back to polling every fifteen seconds when the hub never comes up', async () => {
    startResult.shouldFail = true
    const connection = useConnectionStore()
    let refetchCount = 0
    connection.registerRefetch(async () => {
      refetchCount += 1
    })

    await connection.connect({ deviceToken: 'a-token' })
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

    await connection.connect({ deviceToken: 'a-token' })
    await connection.disconnect()
    await vi.advanceTimersByTimeAsync(45000)

    expect(refetchCount).toBe(0)
  })
})
