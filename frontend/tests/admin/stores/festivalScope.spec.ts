import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useAdminItemsStore } from '../../../src/admin/stores/items'
import { useAdminStationsStore } from '../../../src/admin/stores/stations'
import { useConnectionStore } from '../../../src/shared/stores/connection'
import { stubLaptop, answer, type StubbedLaptop, refusal } from '../../support/laptop'
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
            isAtAnyFestival: false,
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
        isAtAnyFestival: false,
      },
    })
  })
})
