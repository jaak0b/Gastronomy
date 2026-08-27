import { defineStore } from 'pinia'
import { ref } from 'vue'
import { request } from '../api/client'
import type { AppLanguage, PrinterStatusRow, StationTicketRow } from '../core/apiTypes'

export const STATION_LANGUAGE_STORAGE_KEY = 'stationLanguage'
export const TAKE_DELAY_SECONDS = 10

export interface StationLocation {
  locationId: string
  name: string
  canPrint: boolean
}

export const useStationStore = defineStore('station', () => {
  const accessKey = ref<string | null>(null)
  const locations = ref<StationLocation[]>([])
  const selectedLocationId = ref<string | null>(null)
  const tickets = ref<StationTicketRow[]>([])
  const printer = ref<PrinterStatusRow | null>(null)
  const keyIsUnknown = ref(false)
  const pendingTicketIds = ref<string[]>([])
  const noticeKeyByTicketId = ref<Record<string, string>>({})
  const language = ref<AppLanguage>(
    localStorage.getItem(STATION_LANGUAGE_STORAGE_KEY) === 'en' ? 'en' : 'de',
  )

  const pendingTimers = new Map<string, ReturnType<typeof setTimeout>>()

  function setLanguage(next: AppLanguage): void {
    language.value = next
    localStorage.setItem(STATION_LANGUAGE_STORAGE_KEY, next)
  }

  async function loadLocations(): Promise<void> {
    const result = await request<{ locations: StationLocation[] }>(
      `/api/station/${accessKey.value}/locations`,
    )
    if (result.kind === 'error' && result.status === 404) {
      keyIsUnknown.value = true
      return
    }
    if (result.kind === 'ok') {
      locations.value = result.data.locations
      if (selectedLocationId.value === null && result.data.locations.length > 0) {
        selectedLocationId.value = result.data.locations[0].locationId
      }
    }
  }

  async function loadTickets(): Promise<void> {
    if (accessKey.value === null || selectedLocationId.value === null) {
      return
    }
    const result = await request<{ tickets: StationTicketRow[] }>(
      `/api/station/${accessKey.value}/tickets?locationId=${selectedLocationId.value}`,
    )
    if (result.kind === 'ok') {
      tickets.value = result.data.tickets
    }
  }

  async function loadPrinter(): Promise<void> {
    if (accessKey.value === null) {
      return
    }
    const result = await request<PrinterStatusRow>(`/api/station/${accessKey.value}/status`)
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
      `/api/station/${accessKey.value}/tickets/${ticket.ticketId}/acknowledge`,
      { method: 'POST' },
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

  async function open(key: string): Promise<void> {
    accessKey.value = key
    await loadLocations()
    await loadTickets()
    await loadPrinter()
  }

  async function selectLocation(locationId: string): Promise<void> {
    selectedLocationId.value = locationId
    await loadTickets()
  }

  return {
    accessKey,
    locations,
    selectedLocationId,
    tickets,
    printer,
    keyIsUnknown,
    pendingTicketIds,
    noticeKeyByTicketId,
    language,
    setLanguage,
    open,
    selectLocation,
    loadTickets,
    loadPrinter,
    isPending,
    beginTake,
    undoTake,
  }
})
