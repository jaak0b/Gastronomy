import { describe, expect, it } from 'vitest'
import { screenFor } from '../../src/core/landing'

const aStationTablet = { state: 'setUp', deviceKind: 'station' } as const
const aWaiterPhone = { state: 'setUp', deviceKind: 'staffMember' } as const
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

  it('sends a waiter phone to the catalog when it opens the app root', () => {
    expect(screenFor(aWaiterPhone, { name: 'home' })).toBe('catalog')
  })

  it('lets a waiter phone open the review', () => {
    expect(screenFor(aWaiterPhone, { name: 'review' })).toBe('review')
  })

  it('lets a waiter phone open the open items', () => {
    expect(screenFor(aWaiterPhone, { name: 'openItems' })).toBe('openItems')
  })

  it('sends a waiter phone that opens the stations address back to the catalog', () => {
    expect(screenFor(aWaiterPhone, { name: 'stations' })).toBe('catalog')
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
    expect(screenFor(aDeviceThatIsNotSetUp, { name: 'admin', section: 'overview' })).toBe('admin')
    expect(screenFor(aStationTablet, { name: 'admin', section: 'items' })).toBe('admin')
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
    expect(screenFor(aDeviceWaitingForTheLaptop, { name: 'admin', section: 'overview' })).toBe(
      'admin',
    )
  })
})
