export const hubEventsRegistered: string[] = []

const handlersByEventName = new Map<string, ((payload: unknown) => void)[]>()

export function forgetHubEvents(): void {
  hubEventsRegistered.length = 0
  handlersByEventName.clear()
}

export function fireHubEvent(eventName: string, payload: unknown = {}): void {
  for (const handler of handlersByEventName.get(eventName) ?? []) {
    handler(payload)
  }
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
      return {
        state: 'Disconnected',
        on: (eventName: string, handler: (payload: unknown) => void) => {
          hubEventsRegistered.push(eventName)
          handlersByEventName.set(eventName, [
            ...(handlersByEventName.get(eventName) ?? []),
            handler,
          ])
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
        },
        onreconnecting: () => undefined,
        onreconnected: () => undefined,
        onclose: () => undefined,
        start: async () => undefined,
        stop: async () => undefined,
      }
    }
  }
  return { HubConnectionBuilder, HubConnectionState: { Disconnected: 'Disconnected' } }
}
