import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { request } from '../api/client'
import type { PrinterStatusRow } from '../core/apiTypes'
import { useConnectionStore } from './connection'
import { useSessionStore } from './session'

export interface StationBanner {
  stationId: string
  key: string
  name: string
  waitingCount: number | null
}

export const usePrinterStatusStore = defineStore('printerStatus', () => {
  const stations = ref<PrinterStatusRow[]>([])
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
    if (row.isCoverOpen) {
      return 'header.stationCoverOpen'
    }
    return null
  }

  const banners = computed<StationBanner[]>(() => {
    const shown: StationBanner[] = []
    for (const row of stations.value) {
      const key = bannerKeyFor(row)
      if (key === null) {
        continue
      }
      const waiting = waitingCounts.value[row.stationId] ?? 0
      shown.push({
        stationId: row.stationId,
        key,
        name: row.name,
        waitingCount: waiting > 0 ? waiting : null,
      })
    }
    return shown
  })

  async function load(): Promise<void> {
    const session = useSessionStore()
    if (session.deviceToken === null) {
      return
    }
    const result = await request<{ stations: PrinterStatusRow[] }>('/api/printers/status', {
      token: session.deviceToken,
    })
    if (result.kind === 'ok') {
      stations.value = result.data.stations
    }
  }

  function listen(): void {
    const connection = useConnectionStore()
    connection.registerRefetch(load)
    connection.onEvent<
      Omit<PrinterStatusRow, 'name' | 'lastChangedAtUtc'> & {
        stationName: string
        waitingTicketCount: number
        lastChangedAtUtc?: string
      }
    >('PrinterStatusChanged', (payload) => {
        const existing = stations.value.findIndex(
          (row) => row.stationId === payload.stationId,
        )
        const row: PrinterStatusRow = {
          stationId: payload.stationId,
          name: payload.stationName,
          isOnline: payload.isOnline,
          isPaperEnd: payload.isPaperEnd,
          isPaperNearEnd: payload.isPaperNearEnd,
          isCoverOpen: payload.isCoverOpen,
          isFaulty: payload.isFaulty,
          lastChangedAtUtc:
            payload.lastChangedAtUtc ?? stations.value[existing]?.lastChangedAtUtc ?? '',
        }
        if (existing === -1) {
          stations.value = [...stations.value, row]
        } else {
          stations.value = stations.value.map((current, index) =>
            index === existing ? row : current,
          )
        }
        waitingCounts.value = {
          ...waitingCounts.value,
          [payload.stationId]: payload.waitingTicketCount,
        }
    })
  }

  return { stations, waitingCounts, banners, load, listen }
})
