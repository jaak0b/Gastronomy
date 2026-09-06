export const hubEventsRegistered: string[] = []

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
        on: (eventName: string) => {
          hubEventsRegistered.push(eventName)
        },
        off: (eventName: string) => {
          const index = hubEventsRegistered.indexOf(eventName)
          if (index !== -1) {
            hubEventsRegistered.splice(index, 1)
          }
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
