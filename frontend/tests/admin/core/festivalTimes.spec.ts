import { describe, expect, it } from 'vitest'
import {
  formatRunOutMoment,
  localInputToUtcIso,
  utcIsoToLocalInput,
} from '../../../src/admin/core/festivalTimes'

describe('the moment the admin types', () => {
  it('travels as an ISO string in UTC ending in Z', () => {
    const iso = localInputToUtcIso('2026-07-18T17:00')

    expect(iso).toBe(new Date('2026-07-18T17:00').toISOString())
    expect(iso?.endsWith('Z')).toBe(true)
  })

  it('is nothing when the field is empty or unreadable', () => {
    expect(localInputToUtcIso('')).toBeNull()
    expect(localInputToUtcIso('   ')).toBeNull()
    expect(localInputToUtcIso('the eighteenth')).toBeNull()
  })

  it('comes back into the field as the same local moment', () => {
    const iso = localInputToUtcIso('2026-07-18T17:00') as string

    expect(utcIsoToLocalInput(iso)).toBe('2026-07-18T17:00')
  })

  it('leaves the field empty when the laptop sent something unreadable', () => {
    expect(utcIsoToLocalInput('not a moment')).toBe('')
  })
})


describe('the moment the stock of an ingredient is expected to run out', () => {
  const NOW = new Date(2026, 6, 18, 18, 0)

  it('is written as the time of day in German when it is today', () => {
    const today = new Date(2026, 6, 18, 21, 30).toISOString()

    expect(formatRunOutMoment(today, 'de', NOW)).toBe('21:30')
  })

  it('is written as the time of day in English when it is today', () => {
    const today = new Date(2026, 6, 18, 21, 30).toISOString()

    expect(formatRunOutMoment(today, 'en', NOW)).toBe('09:30 PM')
  })

  it('carries the date when it is another day', () => {
    const tomorrow = new Date(2026, 6, 19, 1, 15).toISOString()

    expect(formatRunOutMoment(tomorrow, 'de', NOW)).toBe('19.07., 01:15')
  })

  it('is blank when the laptop sent something unreadable', () => {
    expect(formatRunOutMoment('not a moment', 'de', NOW)).toBe('')
  })
})
