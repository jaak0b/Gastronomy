import { describe, expect, it } from 'vitest'
import { isDrawnAtPhoneSizes, screenFor, waiterLandingFor } from '../../../src/shared/core/landing'

const aStationTablet = { state: 'stationTablet' } as const
const aWaiterPhone = { state: 'waiterPhone' } as const
const aDeviceThatIsNotSetUp = { state: 'notSetUp' } as const
const aDeviceWaitingForTheLaptop = { state: 'startingUp' } as const

describe('screenFor, the screen a device lands on', () => {
  it('sends a station tablet to the station page when it opens the app root', () => {
    expect(screenFor(aStationTablet, { name: 'home' })).toBe('station')
  })

  it('keeps a station tablet on the station page when it opens the review address', () => {
    expect(screenFor(aStationTablet, { name: 'review' })).toBe('station')
  })

  it('keeps a station tablet on the station page when it opens the open items address', () => {
    expect(screenFor(aStationTablet, { name: 'openItems' })).toBe('station')
  })

  it('keeps a station tablet on the station page when it opens the stations address', () => {
    expect(screenFor(aStationTablet, { name: 'stations' })).toBe('station')
  })

  it('sends a waiter phone to the table plan when it opens the app root', () => {
    expect(screenFor(aWaiterPhone, { name: 'home' })).toBe('tablePlan')
  })

  it('lets a waiter phone open the catalog', () => {
    expect(screenFor(aWaiterPhone, { name: 'catalog' })).toBe('catalog')
  })

  it('lets a waiter phone open the review', () => {
    expect(screenFor(aWaiterPhone, { name: 'review' })).toBe('review')
  })

  it('lets a waiter phone open the open items', () => {
    expect(screenFor(aWaiterPhone, { name: 'openItems' })).toBe('openItems')
  })

  it('lets a waiter phone open the orders of a table', () => {
    expect(screenFor(aWaiterPhone, { name: 'orderOverview' })).toBe('orderOverview')
  })

  it('keeps a station tablet on the station page when it opens the order overview address', () => {
    expect(screenFor(aStationTablet, { name: 'orderOverview' })).toBe('station')
  })

  it('sends a waiter phone that opens the stations address back to the table plan', () => {
    expect(screenFor(aWaiterPhone, { name: 'stations' })).toBe('tablePlan')
  })

  it('sends a device that is not set up to the welcome screen wherever it opens', () => {
    expect(screenFor(aDeviceThatIsNotSetUp, { name: 'home' })).toBe('welcome')
    expect(screenFor(aDeviceThatIsNotSetUp, { name: 'review' })).toBe('welcome')
    expect(screenFor(aDeviceThatIsNotSetUp, { name: 'stations' })).toBe('welcome')
  })

  it('opens the enrolment landing for any device that scanned a code', () => {
    expect(screenFor(aDeviceThatIsNotSetUp, { name: 'enrolQr', code: 'abc' })).toBe('enrolQr')
    expect(screenFor(aStationTablet, { name: 'enrolQr', code: 'abc' })).toBe('enrolQr')
  })

  it('opens the admin for whoever asks for it', () => {
    expect(screenFor(aDeviceThatIsNotSetUp, { name: 'admin', section: 'overview', festivalId: null })).toBe('admin')
    expect(screenFor(aStationTablet, { name: 'admin', section: 'items', festivalId: null })).toBe('admin')
  })
})

describe('a device that is set up while the laptop has not said yet whose device it is', () => {
  it('waits on the starting screen wherever it opens', () => {
    expect(screenFor(aDeviceWaitingForTheLaptop, { name: 'home' })).toBe('startingUp')
    expect(screenFor(aDeviceWaitingForTheLaptop, { name: 'review' })).toBe('startingUp')
    expect(screenFor(aDeviceWaitingForTheLaptop, { name: 'openItems' })).toBe('startingUp')
    expect(screenFor(aDeviceWaitingForTheLaptop, { name: 'stations' })).toBe('startingUp')
  })

  it('still lands on the enrolment page when it scanned a code', () => {
    expect(screenFor(aDeviceWaitingForTheLaptop, { name: 'enrolQr', code: 'abc' })).toBe('enrolQr')
  })

  it('still opens the admin when the address asks for it', () => {
    expect(screenFor(aDeviceWaitingForTheLaptop, { name: 'admin', section: 'overview', festivalId: null })).toBe(
      'admin',
    )
  })
})

describe('waiterLandingFor, what the waiter sees on the home screen', () => {
  it('opens straight on the plan when the festival has exactly one area', () => {
    expect(waiterLandingFor({ orderIsLocked: false, festivalIsRunning: true, areaIds: ['area-halle'], openedAreaId: null })).toEqual({
      kind: 'plan',
      areaId: 'area-halle',
    })
  })

  it('lists the areas when the festival has several and none is open yet', () => {
    expect(
      waiterLandingFor({ orderIsLocked: false, festivalIsRunning: true, areaIds: ['area-halle', 'area-zelt'], openedAreaId: null }),
    ).toEqual({ kind: 'areaList' })
  })

  it('shows the plan of the area the waiter opened', () => {
    expect(
      waiterLandingFor({ orderIsLocked: false, festivalIsRunning: true, areaIds: ['area-halle', 'area-zelt'], openedAreaId: 'area-zelt' }),
    ).toEqual({ kind: 'plan', areaId: 'area-zelt' })
  })

  it('lists the areas again when the area the waiter opened is no longer shown', () => {
    expect(
      waiterLandingFor({ orderIsLocked: false, festivalIsRunning: true, areaIds: ['area-halle', 'area-zelt'], openedAreaId: 'area-garten' }),
    ).toEqual({ kind: 'areaList' })
  })

  it('says that no festival is active when none runs', () => {
    expect(waiterLandingFor({ orderIsLocked: false, festivalIsRunning: false, areaIds: [], openedAreaId: null })).toEqual({
      kind: 'noFestival',
    })
  })

  it('says that no tables are set up when the festival has no area', () => {
    expect(waiterLandingFor({ orderIsLocked: false, festivalIsRunning: true, areaIds: [], openedAreaId: null })).toEqual({
      kind: 'noTables',
    })
  })

  it('leads to the order nobody knows the fate of, before anything else', () => {
    expect(waiterLandingFor({ orderIsLocked: true, festivalIsRunning: true, areaIds: ['area-halle'], openedAreaId: null })).toEqual({
      kind: 'lockedOrder',
    })
  })
})

describe('isDrawnAtPhoneSizes, which screens take the sizes of the phone', () => {
  it.each(['tablePlan', 'catalog', 'review', 'openItems', 'orderOverview'] as const)(
    'draws the waiter screen %s at the phone sizes',
    (screen) => {
      expect(isDrawnAtPhoneSizes(screen)).toBe(true)
    },
  )

  it.each(['doorGate', 'enrolQr', 'welcome', 'startingUp'] as const)(
    'draws the start screen %s, shown before the device knows whose it is, at the phone sizes',
    (screen) => {
      expect(isDrawnAtPhoneSizes(screen)).toBe(true)
    },
  )

  it('leaves the station page to the station sizes', () => {
    expect(isDrawnAtPhoneSizes('station')).toBe(false)
  })

  it('leaves the admin to the admin sizes', () => {
    expect(isDrawnAtPhoneSizes('admin')).toBe(false)
  })
})
