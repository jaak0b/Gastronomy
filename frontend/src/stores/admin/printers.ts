import { defineStore } from 'pinia'
import { ref } from 'vue'
import { request } from '../../api/client'

export type TransportKind = 'Network' | 'Agent' | 'Mock'

export type MockFault =
  | 'None'
  | 'PaperEnd'
  | 'CoverOpen'
  | 'ConnectTimeout'
  | 'DropSocketEarly'
  | 'DropSocketMidJob'
  | 'UnknownOutcome'

export type MockFaultMode = 'Once' | 'Sticky'

export interface AdminPrinter {
  locationId: string
  locationName: string
  transport: TransportKind
  host: string | null
  port: number | null
  isEnabled: boolean
  isOnline: boolean
  isPaperEnd: boolean
  isPaperNearEnd: boolean
  isCoverOpen: boolean
  isFaulty: boolean
  waitingTicketCount: number
  lastChangedAtUtc: string | null
  sharedWithLocationNames: string[]
  mockFolderPath: string | null
}

export const useAdminPrintersStore = defineStore('adminPrinters', () => {
  const printers = ref<AdminPrinter[]>([])

  async function load(): Promise<void> {
    const result = await request<{ printers: AdminPrinter[] }>('/api/admin/printers')
    if (result.kind === 'ok') {
      printers.value = result.data.printers
    }
  }

  async function save(printer: AdminPrinter): Promise<void> {
    await request(`/api/admin/printers/${printer.locationId}`, {
      method: 'PUT',
      body: {
        transport: printer.transport,
        host: printer.host,
        port: printer.port,
        isEnabled: printer.isEnabled,
      },
    })
    await load()
  }

  async function testPrint(locationId: string): Promise<void> {
    await request(`/api/admin/printers/${locationId}/test-print`, { method: 'POST' })
  }

  async function reconnect(locationId: string): Promise<void> {
    await request(`/api/admin/printers/${locationId}/reconnect`, { method: 'POST' })
    await load()
  }

  async function setMockFault(
    locationId: string,
    fault: MockFault,
    mode: MockFaultMode,
  ): Promise<void> {
    await request(`/api/admin/mock/${locationId}/fault`, {
      method: 'POST',
      body: { fault, mode },
    })
    await load()
  }

  return { printers, load, save, testPrint, reconnect, setMockFault }
})
