import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { fireHubEvent, forgetHubEvents } from '../../support/hubConnection'
import { useEstimatesStore } from '../../../src/phone/stores/estimates'
import { useSessionStore } from '../../../src/shared/stores/session'
import { useConnectionStore } from '../../../src/shared/stores/connection'

vi.mock('@microsoft/signalr', async () => (await import('../../support/hubConnection')).signalrModuleFake())

const BRATWURST_IN_THE_KITCHEN = {
  catalogItemId: 'item-bratwurst',
  stationId: 'station-kueche',
  readyInMinutes: 48,
}

const TWO_BRATWURST = [{ catalogItemId: 'item-bratwurst', stationId: 'station-kueche', units: 2 }]

function emptyAnswerFor(url: string): unknown {
  return url === '/api/estimates/quote' ? { stations: [] } : []
}

function enrolledPhone() {
  useSessionStore().deviceToken = 'token-here'
}

describe('the waiting times the phone asks the laptop for', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('keeps the time of every article and station the laptop named', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(
        async () =>
          new Response(
            JSON.stringify([BRATWURST_IN_THE_KITCHEN]),
            { status: 200 },
          ),
      ),
    )
    enrolledPhone()
    const estimates = useEstimatesStore()

    await estimates.load()

    expect(estimates.items).toEqual([BRATWURST_IN_THE_KITCHEN])
  })

  it('asks the laptop at its own address', async () => {
    const urls: string[] = []
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string) => {
        urls.push(url)
        return new Response(JSON.stringify(emptyAnswerFor(url)), { status: 200 })
      }),
    )
    enrolledPhone()
    const estimates = useEstimatesStore()

    await estimates.load()

    expect(urls).toEqual(['/api/estimates'])
  })

  it('drops the old times and reports it to the console when the laptop cannot answer', async () => {
    let calls = 0
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => {
        calls += 1
        if (calls === 1) {
          return new Response(JSON.stringify([BRATWURST_IN_THE_KITCHEN]), { status: 200 })
        }
        throw new TypeError('Failed to fetch')
      }),
    )
    enrolledPhone()
    const estimates = useEstimatesStore()
    await estimates.load()
    const logged = vi.spyOn(console, 'error').mockImplementation(() => {})

    await estimates.load()

    expect(estimates.items).toEqual([])
    expect(logged).toHaveBeenCalledOnce()
    logged.mockRestore()
  })

  it('asks for nothing at all before the phone is set up', async () => {
    const fetched = vi.fn()
    vi.stubGlobal('fetch', fetched)
    const estimates = useEstimatesStore()

    await estimates.load()

    expect(fetched).not.toHaveBeenCalled()
  })
})

describe('the waiting times a phone follows while it takes orders', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    forgetHubEvents()
    useSessionStore().deviceToken = 'token-here'
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  async function listeningPhone(): Promise<string[]> {
    const urls: string[] = []
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string) => {
        urls.push(url)
        return new Response(JSON.stringify(emptyAnswerFor(url)), { status: 200 })
      }),
    )
    useEstimatesStore().listen()
    await useConnectionStore().connect({ deviceToken: 'token-here' })
    urls.length = 0
    return urls
  }

  async function letTheReloadFinish(): Promise<void> {
    await new Promise((resolve) => setTimeout(resolve, 0))
  }

  it('loads again when a station worked off part of its queue', async () => {
    const urls = await listeningPhone()

    fireHubEvent('OrdersChanged')
    await letTheReloadFinish()

    expect(urls).toEqual(['/api/estimates'])
  })

  it('loads again when the menu changes', async () => {
    const urls = await listeningPhone()

    fireHubEvent('ConfigurationChanged')
    await letTheReloadFinish()

    expect(urls).toEqual(['/api/estimates'])
  })

  it('loads again when the connection comes back', async () => {
    const urls = await listeningPhone()

    await useConnectionStore().refetchAll()

    expect(urls).toEqual(['/api/estimates'])
  })

  it('is left alone once the phone has stopped listening', async () => {
    const urls: string[] = []
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string) => {
        urls.push(url)
        return new Response(JSON.stringify(emptyAnswerFor(url)), { status: 200 })
      }),
    )
    const stopListening = useEstimatesStore().listen()
    await useConnectionStore().connect({ deviceToken: 'token-here' })
    stopListening()
    urls.length = 0

    fireHubEvent('ConfigurationChanged')
    await letTheReloadFinish()

    expect(urls).toEqual([])
  })
})

