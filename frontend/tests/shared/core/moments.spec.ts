import { describe, expect, it } from 'vitest'
import {
  formatFestivalMoment,
  formatRunOutMoment,
  localInputToUtcIso,
  utcIsoToLocalInput,
} from '../../../src/shared/core/moments'

describe('a festival moment on the screen', () => {
  it('is read with the browser and never repaired when the Z is missing', () => {
    const withZ = formatFestivalMoment('2026-07-18T15:00:00Z', 'de')
    const withoutZ = formatFestivalMoment('2026-07-18T15:00:00', 'de')

    expect(withZ).toBe(new Date('2026-07-18T15:00:00Z').toLocaleString('de', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    }))
    expect(withoutZ).toBe(new Date('2026-07-18T15:00:00').toLocaleString('de', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    }))
  })

  it('stays empty when the value cannot be read as a moment', () => {
    expect(formatFestivalMoment('later', 'en')).toBe('')
  })
})


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
