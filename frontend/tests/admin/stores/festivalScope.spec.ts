import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useAdminItemsStore } from '../../../src/admin/stores/items'
import { useAdminStationsStore } from '../../../src/admin/stores/stations'
import { useConnectionStore } from '../../../src/shared/stores/connection'
import { aHold, answer, heldUntil, refusal, stubLaptop, type LaptopReply, type StubbedLaptop } from '../../support/laptop'
import { anAdminStation } from '../../support/wireViews'

const FESTIVAL_ID = 'fest-1'

function scopeLaptop(): StubbedLaptop {
  return stubLaptop()
    .answersEverythingElse(answer({}))
    .answers('GET', '/api/admin/stations', answer({ stations: [] }))
    .answers('GET', '/api/admin/items', answer({ items: [] }))
}

describe('the lists the admin reads', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('carry every item of the laptop while no festival is named', async () => {
    const laptop = scopeLaptop()

    await useAdminItemsStore().load()

    expect(laptop.calls.at(-1)?.url).toBe('/api/admin/items')
  })

  it('carry the items of the festival whose page is open', async () => {
    const laptop = scopeLaptop()

    await useAdminItemsStore().loadAtTheFestival(FESTIVAL_ID)

    expect(laptop.calls.at(-1)?.url).toBe('/api/admin/items?festivalId=fest-1')
  })

  it('carry every station of the laptop while no festival is named', async () => {
    const laptop = scopeLaptop()

    await useAdminStationsStore().load()

    expect(laptop.calls.at(-1)?.url).toBe('/api/admin/stations')
  })

  it('carry the stations of the festival whose page is open', async () => {
    const laptop = scopeLaptop()

    await useAdminStationsStore().loadAtTheFestival(FESTIVAL_ID)

    expect(laptop.calls.at(-1)?.url).toBe('/api/admin/stations?festivalId=fest-1')
  })

  it('stay on that festival after the connection asks for everything again', async () => {
    const laptop = scopeLaptop()
    const stations = useAdminStationsStore()
    stations.listen()
    await stations.loadAtTheFestival(FESTIVAL_ID)

    await useConnectionStore().refetchAll()

    expect(laptop.calls.at(-1)?.url).toBe('/api/admin/stations?festivalId=fest-1')
  })
})

describe('an item at a festival', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('is put at the festival in the address, with its price and its stations', async () => {
    const laptop = scopeLaptop()

    await useAdminItemsStore().putAtTheFestival(FESTIVAL_ID, 'item-1', {
      priceCents: 350,
      stationIds: ['station-kueche'],
    })

    const sent = laptop.calls.find((call) => call.method === 'PUT')
    expect(sent?.url).toBe('/api/admin/festivals/fest-1/items/item-1')
    expect(sent?.body).toEqual({ priceCents: 350, stationIds: ['station-kueche'] })
  })

  it('is removed from that festival alone', async () => {
    const laptop = scopeLaptop()

    await useAdminItemsStore().removeFromTheFestival(FESTIVAL_ID, 'item-1')

    expect(laptop.calls.find((call) => call.method === 'DELETE')?.url).toBe(
      '/api/admin/festivals/fest-1/items/item-1',
    )
  })

  it('is sold out at that festival alone', async () => {
    const laptop = scopeLaptop()

    await useAdminItemsStore().setAvailability(FESTIVAL_ID, 'item-1', false)

    const sent = laptop.calls.find((call) => call.method === 'POST')
    expect(sent?.url).toBe('/api/admin/festivals/fest-1/items/item-1/availability')
    expect(sent?.body).toEqual({ isAvailable: false })
  })
})

describe('a station at a festival', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('is added to that festival, without a body', async () => {
    const laptop = scopeLaptop()

    await useAdminStationsStore().addToTheFestival(FESTIVAL_ID, 'station-kueche')

    const sent = laptop.calls.find((call) => call.method === 'PUT')
    expect(sent?.url).toBe('/api/admin/festivals/fest-1/stations/station-kueche')
    expect(sent?.body).toBeUndefined()
  })

  it('keeps the reason the laptop gave for refusing to remove it', async () => {
    stubLaptop()
      .answersEverythingElse(answer({ stations: [] }))
      .answers(
        'DELETE',
        /./,
        refusal('errors.admin.festivals.stationHasOrdersAtTheFestival', {
          code: 'StationHasOrdersAtTheFestival',
          parameters: { count: '3' },
        }),
      )
    const stations = useAdminStationsStore()

    const result = await stations.removeFromTheFestival(FESTIVAL_ID, 'station-kueche')

    expect(result).toEqual({
      kind: 'failed',
      message: {
        key: 'errors.admin.festivals.stationHasOrdersAtTheFestival',
        parameters: { count: '3' },
        count: 3,
      },
    })
  })

  it('is created and handed back as the station itself, so it can be added straight away', async () => {
    stubLaptop()
      .answersEverythingElse(answer({ stations: [] }))
      .answers(
        'POST',
        /./,
        answer(
          anAdminStation({
            stationId: 'station-new',
            name: 'Theke',
            hasDevice: false,
            isAtFestival: false,
          }),
          201,
        ),
      )

    const created = await useAdminStationsStore().create({ name: 'Theke', sortOrder: 1 })

    expect(created).toEqual({
      kind: 'ok',
      value: {
        stationId: 'station-new',
        name: 'Theke',
        sortOrder: 1,
        isActive: true,
        hasDevice: false,
        isAtFestival: false,
        hasItemsToHandOutAtTheRunningFestival: false,
        strandedItemCountWhenSwitchedOff: null,
        strandedItemCountWhenRemovedFromTheFestival: null,
      },
    })
  })
})

