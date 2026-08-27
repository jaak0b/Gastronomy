import { defineStore } from 'pinia'
import { ref } from 'vue'
import { request } from '../api/client'
import { useConnectionStore } from './connection'
import { useSessionStore } from './session'
import type { PrinterStatusRow, StationTicketRow } from '../core/apiTypes'

export const TAKE_DELAY_SECONDS = 10

export interface Station {
  stationId: string
  name: string
  canPrint: boolean
}

export const useStationStore = defineStore('station', () => {
  const stations = ref<Station[]>([])
  const selectedStationId = ref<string | null>(null)
  const tickets = ref<StationTicketRow[]>([])
  const printer = ref<PrinterStatusRow | null>(null)
  const loadFailed = ref(false)
  const pendingTicketIds = ref<string[]>([])
  const noticeKeyByTicketId = ref<Record<string, string>>({})
  const pendingTimers = new Map<string, ReturnType<typeof setTimeout>>()

  function deviceToken(): string | null {
    return useSessionStore().deviceToken
  }

  async function loadStations(): Promise<void> {
    loadFailed.value = false
    const result = await request<{ stations: Station[] }>('/api/stations', {
      token: deviceToken(),
    })
    if (result.kind !== 'ok') {
      loadFailed.value = true
      return
    }
    stations.value = result.data.stations
    if (selectedStationId.value === null && result.data.stations.length > 0) {
      selectedStationId.value = result.data.stations[0].stationId
    }
  }

  async function loadTickets(): Promise<void> {
    if (selectedStationId.value === null) {
      return
    }
    const result = await request<{ tickets: StationTicketRow[] }>(
      `/api/stations/${selectedStationId.value}/tickets`,
      { token: deviceToken() },
    )
    if (result.kind === 'ok') {
      tickets.value = result.data.tickets
    }
  }

  async function loadPrinter(): Promise<void> {
    if (selectedStationId.value === null) {
      return
    }
    const result = await request<PrinterStatusRow>(
      `/api/stations/${selectedStationId.value}/status`,
      { token: deviceToken() },
    )
    if (result.kind === 'ok') {
      printer.value = result.data
    }
  }

  function isPending(ticketId: string): boolean {
    return pendingTicketIds.value.includes(ticketId)
  }

  async function sendAcknowledge(ticketId: string): Promise<void> {
    const ticket = tickets.value.find((row) => row.ticketId === ticketId)
    if (ticket === undefined) {
      return
    }
    const result = await request(
      `/api/stations/${selectedStationId.value}/tickets/${ticket.ticketId}/acknowledge`,
      { method: 'POST', token: deviceToken() },
    )
    if (result.kind === 'error') {
      noticeKeyByTicketId.value = {
        ...noticeKeyByTicketId.value,
        [ticketId]: result.body?.messageKey ?? 'station.takeRefused',
      }
    }
    await loadTickets()
  }

  function beginTake(ticketId: string): void {
    if (isPending(ticketId)) {
      return
    }
    pendingTicketIds.value = [...pendingTicketIds.value, ticketId]
    pendingTimers.set(
      ticketId,
      setTimeout(() => {
        pendingTimers.delete(ticketId)
        pendingTicketIds.value = pendingTicketIds.value.filter((id) => id !== ticketId)
        void sendAcknowledge(ticketId)
      }, TAKE_DELAY_SECONDS * 1000),
    )
  }

  function undoTake(ticketId: string): void {
    const timer = pendingTimers.get(ticketId)
    if (timer !== undefined) {
      clearTimeout(timer)
      pendingTimers.delete(ticketId)
    }
    pendingTicketIds.value = pendingTicketIds.value.filter((id) => id !== ticketId)
  }

  async function refresh(): Promise<void> {
    await loadTickets()
    await loadPrinter()
  }

  function listen(): () => void {
    return useConnectionStore().registerRefetch(refresh)
  }

  async function open(): Promise<void> {
    await loadStations()
    await loadTickets()
    await loadPrinter()
  }

  async function selectStation(stationId: string): Promise<void> {
    selectedStationId.value = stationId
    await loadTickets()
  }

  return {
    stations,
    selectedStationId,
    tickets,
    printer,
    loadFailed,
    pendingTicketIds,
    noticeKeyByTicketId,
    listen,
    refresh,
    open,
    selectStation,
    loadTickets,
    loadPrinter,
    isPending,
    beginTake,
    undoTake,
  }
})
