import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { request } from '../api/client'
import type { PrinterStatusRow } from '../core/apiTypes'
import { useConnectionStore } from './connection'
import { useSessionStore } from './session'

export interface StationBanner {
  key: string
  name: string
  waitingCount: number | null
}

export const usePrinterStatusStore = defineStore('printerStatus', () => {
  const locations = ref<PrinterStatusRow[]>([])
  const waitingCounts = ref<Record<string, number>>({})

  function bannerKeyFor(row: PrinterStatusRow): string | null {
    if (row.isFaulty) {
      return 'header.stationFaulty'
    }
    if (!row.isOnline) {
      return 'header.stationOffline'
    }
    if (row.isPaperEnd) {
      return 'header.stationPaperOut'
    }
    return null
  }

  const banners = computed<StationBanner[]>(() => {
    const shown: StationBanner[] = []
    for (const row of locations.value) {
      const key = bannerKeyFor(row)
      if (key === null) {
        continue
      }
      const waiting = waitingCounts.value[row.locationId] ?? 0
      shown.push({ key, name: row.name, waitingCount: waiting > 0 ? waiting : null })
    }
    return shown
  })

  const catalogWarnings = computed<StationBanner[]>(() => {
    const shown: StationBanner[] = []
    for (const row of locations.value) {
      if (row.isFaulty) {
        shown.push({ key: 'header.stationFaulty', name: row.name, waitingCount: null })
        continue
      }
      if (!row.isOnline) {
        shown.push({ key: 'catalog.offlineWarning', name: row.name, waitingCount: null })
        continue
      }
      if (row.isPaperEnd) {
        shown.push({ key: 'catalog.paperWarning', name: row.name, waitingCount: null })
      }
    }
    return shown
  })

  async function load(): Promise<void> {
    const session = useSessionStore()
    if (session.deviceToken === null) {
      return
    }
    const result = await request<{ locations: PrinterStatusRow[] }>('/api/printers/status', {
      token: session.deviceToken,
    })
    if (result.kind === 'ok') {
      locations.value = result.data.locations
    }
  }

  function listen(): void {
    const connection = useConnectionStore()
    connection.registerRefetch(load)
    connection.onEvent<
      Omit<PrinterStatusRow, 'name' | 'lastChangedAtUtc'> & {
        locationName: string
        waitingTicketCount: number
        lastChangedAtUtc?: string
      }
    >('PrinterStatusChanged', (payload) => {
        const existing = locations.value.findIndex(
          (row) => row.locationId === payload.locationId,
        )
        const row: PrinterStatusRow = {
          locationId: payload.locationId,
          name: payload.locationName,
          isOnline: payload.isOnline,
          isPaperEnd: payload.isPaperEnd,
          isPaperNearEnd: payload.isPaperNearEnd,
          isCoverOpen: payload.isCoverOpen,
          isFaulty: payload.isFaulty,
          lastChangedAtUtc:
            payload.lastChangedAtUtc ?? locations.value[existing]?.lastChangedAtUtc ?? '',
        }
        if (existing === -1) {
          locations.value = [...locations.value, row]
        } else {
          locations.value = locations.value.map((current, index) =>
            index === existing ? row : current,
          )
        }
        waitingCounts.value = {
          ...waitingCounts.value,
          [payload.locationId]: payload.waitingTicketCount,
        }
    })
  }

  return { locations, waitingCounts, banners, catalogWarnings, load, listen }
})
