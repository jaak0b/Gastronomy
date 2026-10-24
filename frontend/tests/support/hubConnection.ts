export const hubEventsRegistered: string[] = []

interface ConnectionListeners {
  readonly groupsJoined: ((payload: unknown) => void)[]
  readonly reconnected: (() => void)[]
  readonly closed: (() => void)[]
}

const handlersByEventName = new Map<string, ((payload: unknown) => void)[]>()
let groupsJoinedIsHeldBack = false
let lastStartedConnectionListeners: ConnectionListeners | null = null

export function forgetHubEvents(): void {
  hubEventsRegistered.length = 0
  handlersByEventName.clear()
  groupsJoinedIsHeldBack = false
  lastStartedConnectionListeners = null
}

export function fireHubEvent(eventName: string, payload: unknown = {}): void {
  for (const handler of handlersByEventName.get(eventName) ?? []) {
    handler(payload)
  }
}

export function hubEventsOtherThanGroupsJoined(): string[] {
  return hubEventsRegistered.filter((eventName) => eventName !== 'GroupsJoined')
}

export function holdBackGroupsJoined(): void {
  groupsJoinedIsHeldBack = true
}

export function theLaptopSendsGroupsJoined(): void {
  for (const handler of listenersOfTheLastStartedConnection().groupsJoined) {
    handler(undefined)
  }
}

export function theWifiComesBack(): void {
  for (const callback of listenersOfTheLastStartedConnection().reconnected) {
    callback()
  }
}

export function theConnectionCloses(): void {
  for (const callback of listenersOfTheLastStartedConnection().closed) {
    callback()
  }
}

function listenersOfTheLastStartedConnection(): ConnectionListeners {
  if (lastStartedConnectionListeners === null) {
    throw new Error('No hub connection has been started in this test.')
  }
  return lastStartedConnectionListeners
}

export function signalrModuleFake() {
  class HubConnectionBuilder {
    withUrl() {
      return this
    }
    withAutomaticReconnect() {
      return this
    }
    build() {
      const listeners: ConnectionListeners = { groupsJoined: [], reconnected: [], closed: [] }
      return {
        state: 'Disconnected',
        on: (eventName: string, handler: (payload: unknown) => void) => {
          hubEventsRegistered.push(eventName)
          handlersByEventName.set(eventName, [
            ...(handlersByEventName.get(eventName) ?? []),
            handler,
          ])
          if (eventName === 'GroupsJoined') {
            listeners.groupsJoined.push(handler)
          }
        },
        off: (eventName: string, handler: (payload: unknown) => void) => {
          const index = hubEventsRegistered.indexOf(eventName)
          if (index !== -1) {
            hubEventsRegistered.splice(index, 1)
          }
          handlersByEventName.set(
            eventName,
            (handlersByEventName.get(eventName) ?? []).filter(
              (candidate) => candidate !== handler,
            ),
          )
          const groupsJoinedIndex = listeners.groupsJoined.indexOf(handler)
          if (groupsJoinedIndex !== -1) {
            listeners.groupsJoined.splice(groupsJoinedIndex, 1)
          }
        },
        onreconnecting: () => undefined,
        onreconnected: (callback: () => void) => {
          listeners.reconnected.push(callback)
        },
        onclose: (callback: () => void) => {
          listeners.closed.push(callback)
        },
        start: () => {
          lastStartedConnectionListeners = listeners
          const started = Promise.resolve()
          if (!groupsJoinedIsHeldBack) {
            void started.then(() => {
              for (const handler of listeners.groupsJoined) {
                handler(undefined)
              }
            })
          }
          return started
        },
        stop: async () => undefined,
      }
    }
  }
  return { HubConnectionBuilder, HubConnectionState: { Disconnected: 'Disconnected' } }
}