describe('the waiting time the laptop calculates for the order on the screen', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    forgetHubEvents()
    enrolledPhone()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('sends the lines of the order and keeps the time of every station', async () => {
    const bodies: unknown[] = []
    vi.stubGlobal(
      'fetch',
      vi.fn(async (_url: string, init: RequestInit) => {
        bodies.push(JSON.parse(String(init.body)))
        return new Response(
          JSON.stringify({ stations: [{ stationId: 'station-kueche', readyInMinutes: 96 }] }),
          { status: 200 },
        )
      }),
    )
    const estimates = useEstimatesStore()

    await estimates.quote(TWO_BRATWURST)

    expect(bodies).toEqual([{ lines: TWO_BRATWURST }])
    expect(estimates.quotedStations).toEqual([{ stationId: 'station-kueche', readyInMinutes: 96 }])
  })

  it('drops the times and reports it to the console when the laptop cannot answer', async () => {
    let calls = 0
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => {
        calls += 1
        if (calls === 1) {
          return new Response(
            JSON.stringify({ stations: [{ stationId: 'station-kueche', readyInMinutes: 96 }] }),
            { status: 200 },
          )
        }
        throw new TypeError('Failed to fetch')
      }),
    )
    const estimates = useEstimatesStore()
    await estimates.quote(TWO_BRATWURST)
    const logged = vi.spyOn(console, 'error').mockImplementation(() => {})

    await estimates.quote(TWO_BRATWURST)

    expect(estimates.quotedStations).toEqual([])
    expect(logged).toHaveBeenCalledOnce()
    logged.mockRestore()
  })

  it('keeps the newer answer when an older one arrives late', async () => {
    const answers: Array<(response: Response) => void> = []
    vi.stubGlobal(
      'fetch',
      vi.fn(() => new Promise<Response>((resolve) => answers.push(resolve))),
    )
    const estimates = useEstimatesStore()
    const older = estimates.quote(TWO_BRATWURST)
    const newer = estimates.quote([{ ...TWO_BRATWURST[0], units: 3 }])

    answers[1](new Response(JSON.stringify({ stations: [{ stationId: 'station-kueche', readyInMinutes: 144 }] }), { status: 200 }))
    await newer
    answers[0](new Response(JSON.stringify({ stations: [{ stationId: 'station-kueche', readyInMinutes: 96 }] }), { status: 200 }))
    await older

    expect(estimates.quotedStations).toEqual([{ stationId: 'station-kueche', readyInMinutes: 144 }])
  })

  it('asks nothing for an order without lines', async () => {
    const fetched = vi.fn()
    vi.stubGlobal('fetch', fetched)

    await useEstimatesStore().quote([])

    expect(fetched).not.toHaveBeenCalled()
  })

  it('asks again when a station worked off part of its queue', async () => {
    const urls: string[] = []
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string) => {
        urls.push(url)
        return new Response(JSON.stringify(emptyAnswerFor(url)), { status: 200 })
      }),
    )
    const estimates = useEstimatesStore()
    estimates.listen()
    await useConnectionStore().connect({ deviceToken: 'token-here' })
    await estimates.quote(TWO_BRATWURST)
    urls.length = 0

    fireHubEvent('OrdersChanged')
    await new Promise((resolve) => setTimeout(resolve, 0))

    expect(urls).toEqual(['/api/estimates', '/api/estimates/quote'])
  })

  it('stops asking once the review screen has closed', async () => {
    const urls: string[] = []
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string) => {
        urls.push(url)
        return new Response(JSON.stringify(emptyAnswerFor(url)), { status: 200 })
      }),
    )
    const estimates = useEstimatesStore()
    estimates.listen()
    await useConnectionStore().connect({ deviceToken: 'token-here' })
    await estimates.quote(TWO_BRATWURST)
    estimates.stopQuoting()
    urls.length = 0

    fireHubEvent('OrdersChanged')
    await new Promise((resolve) => setTimeout(resolve, 0))

    expect(urls).toEqual(['/api/estimates'])
  })
})

describe('the waiting times the laptop calculates for each button of the station question', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    forgetHubEvents()
    enrolledPhone()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  const KAFFEE_AT_THE_BAR = [{ catalogItemId: 'item-kaffee', stationId: 'station-bar', units: 1 }]

  function answerNaming(readyInMinutes: number): Response {
    return new Response(JSON.stringify({ stations: [{ stationId: 'station-bar', readyInMinutes }] }), { status: 200 })
  }

  it('drops an answer that arrives after the station question has closed', async () => {
    const answers: Array<(response: Response) => void> = []
    vi.stubGlobal(
      'fetch',
      vi.fn(() => new Promise<Response>((resolve) => answers.push(resolve))),
    )
    const estimates = useEstimatesStore()
    const pending = estimates.quoteStationChoice('station-bar', KAFFEE_AT_THE_BAR)

    estimates.stopQuotingStationChoices()
    answers[0](answerNaming(12))
    await pending

    expect(estimates.stationChoiceQuotes).toEqual({})
  })

  it('keeps the newer answer for a button when an older one for the same button arrives late', async () => {
    const answers: Array<(response: Response) => void> = []
    vi.stubGlobal(
      'fetch',
      vi.fn(() => new Promise<Response>((resolve) => answers.push(resolve))),
    )
    const estimates = useEstimatesStore()
    const older = estimates.quoteStationChoice('station-bar', KAFFEE_AT_THE_BAR)
    const newer = estimates.quoteStationChoice('station-bar', [{ ...KAFFEE_AT_THE_BAR[0], units: 2 }])

    answers[0](answerNaming(12))
    await older
    answers[1](answerNaming(24))
    await newer

    expect(estimates.stationChoiceQuotes).toEqual({
      'station-bar': [{ stationId: 'station-bar', readyInMinutes: 24 }],
    })
  })

  it('keeps the answer to the refreshed question when the first answer arrives late', async () => {
    const answers: Array<{ url: string; resolve: (response: Response) => void }> = []
    vi.stubGlobal(
      'fetch',
      vi.fn(
        (url: string) =>
          new Promise<Response>((resolve) => {
            if (url === '/api/estimates') {
              resolve(new Response(JSON.stringify([]), { status: 200 }))
              return
            }
            answers.push({ url, resolve })
          }),
      ),
    )
    const estimates = useEstimatesStore()
    estimates.listen()
    await useConnectionStore().connect({ deviceToken: 'token-here' })
    const first = estimates.quoteStationChoice('station-bar', KAFFEE_AT_THE_BAR)

    fireHubEvent('OrdersChanged')
    await new Promise((resolve) => setTimeout(resolve, 0))
    answers[1].resolve(answerNaming(30))
    await new Promise((resolve) => setTimeout(resolve, 0))
    answers[0].resolve(answerNaming(12))
    await first

    expect(estimates.stationChoiceQuotes).toEqual({
      'station-bar': [{ stationId: 'station-bar', readyInMinutes: 30 }],
    })
  })
})
