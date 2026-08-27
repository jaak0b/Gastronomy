import { defineStore } from 'pinia'
import { ref } from 'vue'
import { listFrom, request } from '../../api/client'
import { adminErrorMessage, type AdminErrorMessage } from '../../core/adminErrorMessage'

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
  transportKind: TransportKind
  host: string | null
  port: number
  agentIdentifier: string | null
  charactersPerLine: number
  codePageName: string
  connectTimeoutSeconds: number
  jobTimeoutSeconds: number
  heartbeatSeconds: number
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
  const loadFailed = ref(false)
  const errorMessage = ref<AdminErrorMessage | null>(null)

  async function load(): Promise<void> {
    loadFailed.value = false
    const result = await request<unknown>('/api/admin/printers')
    if (result.kind !== 'ok') {
      loadFailed.value = true
      return
    }
    const rows = listFrom<AdminPrinter>(result.data, 'printers')
    if (rows === null) {
      loadFailed.value = true
      return
    }
    printers.value = rows
  }

  async function save(printer: AdminPrinter): Promise<void> {
    errorMessage.value = null
    const result = await request(`/api/admin/printers/${printer.locationId}`, {
      method: 'PUT',
      body: {
        transportKind: printer.transportKind,
        host: printer.host,
        port: printer.port,
        agentIdentifier: printer.agentIdentifier,
        charactersPerLine: printer.charactersPerLine,
        codePageName: printer.codePageName,
        connectTimeoutSeconds: printer.connectTimeoutSeconds,
        jobTimeoutSeconds: printer.jobTimeoutSeconds,
        heartbeatSeconds: printer.heartbeatSeconds,
        isEnabled: printer.isEnabled,
      },
    })
    if (result.kind !== 'ok') {
      errorMessage.value = adminErrorMessage(result.kind === 'error' ? result.body : null)
      return
    }
    await load()
  }

  async function testPrint(locationId: string): Promise<void> {
    errorMessage.value = null
    const result = await request(`/api/admin/printers/${locationId}/test-print`, { method: 'POST' })
    if (result.kind !== 'ok') {
      errorMessage.value = adminErrorMessage(result.kind === 'error' ? result.body : null)
    }
  }

  async function reconnect(locationId: string): Promise<void> {
    errorMessage.value = null
    const result = await request(`/api/admin/printers/${locationId}/reconnect`, { method: 'POST' })
    if (result.kind !== 'ok') {
      errorMessage.value = adminErrorMessage(result.kind === 'error' ? result.body : null)
      return
    }
    await load()
  }

  async function setMockFault(
    locationId: string,
    fault: MockFault,
    mode: MockFaultMode,
  ): Promise<void> {
    errorMessage.value = null
    const result = await request(`/api/admin/mock/${locationId}/fault`, {
      method: 'POST',
      body: { fault, mode },
    })
    if (result.kind !== 'ok') {
      errorMessage.value = adminErrorMessage(result.kind === 'error' ? result.body : null)
      return
    }
    await load()
  }

  return { printers, loadFailed, errorMessage, load, save, testPrint, reconnect, setMockFault }
})
