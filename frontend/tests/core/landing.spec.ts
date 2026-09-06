import { describe, expect, it } from 'vitest'
import { parseDeviceKind, screenFor } from '../../src/core/landing'

describe('screenFor, the screen a device lands on', () => {
  it('sends a station tablet to the station page when it opens the app root', () => {
    expect(screenFor('station', { name: 'home' })).toBe('station')
  })

  it('keeps a station tablet on the station page when it opens the review address', () => {
    expect(screenFor('station', { name: 'review' })).toBe('station')
  })

  it('keeps a station tablet on the station page when it opens the open items address', () => {
    expect(screenFor('station', { name: 'openItems' })).toBe('station')
  })

  it('keeps a station tablet on the station page when it opens the stations address', () => {
    expect(screenFor('station', { name: 'stations' })).toBe('station')
  })

  it('sends a waiter phone to the catalog when it opens the app root', () => {
    expect(screenFor('staffMember', { name: 'home' })).toBe('catalog')
  })

  it('lets a waiter phone open the review', () => {
    expect(screenFor('staffMember', { name: 'review' })).toBe('review')
  })

  it('lets a waiter phone open the open items', () => {
    expect(screenFor('staffMember', { name: 'openItems' })).toBe('openItems')
  })

  it('sends a waiter phone that opens the stations address back to the catalog', () => {
    expect(screenFor('staffMember', { name: 'stations' })).toBe('catalog')
  })

  it('sends a device that is not set up to the welcome screen wherever it opens', () => {
    expect(screenFor(null, { name: 'home' })).toBe('welcome')
    expect(screenFor(null, { name: 'review' })).toBe('welcome')
    expect(screenFor(null, { name: 'stations' })).toBe('welcome')
  })

  it('opens the enrolment landing for any device that scanned a code', () => {
    expect(screenFor(null, { name: 'enrolQr', code: 'abc' })).toBe('enrolQr')
    expect(screenFor('station', { name: 'enrolQr', code: 'abc' })).toBe('enrolQr')
  })

  it('opens the admin for whoever asks for it', () => {
    expect(screenFor(null, { name: 'admin', section: 'overview' })).toBe('admin')
    expect(screenFor('station', { name: 'admin', section: 'items' })).toBe('admin')
  })
})

describe('parseDeviceKind, the kind read back from the phone storage', () => {
  it('reads a stored station kind', () => {
    expect(parseDeviceKind('station')).toBe('station')
  })

  it('reads a stored staff member kind', () => {
    expect(parseDeviceKind('staffMember')).toBe('staffMember')
  })

  it('treats a phone set up before kinds existed as a staff member phone', () => {
    expect(parseDeviceKind(null)).toBe('staffMember')
  })

  it('treats an unreadable value as a staff member phone rather than crashing', () => {
    expect(parseDeviceKind('somethingElse')).toBe('staffMember')
  })
})
