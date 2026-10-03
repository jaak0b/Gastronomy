import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { fireHubEvent, forgetHubEvents } from '../../support/hubConnection'
import { useEstimatesStore } from '../../../src/phone/stores/estimates'
import { useSessionStore } from '../../../src/shared/stores/session'
import { useConnectionStore } from '../../../src/shared/stores/connection'
import {
  aHold,
  answer,
  heldUntil,
  inTurn,
  noConnection,
  stubLaptop,
  stubLaptopAnswering,
} from '../../support/laptop'

vi.mock('@microsoft/signalr', async () => (await import('../../support/hubConnection')).signalrModuleFake())

const BRATWURST_IN_THE_KITCHEN = {
  catalogItemId: 'item-bratwurst',
  stationId: 'station-kueche',
  readyInMinutes: 48,
}

const TWO_BRATWURST = [{ catalogItemId: 'item-bratwurst', stationId: 'station-kueche', units: 2 }]

const KITCHEN_IN_96_MINUTES = { stations: [{ stationId: 'station-kueche', readyInMinutes: 96 }] }

function emptyAnswerFor(url: string): unknown {
  return url === '/api/estimates/quote' ? { stations: [] } : []
}

function laptopWithNoWaitingTimes() {
  return stubLaptopAnswering(emptyAnswerFor)
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
    stubLaptop().answersEverythingElse(answer([BRATWURST_IN_THE_KITCHEN]))
    enrolledPhone()
    const estimates = useEstimatesStore()

    await estimates.load()

    expect(estimates.items).toEqual([BRATWURST_IN_THE_KITCHEN])
  })

  it('asks the laptop at its own address', async () => {
    const laptop = laptopWithNoWaitingTimes()
    enrolledPhone()
    const estimates = useEstimatesStore()

    await estimates.load()

    expect(laptop.urls()).toEqual(['/api/estimates'])
  })

  it('drops the old times and reports it to the console when the laptop cannot answer', async () => {
    stubLaptop().answersEverythingElse(inTurn(answer([BRATWURST_IN_THE_KITCHEN]), noConnection()))
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
    const laptop = stubLaptop()
    const estimates = useEstimatesStore()

    await estimates.load()

    expect(laptop.calls).toEqual([])
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

  async function listeningPhone() {
    const laptop = laptopWithNoWaitingTimes()
    useEstimatesStore().listen()
    await useConnectionStore().connect({ deviceToken: 'token-here' })
    laptop.calls.length = 0
    return laptop
  }

  it('loads again when a station worked off part of its queue', async () => {
    const laptop = await listeningPhone()

    fireHubEvent('OrdersChanged')
    await flushPromises()

    expect(laptop.urls()).toEqual(['/api/estimates'])
  })

  it('loads again when the menu changes', async () => {
    const laptop = await listeningPhone()

    fireHubEvent('ConfigurationChanged')
    await flushPromises()

    expect(laptop.urls()).toEqual(['/api/estimates'])
  })

  it('loads again when the connection comes back', async () => {
    const laptop = await listeningPhone()

    await useConnectionStore().refetchAll()

    expect(laptop.urls()).toEqual(['/api/estimates'])
  })

  it('is left alone once the phone has stopped listening', async () => {
    const laptop = laptopWithNoWaitingTimes()
    const stopListening = useEstimatesStore().listen()
    await useConnectionStore().connect({ deviceToken: 'token-here' })
    stopListening()
    laptop.calls.length = 0

    fireHubEvent('ConfigurationChanged')
    await flushPromises()

    expect(laptop.urls()).toEqual([])
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
    const laptop = stubLaptop().answersEverythingElse(answer(KITCHEN_IN_96_MINUTES))
    const estimates = useEstimatesStore()

    await estimates.quote(TWO_BRATWURST)

    expect(laptop.writtenBodies()).toEqual([{ lines: TWO_BRATWURST }])
    expect(estimates.quotedStations).toEqual([{ stationId: 'station-kueche', readyInMinutes: 96 }])
  })

  it('drops the times and reports it to the console when the laptop cannot answer', async () => {
    stubLaptop().answersEverythingElse(inTurn(answer(KITCHEN_IN_96_MINUTES), noConnection()))
    const estimates = useEstimatesStore()
    await estimates.quote(TWO_BRATWURST)
    const logged = vi.spyOn(console, 'error').mockImplementation(() => {})

    await estimates.quote(TWO_BRATWURST)

    expect(estimates.quotedStations).toEqual([])
    expect(logged).toHaveBeenCalledOnce()
    logged.mockRestore()
  })

  it('keeps the newer answer when an older one arrives late', async () => {
    const olderAnswer = aHold()
    const newerAnswer = aHold()
    stubLaptop().answersEverythingElse(
      inTurn(
        heldUntil(olderAnswer.released, answer(KITCHEN_IN_96_MINUTES)),
        heldUntil(
          newerAnswer.released,
          answer({ stations: [{ stationId: 'station-kueche', readyInMinutes: 144 }] }),
        ),
      ),
    )
    const estimates = useEstimatesStore()
    const older = estimates.quote(TWO_BRATWURST)
    const newer = estimates.quote([{ ...TWO_BRATWURST[0], units: 3 }])

    newerAnswer.release()
    await newer
    olderAnswer.release()
    await older

    expect(estimates.quotedStations).toEqual([{ stationId: 'station-kueche', readyInMinutes: 144 }])
  })

  it('asks nothing for an order without lines', async () => {
    const laptop = stubLaptop()

    await useEstimatesStore().quote([])

    expect(laptop.calls).toEqual([])
  })

  it('asks again when a station worked off part of its queue', async () => {
    const laptop = laptopWithNoWaitingTimes()
    const estimates = useEstimatesStore()
    estimates.listen()
    await useConnectionStore().connect({ deviceToken: 'token-here' })
    await estimates.quote(TWO_BRATWURST)
    laptop.calls.length = 0

    fireHubEvent('OrdersChanged')
    await flushPromises()

    expect(laptop.urls()).toEqual(['/api/estimates', '/api/estimates/quote'])
  })

  it('stops asking once the review screen has closed', async () => {
    const laptop = laptopWithNoWaitingTimes()
    const estimates = useEstimatesStore()
    estimates.listen()
    await useConnectionStore().connect({ deviceToken: 'token-here' })
    await estimates.quote(TWO_BRATWURST)
    estimates.stopQuoting()
    laptop.calls.length = 0

    fireHubEvent('OrdersChanged')
    await flushPromises()

    expect(laptop.urls()).toEqual(['/api/estimates'])
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

  function answerNaming(readyInMinutes: number) {
    return answer({ stations: [{ stationId: 'station-bar', readyInMinutes }] })
  }

  it('drops an answer that arrives after the station question has closed', async () => {
    const lateAnswer = aHold()
    stubLaptop().answersEverythingElse(heldUntil(lateAnswer.released, answerNaming(12)))
    const estimates = useEstimatesStore()
    const pending = estimates.quoteStationChoice('station-bar', KAFFEE_AT_THE_BAR)

    estimates.stopQuotingStationChoices()
    lateAnswer.release()
    await pending

    expect(estimates.stationChoiceQuotes).toEqual({})
  })

  it('keeps the newer answer for a button when an older one for the same button arrives late', async () => {
    const olderAnswer = aHold()
    const newerAnswer = aHold()
    stubLaptop().answersEverythingElse(
      inTurn(
        heldUntil(olderAnswer.released, answerNaming(12)),
        heldUntil(newerAnswer.released, answerNaming(24)),
      ),
    )
    const estimates = useEstimatesStore()
    const older = estimates.quoteStationChoice('station-bar', KAFFEE_AT_THE_BAR)
    const newer = estimates.quoteStationChoice('station-bar', [{ ...KAFFEE_AT_THE_BAR[0], units: 2 }])

    olderAnswer.release()
    await older
    newerAnswer.release()
    await newer

    expect(estimates.stationChoiceQuotes).toEqual({
      'station-bar': [{ stationId: 'station-bar', readyInMinutes: 24 }],
    })
  })

  it('keeps the answer to the refreshed question when the first answer arrives late', async () => {
    const firstAnswer = aHold()
    const refreshedAnswer = aHold()
    stubLaptop()
      .answers('GET', '/api/estimates', answer([]))
      .answers(
        'POST',
        '/api/estimates/quote',
        inTurn(
          heldUntil(firstAnswer.released, answerNaming(12)),
          heldUntil(refreshedAnswer.released, answerNaming(30)),
        ),
      )
    const estimates = useEstimatesStore()
    estimates.listen()
    await useConnectionStore().connect({ deviceToken: 'token-here' })
    const first = estimates.quoteStationChoice('station-bar', KAFFEE_AT_THE_BAR)

    fireHubEvent('OrdersChanged')
    await flushPromises()
    refreshedAnswer.release()
    await flushPromises()
    firstAnswer.release()
    await first

    expect(estimates.stationChoiceQuotes).toEqual({
      'station-bar': [{ stationId: 'station-bar', readyInMinutes: 30 }],
    })
  })
})
