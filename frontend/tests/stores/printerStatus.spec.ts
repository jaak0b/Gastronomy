import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { usePrinterStatusStore } from '../../src/stores/printerStatus'
import { useConnectionStore } from '../../src/stores/connection'
import { useSessionStore } from '../../src/stores/session'

const KITCHEN_ID = '11111111-1111-1111-1111-111111111111'
const OUTDOOR_BAR_ID = '22222222-2222-2222-2222-222222222222'

const REST_STATUS = {
  stations: [
    {
      stationId: KITCHEN_ID,
      name: 'Küche',
      isOnline: true,
      isPaperEnd: false,
      isPaperNearEnd: false,
      isCoverOpen: false,
      isFaulty: false,
      lastChangedAtUtc: '2026-08-27T18:00:00Z',
    },
  ],
}

const PUSHED_STATUS = {
  stationId: KITCHEN_ID,
  stationName: 'Küche',
  isOnline: true,
  isPaperEnd: true,
  isPaperNearEnd: false,
  isCoverOpen: false,
  isFaulty: false,
  waitingTicketCount: 2,
  lastChangedAtUtc: '2026-08-27T19:30:00Z',
  lastDetail: 'Papier ist leer.',
}

describe('a printer status that arrives over the live connection', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => new Response(JSON.stringify(REST_STATUS), { status: 200 })),
    )
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  async function pushStatuses(...payloads: Record<string, unknown>[]) {
    const session = useSessionStore()
    session.deviceToken = 'a-token'
    const printerStatus = usePrinterStatusStore()
    const connection = useConnectionStore()
    const handlers = new Map<string, (payload: unknown) => void>()
    vi.spyOn(connection, 'onEvent').mockImplementation(((
      eventName: string,
      handler: (payload: unknown) => void,
    ) => {
      handlers.set(eventName, handler)
      return () => undefined
    }) as typeof connection.onEvent)

    await printerStatus.load()
    printerStatus.listen()
    for (const payload of payloads) {
      handlers.get('PrinterStatusChanged')?.(payload)
    }

    return printerStatus
  }

  async function pushOneStatus(payload: Record<string, unknown> = PUSHED_STATUS) {
    return pushStatuses(payload)
  }

  it('takes the time the push carried', async () => {
    const printerStatus = await pushOneStatus()

    expect(printerStatus.stations[0].lastChangedAtUtc).toBe('2026-08-27T19:30:00Z')
  })

  it('keeps the time it already knew when a push carries none, rather than forgetting it', async () => {
    const withoutTime = { ...PUSHED_STATUS }
    delete (withoutTime as Record<string, unknown>).lastChangedAtUtc

    const printerStatus = await pushOneStatus(withoutTime)

    expect(printerStatus.stations[0].lastChangedAtUtc).toBe('2026-08-27T18:00:00Z')
  })

  it('still applies the state the push carried', async () => {
    const printerStatus = await pushOneStatus()

    expect(printerStatus.stations[0].isPaperEnd).toBe(true)
  })

  it('derives one message about the station, carrying the slips waiting there', async () => {
    const printerStatus = await pushOneStatus()

    expect(printerStatus.banners).toEqual([
      { stationId: KITCHEN_ID, key: 'header.stationPaperOut', name: 'Küche', waitingCount: 2 },
    ])
  })

  it('derives one message about a station that stopped answering', async () => {
    const printerStatus = await pushOneStatus({
      ...PUSHED_STATUS,
      isOnline: false,
      isPaperEnd: false,
      waitingTicketCount: 0,
    })

    expect(printerStatus.banners).toEqual([
      { stationId: KITCHEN_ID, key: 'header.stationOffline', name: 'Küche', waitingCount: null },
    ])
  })

  it('derives one message about a station whose printer cover stands open', async () => {
    const printerStatus = await pushOneStatus({
      ...PUSHED_STATUS,
      isPaperEnd: false,
      isCoverOpen: true,
      waitingTicketCount: 0,
    })

    expect(printerStatus.banners).toEqual([
      { stationId: KITCHEN_ID, key: 'header.stationCoverOpen', name: 'Küche', waitingCount: null },
    ])
  })

  it('says nothing while the paper is only running low, because the station keeps printing', async () => {
    const printerStatus = await pushOneStatus({
      ...PUSHED_STATUS,
      isPaperEnd: false,
      isPaperNearEnd: true,
      waitingTicketCount: 0,
    })

    expect(printerStatus.banners).toEqual([])
  })

  it('derives one message about a station whose printer is broken', async () => {
    const printerStatus = await pushOneStatus({
      ...PUSHED_STATUS,
      isPaperEnd: false,
      isFaulty: true,
      waitingTicketCount: 0,
    })

    expect(printerStatus.banners).toEqual([
      { stationId: KITCHEN_ID, key: 'header.stationFaulty', name: 'Küche', waitingCount: null },
    ])
  })

  it('names the station on every message, so two stations with the same fault stay apart', async () => {
    const printerStatus = await pushStatuses(PUSHED_STATUS, {
      ...PUSHED_STATUS,
      stationId: OUTDOOR_BAR_ID,
      stationName: 'Bar außen',
      waitingTicketCount: 0,
    })

    expect(printerStatus.banners).toEqual([
      { stationId: KITCHEN_ID, key: 'header.stationPaperOut', name: 'Küche', waitingCount: 2 },
      {
        stationId: OUTDOOR_BAR_ID,
        key: 'header.stationPaperOut',
        name: 'Bar außen',
        waitingCount: null,
      },
    ])
  })
})