describe('the festival that the lists the admin reads belong to', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('is named for the stations once the answer for that festival has arrived', async () => {
    scopeLaptop()
    const stations = useAdminStationsStore()

    await stations.loadAtTheFestival(FESTIVAL_ID)

    expect(stations.loadedFestivalId).toBe(FESTIVAL_ID)
  })

  it('is named for the items once the answer for that festival has arrived', async () => {
    scopeLaptop()
    const items = useAdminItemsStore()

    await items.loadAtTheFestival(FESTIVAL_ID)

    expect(items.loadedFestivalId).toBe(FESTIVAL_ID)
  })

  it('is no festival for the items once the answer for the global list has arrived', async () => {
    scopeLaptop()
    const items = useAdminItemsStore()
    await items.loadAtTheFestival(FESTIVAL_ID)

    await items.load()

    expect(items.loadedFestivalId).toBeNull()
  })
})

const FESTIVAL_A = 'fest-a'

const FESTIVAL_B = 'fest-b'

const KITCHEN_OF_A = anAdminStation({ stationId: 'station-a', name: 'Küche' })

const BAR_OF_B = anAdminStation({ stationId: 'station-b', name: 'Theke' })

const HEADQUARTERS = anAdminStation({ stationId: 'station-global', name: 'Zentrale', isAtFestival: false })

interface StationAnswers {
  forA?: LaptopReply
  forB?: LaptopReply
  forTheGlobalList?: LaptopReply
}

function stationsLaptop(answers: StationAnswers = {}): StubbedLaptop {
  return stubLaptop()
    .answers(
      'GET',
      (call) => call.url === `/api/admin/stations?festivalId=${FESTIVAL_A}`,
      answers.forA ?? answer({ stations: [KITCHEN_OF_A] }),
    )
    .answers(
      'GET',
      (call) => call.url === `/api/admin/stations?festivalId=${FESTIVAL_B}`,
      answers.forB ?? answer({ stations: [BAR_OF_B] }),
    )
    .answers(
      'GET',
      (call) => call.url === '/api/admin/stations',
      answers.forTheGlobalList ?? answer({ stations: [HEADQUARTERS] }),
    )
}

function whatTheStationsShow(stations: ReturnType<typeof useAdminStationsStore>) {
  return { loadedFestivalId: stations.loadedFestivalId, names: stations.stations.map((station) => station.name) }
}

describe('the stations of one festival while the answers for others overtake each other', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('stay the stations of the festival on screen while the answer for another festival is on its way', async () => {
    const holdB = aHold()
    stationsLaptop({ forB: heldUntil(holdB.released, answer({ stations: [BAR_OF_B] })) })
    const stations = useAdminStationsStore()
    await stations.loadAtTheFestival(FESTIVAL_A)

    const loadingB = stations.loadAtTheFestival(FESTIVAL_B)

    expect(whatTheStationsShow(stations)).toEqual({ loadedFestivalId: FESTIVAL_A, names: ['Küche'] })
    holdB.release()
    await loadingB
  })

  it('become the stations of the other festival once its answer arrives', async () => {
    const holdB = aHold()
    stationsLaptop({ forB: heldUntil(holdB.released, answer({ stations: [BAR_OF_B] })) })
    const stations = useAdminStationsStore()
    await stations.loadAtTheFestival(FESTIVAL_A)
    const loadingB = stations.loadAtTheFestival(FESTIVAL_B)

    holdB.release()
    await loadingB

    expect(whatTheStationsShow(stations)).toEqual({ loadedFestivalId: FESTIVAL_B, names: ['Theke'] })
  })

  it('ignore a late answer for the earlier festival once the later festival was shown', async () => {
    const holdA = aHold()
    stationsLaptop({ forA: heldUntil(holdA.released, answer({ stations: [KITCHEN_OF_A] })) })
    const stations = useAdminStationsStore()
    const loadingA = stations.loadAtTheFestival(FESTIVAL_A)
    await stations.loadAtTheFestival(FESTIVAL_B)

    holdA.release()
    await loadingA

    expect(whatTheStationsShow(stations)).toEqual({ loadedFestivalId: FESTIVAL_B, names: ['Theke'] })
  })

  it('ignore a late answer of a global reload once a festival was shown', async () => {
    const holdGlobal = aHold()
    stationsLaptop({ forTheGlobalList: heldUntil(holdGlobal.released, answer({ stations: [HEADQUARTERS] })) })
    const stations = useAdminStationsStore()
    const loadingTheGlobalList = stations.load()
    await stations.loadAtTheFestival(FESTIVAL_B)

    holdGlobal.release()
    await loadingTheGlobalList

    expect(whatTheStationsShow(stations)).toEqual({ loadedFestivalId: FESTIVAL_B, names: ['Theke'] })
  })

  it('belong to no festival once the global list answers after a festival', async () => {
    stationsLaptop()
    const stations = useAdminStationsStore()
    await stations.loadAtTheFestival(FESTIVAL_A)

    await stations.load()

    expect(whatTheStationsShow(stations)).toEqual({ loadedFestivalId: null, names: ['Zentrale'] })
  })

  it('claim no festival while an older answer is dropped and a newer request is still on its way', async () => {
    const holdA = aHold()
    const holdB = aHold()
    stationsLaptop({
      forA: heldUntil(holdA.released, answer({ stations: [KITCHEN_OF_A] })),
      forB: heldUntil(holdB.released, answer({ stations: [BAR_OF_B] })),
    })
    const stations = useAdminStationsStore()
    const loadingA = stations.loadAtTheFestival(FESTIVAL_A)
    const loadingB = stations.loadAtTheFestival(FESTIVAL_B)

    holdA.release()
    await loadingA

    expect(whatTheStationsShow(stations)).toEqual({ loadedFestivalId: null, names: [] })
    holdB.release()
    await loadingB
  })
})
