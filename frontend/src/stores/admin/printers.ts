import { defineStore } from 'pinia'
import { ref } from 'vue'
import { listFrom, request } from '../../api/client'
import { adminErrorMessage, type AdminErrorMessage } from '../../core/adminErrorMessage'

export type PrinterType = 'TestPrinter' | 'EpsonTmT20ivNetworkPrinter'

export type SimulatedFault =
  | 'None'
  | 'PaperEnd'
  | 'CoverOpen'
  | 'ConnectTimeout'
  | 'DropSocketEarly'
  | 'DropSocketMidJob'
  | 'UnknownOutcome'

export type SimulatedFaultMode = 'Once' | 'Sticky'

interface AdminPrinterCommon {
  printerId: string
  name: string
  isOnline: boolean
  isPaperEnd: boolean
  isPaperNearEnd: boolean
  isCoverOpen: boolean
  isFaulty: boolean
  waitingTicketCount: number
  lastChangedAtUtc: string | null
  statusDetail: string | null
  stationNames: string[]
}

export interface AdminTestPrinter extends AdminPrinterCommon {
  printerType: 'TestPrinter'
  simulatedFault: SimulatedFault
  simulatedFaultMode: SimulatedFaultMode
}

export interface AdminNetworkPrinter extends AdminPrinterCommon {
  printerType: 'EpsonTmT20ivNetworkPrinter'
  host: string
  port: number
}

export type AdminPrinter = AdminTestPrinter | AdminNetworkPrinter

export type SavePrinter =
  | {
      printerType: 'TestPrinter'
      printerId?: string
      name: string
      simulatedFault: SimulatedFault
      simulatedFaultMode: SimulatedFaultMode
    }
  | {
      printerType: 'EpsonTmT20ivNetworkPrinter'
      printerId?: string
      name: string
      host: string
      port: number
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

  async function save(printer: SavePrinter): Promise<void> {
    errorMessage.value = null
    const isNew = printer.printerId === undefined
    const result = await request(
      isNew ? '/api/admin/printers' : `/api/admin/printers/${printer.printerId}`,
      {
        method: isNew ? 'POST' : 'PUT',
        body: bodyOf(printer),
      },
    )
    if (result.kind !== 'ok') {
      errorMessage.value = adminErrorMessage(result.kind === 'error' ? result.body : null)
      return
    }
    await load()
  }

  async function remove(printerId: string): Promise<void> {
    errorMessage.value = null
    const result = await request(`/api/admin/printers/${printerId}`, { method: 'DELETE' })
    if (result.kind !== 'ok') {
      errorMessage.value = adminErrorMessage(result.kind === 'error' ? result.body : null)
      return
    }
    await load()
  }

  async function testPrint(printerId: string): Promise<void> {
    errorMessage.value = null
    const result = await request(`/api/admin/printers/${printerId}/test-print`, { method: 'POST' })
    if (result.kind !== 'ok') {
      errorMessage.value = adminErrorMessage(result.kind === 'error' ? result.body : null)
      return
    }
    await load()
  }

  async function reconnect(printerId: string): Promise<void> {
    await act(printerId, 'reconnect')
  }

  async function act(printerId: string, action: string): Promise<void> {
    errorMessage.value = null
    const result = await request(`/api/admin/printers/${printerId}/${action}`, { method: 'POST' })
    if (result.kind !== 'ok') {
      errorMessage.value = adminErrorMessage(result.kind === 'error' ? result.body : null)
      return
    }
    await load()
  }

  function bodyOf(printer: SavePrinter): Record<string, unknown> {
    if (printer.printerType === 'EpsonTmT20ivNetworkPrinter') {
      return {
        printerType: printer.printerType,
        name: printer.name,
        host: printer.host,
        port: printer.port,
      }
    }

    return {
      printerType: printer.printerType,
      name: printer.name,
      simulatedFault: printer.simulatedFault,
      simulatedFaultMode: printer.simulatedFaultMode,
    }
  }

  return { printers, loadFailed, errorMessage, load, save, remove, testPrint, reconnect }
})
