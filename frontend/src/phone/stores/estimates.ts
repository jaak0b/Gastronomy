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
import { createLatestRequestGate, type LatestRequestGate } from '../../shared/core/latestRequestGate'
import { useConnectionStore } from '../../shared/stores/connection'
import { useSessionStore } from '../../shared/stores/session'

export const useEstimatesStore = defineStore('estimates', () => {
  const items = ref<ItemEstimateView[]>([])
  const quotedStations = ref<StationQuoteView[]>([])
  const linesBeingQuoted = ref<EstimateQuoteLine[] | null>(null)

  const stationChoiceQuotes = ref<Record<string, StationQuoteView[]>>({})
  const stationChoiceLines = new Map<string, EstimateQuoteLine[]>()
  const stationChoiceGates = new Map<string, LatestRequestGate>()

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

  async function sendQuoteRequest(
    lines: EstimateQuoteLine[],
    gate: LatestRequestGate,
    apply: (stations: StationQuoteView[]) => void,
  ): Promise<void> {
    const session = useSessionStore()
    const token = gate.startRequest()
    if (session.deviceToken === null || lines.length === 0) {
      apply([])
      return
    }
    const result = await request('/api/estimates/quote', {
      method: 'POST',
      body: { lines },
      token: session.deviceToken,
      schema: EstimateQuoteView,
    })
    if (!gate.isNewestRequest(token)) {
      return
    }
    if (result.kind !== 'ok') {
      console.error('The waiting times for the order could not be calculated.', result)
      apply([])
      return
    }
    apply(result.data.stations)
  }

  function quoteReviewOrder(lines: EstimateQuoteLine[]): Promise<void> {
    return sendQuoteRequest(lines, quoteGate, (stations) => {
      quotedStations.value = stations
    })
  }

  function gateForStationChoice(stationChoice: string): LatestRequestGate {
    const existing = stationChoiceGates.get(stationChoice)
    if (existing !== undefined) {
      return existing
    }
    const created = createLatestRequestGate()
    stationChoiceGates.set(stationChoice, created)
    return created
  }

  function requoteStationChoice(stationChoice: string, lines: EstimateQuoteLine[]): Promise<void> {
    return sendQuoteRequest(lines, gateForStationChoice(stationChoice), (stations) => {
      stationChoiceQuotes.value = { ...stationChoiceQuotes.value, [stationChoice]: stations }
    })
  }

  async function quoteStationChoice(stationChoice: string, lines: EstimateQuoteLine[]): Promise<void> {
    stationChoiceLines.set(stationChoice, lines)
    stationChoiceQuotes.value = Object.fromEntries(
      Object.entries(stationChoiceQuotes.value).filter(([quoted]) => quoted !== stationChoice),
    )
    await requoteStationChoice(stationChoice, lines)
  }

  function stopQuotingStationChoices(): void {
    stationChoiceLines.clear()
    stationChoiceGates.forEach((gate) => gate.startRequest())
    stationChoiceQuotes.value = {}
  }

  async function quote(lines: EstimateQuoteLine[]): Promise<void> {
    linesBeingQuoted.value = lines
    await quoteReviewOrder(lines)
  }

  function stopQuoting(): void {
    linesBeingQuoted.value = null
    quoteGate.startRequest()
    quotedStations.value = []
  }

  async function refresh(): Promise<void> {
    const lines = linesBeingQuoted.value
    await Promise.all([
      load(),
      lines === null ? Promise.resolve() : quoteReviewOrder(lines),
      ...[...stationChoiceLines].map(([stationChoice, linesOfTheStationChoice]) =>
        requoteStationChoice(stationChoice, linesOfTheStationChoice),
      ),
    ])
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

  return {
    items,
    quotedStations,
    stationChoiceQuotes,
    load,
    quote,
    stopQuoting,
    quoteStationChoice,
    stopQuotingStationChoices,
    listen,
  }
})
