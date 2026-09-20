import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'

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
        off: () => undefined,
        onreconnecting: () => undefined,
        onreconnected: () => undefined,
        onclose: () => undefined,
        start: async () => undefined,
        stop: async () => undefined,
      }
    }
  }
  return { HubConnectionBuilder, HubConnectionState: { Disconnected: 'Disconnected' } }
})

const { useConnectionStore } = await import('../../../src/shared/stores/connection')

describe('a refetch whose request never answers', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('does not stop the open items from being fetched again', async () => {
    const connection = useConnectionStore()
    const openItemsLoads: number[] = []
    connection.registerRefetch(() => new Promise<void>(() => undefined))
    connection.registerRefetch(async () => {
      openItemsLoads.push(openItemsLoads.length + 1)
    })

    void connection.refetchAll()
    await new Promise((resolve) => setTimeout(resolve, 20))

    expect(openItemsLoads).toEqual([1])
  })
})
