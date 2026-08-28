import { defineStore } from 'pinia'
import { ref } from 'vue'
import { request } from '../api/client'
import { useConnectionStore } from './connection'
import { useSessionStore } from './session'
import type { PrinterStatusRow, StationScreenOrderRow } from '../core/apiTypes'

export const TAKE_DELAY_SECONDS = 10

export interface Station {
  stationId: string
  name: string
  canPrint: boolean
}

export const useStationStore = defineStore('station', () => {
  const stations = ref<Station[]>([])
  const selectedStationId = ref<string | null>(null)
  const stationOrders = ref<StationScreenOrderRow[]>([])
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
    const result = await request<{ stationOrders: StationScreenOrderRow[] }>(
      `/api/stations/${selectedStationId.value}/station-orders`,
      { token: deviceToken() },
    )
    if (result.kind === 'ok') {
      stationOrders.value = result.data.stationOrders
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

  function isPending(stationOrderId: string): boolean {
    return pendingTicketIds.value.includes(stationOrderId)
  }

  async function sendAcknowledge(stationOrderId: string): Promise<void> {
    const stationOrder = stationOrders.value.find((row) => row.stationOrderId === stationOrderId)
    if (stationOrder === undefined) {
      return
    }
    const result = await request(
      `/api/stations/${selectedStationId.value}/station-orders/${stationOrder.stationOrderId}/hand-on-paper`,
      { method: 'POST', token: deviceToken() },
    )
    if (result.kind === 'error') {
      noticeKeyByTicketId.value = {
        ...noticeKeyByTicketId.value,
        [stationOrderId]: result.body?.messageKey ?? 'station.takeRefused',
      }
    }
    await loadTickets()
  }

  function beginTake(stationOrderId: string): void {
    if (isPending(stationOrderId)) {
      return
    }
    pendingTicketIds.value = [...pendingTicketIds.value, stationOrderId]
    pendingTimers.set(
      stationOrderId,
      setTimeout(() => {
        pendingTimers.delete(stationOrderId)
        pendingTicketIds.value = pendingTicketIds.value.filter((id) => id !== stationOrderId)
        void sendAcknowledge(stationOrderId)
      }, TAKE_DELAY_SECONDS * 1000),
    )
  }

  function undoTake(stationOrderId: string): void {
    const timer = pendingTimers.get(stationOrderId)
    if (timer !== undefined) {
      clearTimeout(timer)
      pendingTimers.delete(stationOrderId)
    }
    pendingTicketIds.value = pendingTicketIds.value.filter((id) => id !== stationOrderId)
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
    stationOrders,
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
