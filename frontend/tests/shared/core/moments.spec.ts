import { describe, expect, it } from 'vitest'
import {
  formatFestivalMoment,
  formatTimeOfDay,
  formatTimeTick,
  localInputToUtcIso,
  spansMoreThanOneDay,
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


describe('a time of day in the stock section', () => {
  const SATURDAY_EVENING = new Date(2026, 6, 18, 21, 30)

  it('is the hour and minute in German', () => {
    expect(formatTimeOfDay(SATURDAY_EVENING, 'de', false)).toBe('21:30')
  })

  it('is the hour and minute in English', () => {
    expect(formatTimeOfDay(SATURDAY_EVENING, 'en', false)).toBe('09:30 PM')
  })

  it('names the weekday in German when asked to', () => {
    expect(formatTimeOfDay(SATURDAY_EVENING, 'de', true)).toBe('Sa., 21:30')
  })

  it('names the weekday in English when asked to', () => {
    expect(formatTimeOfDay(SATURDAY_EVENING, 'en', true)).toBe('Sat 09:30 PM')
  })

  it('is blank when the laptop sent something unreadable', () => {
    expect(formatTimeOfDay(new Date('not a moment'), 'de', false)).toBe('')
  })
})

describe('a label on the time axis of a chart', () => {
  const SATURDAY_EVENING = new Date(2026, 6, 18, 21, 30)

  it.each([
    { form: 'timeOfDay', language: 'de', text: '21:30' },
    { form: 'timeOfDay', language: 'en', text: '09:30 PM' },
    { form: 'weekdayAndTime', language: 'de', text: 'Sa., 21:30' },
    { form: 'weekdayAndTime', language: 'en', text: 'Sat 09:30 PM' },
    { form: 'date', language: 'de', text: '18.07.' },
    { form: 'date', language: 'en', text: '07/18' },
  ] as const)('is written as $text for the form $form in $language', ({ form, language, text }) => {
    expect(formatTimeTick(SATURDAY_EVENING, language, form)).toBe(text)
  })

  it('is blank for a date when the laptop sent something unreadable', () => {
    expect(formatTimeTick(new Date('not a moment'), 'de', 'date')).toBe('')
  })
})

describe('a festival period', () => {
  it('spans one day when it starts and ends on the same local day', () => {
    expect(spansMoreThanOneDay('2026-07-18T08:00:00Z', '2026-07-18T20:00:00Z')).toBe(false)
  })

  it('spans more than one day when it runs past local midnight', () => {
    expect(spansMoreThanOneDay('2026-07-18T16:00:00Z', '2026-07-18T23:00:00Z')).toBe(true)
  })
})
