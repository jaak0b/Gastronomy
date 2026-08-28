import { defineStore } from 'pinia'
import { ref } from 'vue'
import { HubConnectionBuilder, HubConnectionState, type HubConnection } from '@microsoft/signalr'

export type ConnectionState = 'connected' | 'reconnecting' | 'offline'

export interface HubCredential {
  deviceToken?: string
}

export const POLLING_INTERVAL_MS = 15000
export const RECONNECT_DELAYS_MS = [0, 2000, 5000, 10000, 30000]
export const RECOVERED_NOTICE_MS = 5000

export const useConnectionStore = defineStore('connection', () => {
  const state = ref<ConnectionState>('offline')
  const recovered = ref(false)
  const refetchCallbacks: (() => Promise<void>)[] = []
  const eventHandlers = new Map<string, ((payload: unknown) => void)[]>()
  let connection: HubConnection | null = null
  let pollingTimer: ReturnType<typeof setInterval> | null = null
  let recoveredNoticeTimer: ReturnType<typeof setTimeout> | null = null

  function showRecoveredNotice(): void {
    recovered.value = true
    if (recoveredNoticeTimer !== null) {
      clearTimeout(recoveredNoticeTimer)
    }
    recoveredNoticeTimer = setTimeout(() => {
      recovered.value = false
      recoveredNoticeTimer = null
    }, RECOVERED_NOTICE_MS)
  }

  function registerRefetch(callback: () => Promise<void>): () => void {
    refetchCallbacks.push(callback)
    return () => {
      const index = refetchCallbacks.indexOf(callback)
      if (index !== -1) {
        refetchCallbacks.splice(index, 1)
      }
    }
  }

  async function refetchAll(): Promise<void> {
    for (const callback of refetchCallbacks) {
      await callback()
    }
  }

  function onEvent<T>(eventName: string, handler: (payload: T) => void): () => void {
    const registered = handler as (payload: unknown) => void
    const handlers = eventHandlers.get(eventName) ?? []
    handlers.push(registered)
    eventHandlers.set(eventName, handlers)
    connection?.on(eventName, registered)
    return () => {
      const remaining = (eventHandlers.get(eventName) ?? []).filter(
        (candidate) => candidate !== registered,
      )
      eventHandlers.set(eventName, remaining)
      connection?.off(eventName, registered)
    }
  }

  function startPolling(): void {
    if (pollingTimer !== null) {
      return
    }
    pollingTimer = setInterval(() => {
      void refetchAll()
    }, POLLING_INTERVAL_MS)
  }

  function stopPolling(): void {
    if (pollingTimer === null) {
      return
    }
    clearInterval(pollingTimer)
    pollingTimer = null
  }

  async function connect(credential: HubCredential): Promise<void> {
    if (connection !== null) {
      return
    }
    const built = new HubConnectionBuilder()
      .withUrl(
        credential.deviceToken === undefined
          ? '/hub'
          : `/hub?access_token=${encodeURIComponent(credential.deviceToken)}`,
      )
      .withAutomaticReconnect([...RECONNECT_DELAYS_MS])
      .build()
    connection = built
    for (const [eventName, handlers] of eventHandlers) {
      for (const handler of handlers) {
        built.on(eventName, handler)
      }
    }
    built.onreconnecting(() => {
      state.value = 'reconnecting'
      recovered.value = false
    })
    built.onreconnected(() => {
      state.value = 'connected'
      showRecoveredNotice()
      stopPolling()
      void refetchAll()
    })
    built.onclose(() => {
      state.value = 'offline'
      recovered.value = false
      startPolling()
    })
    try {
      await built.start()
      state.value = 'connected'
      stopPolling()
      await refetchAll()
    } catch {
      state.value = 'offline'
      startPolling()
    }
  }

  async function disconnect(): Promise<void> {
    stopPolling()
    if (recoveredNoticeTimer !== null) {
      clearTimeout(recoveredNoticeTimer)
      recoveredNoticeTimer = null
    }
    recovered.value = false
    if (connection !== null && connection.state !== HubConnectionState.Disconnected) {
      await connection.stop()
    }
    connection = null
    state.value = 'offline'
  }

  return {
    state,
    recovered,
    registerRefetch,
    refetchAll,
    onEvent,
    connect,
    disconnect,
  }
})
