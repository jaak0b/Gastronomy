import { defineStore } from 'pinia'
import { ref } from 'vue'
import { z } from 'zod'
import { request } from '../../shared/api/client'
import {
  EstimateQuoteLine,
  EstimateQuoteView,
  ItemEstimateView,
  StationQuoteView,
} from '../../shared/api/generatedSchemas'
import { createLatestRequestGate } from '../../shared/core/latestRequestGate'
import { useConnectionStore } from '../../shared/stores/connection'
import { useSessionStore } from '../../shared/stores/session'

export const useEstimatesStore = defineStore('estimates', () => {
  const items = ref<ItemEstimateView[]>([])
  const quotedStations = ref<StationQuoteView[]>([])
  const linesBeingQuoted = ref<EstimateQuoteLine[] | null>(null)

  const loadGate = createLatestRequestGate()
  const quoteGate = createLatestRequestGate()

  async function load(): Promise<void> {
    const session = useSessionStore()
    if (session.deviceToken === null) {
      return
    }
    const token = loadGate.startRequest()
    const result = await request('/api/estimates', {
      token: session.deviceToken,
      schema: z.array(ItemEstimateView),
    })
    if (!loadGate.isNewestRequest(token)) {
      return
    }
    if (result.kind !== 'ok') {
      console.error('The waiting times could not be loaded.', result)
      items.value = []
      return
    }
    items.value = result.data
  }

  async function askForTheQuote(lines: EstimateQuoteLine[]): Promise<void> {
    const session = useSessionStore()
    const token = quoteGate.startRequest()
    if (session.deviceToken === null || lines.length === 0) {
      quotedStations.value = []
      return
    }
    const result = await request('/api/estimates/quote', {
      method: 'POST',
      body: { lines },
      token: session.deviceToken,
      schema: EstimateQuoteView,
    })
    if (!quoteGate.isNewestRequest(token)) {
      return
    }
    if (result.kind !== 'ok') {
      console.error('The waiting times for the order could not be calculated.', result)
      quotedStations.value = []
      return
    }
    quotedStations.value = result.data.stations
  }

  async function quote(lines: EstimateQuoteLine[]): Promise<void> {
    linesBeingQuoted.value = lines
    await askForTheQuote(lines)
  }

  function stopQuoting(): void {
    linesBeingQuoted.value = null
    quoteGate.startRequest()
    quotedStations.value = []
  }

  async function refresh(): Promise<void> {
    const lines = linesBeingQuoted.value
    await Promise.all([load(), lines === null ? Promise.resolve() : askForTheQuote(lines)])
  }

  function listen(): () => void {
    const connection = useConnectionStore()
    const releases = [
      connection.registerRefetch(refresh),
      ...['StationOrdersChanged', 'OrderStatusChanged', 'CatalogChanged', 'StationsChanged', 'FestivalChanged'].map(
        (eventName) =>
          connection.onEvent<unknown>(eventName, () => {
            void refresh()
          }),
      ),
    ]
    return () => {
      for (const release of releases) {
        release()
      }
    }
  }

  return { items, quotedStations, load, quote, stopQuoting, listen }
})
