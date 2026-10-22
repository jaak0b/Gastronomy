import { describe, expect, it } from 'vitest'
import { timeAxisTicksOf, timeTickFormOf } from '../../../src/admin/core/timeAxisTicks'
import { formatTimeTick } from '../../../src/shared/core/moments'

function isoTicksOf(startsAtUtc: string, endsAtUtc: string): string[] {
  const startsAtMs = Date.parse(startsAtUtc)
  const endsAtMs = Date.parse(endsAtUtc)
  return timeAxisTicksOf(startsAtMs, endsAtMs, timeTickFormOf(startsAtMs, endsAtMs)).map((tick) =>
    new Date(tick).toISOString(),
  )
}

function germanTickTextsOf(startsAtUtc: string, endsAtUtc: string): string[] {
  const startsAtMs = Date.parse(startsAtUtc)
  const endsAtMs = Date.parse(endsAtUtc)
  const form = timeTickFormOf(startsAtMs, endsAtMs)
  return timeAxisTicksOf(startsAtMs, endsAtMs, form).map((tick) => formatTimeTick(new Date(tick), 'de', form))
}

describe('the form of the time labels of a festival', () => {
  it.each([
    { length: 'twelve hours', endsAtUtc: '2026-07-18T22:00:00Z', form: 'timeOfDay' },
    { length: 'exactly one day', endsAtUtc: '2026-07-19T10:00:00Z', form: 'timeOfDay' },
    { length: 'one day and an hour', endsAtUtc: '2026-07-19T11:00:00Z', form: 'weekdayAndTime' },
    { length: 'exactly three days', endsAtUtc: '2026-07-21T10:00:00Z', form: 'weekdayAndTime' },
    { length: 'three days and an hour', endsAtUtc: '2026-07-21T11:00:00Z', form: 'date' },
  ])('is chosen by the length of a festival of $length', ({ endsAtUtc, form }) => {
    expect(timeTickFormOf(Date.parse('2026-07-18T10:00:00Z'), Date.parse(endsAtUtc))).toBe(form)
  })
})

describe('the ticks on the time axis of a festival', () => {
  it('fall on whole local hours every two hours for an evening that lasts sixteen hours', () => {
    expect(isoTicksOf('2026-07-18T10:00:00Z', '2026-07-19T02:00:00Z')).toEqual([
      '2026-07-18T10:00:00.000Z',
      '2026-07-18T12:00:00.000Z',
      '2026-07-18T14:00:00.000Z',
      '2026-07-18T16:00:00.000Z',
      '2026-07-18T18:00:00.000Z',
      '2026-07-18T20:00:00.000Z',
      '2026-07-18T22:00:00.000Z',
      '2026-07-19T00:00:00.000Z',
      '2026-07-19T02:00:00.000Z',
    ])
  })

  it('start at the first whole step after a start between two steps', () => {
    expect(isoTicksOf('2026-07-18T10:20:00Z', '2026-07-18T13:00:00Z')).toEqual([
      '2026-07-18T10:30:00.000Z',
      '2026-07-18T11:00:00.000Z',
      '2026-07-18T11:30:00.000Z',
      '2026-07-18T12:00:00.000Z',
      '2026-07-18T12:30:00.000Z',
      '2026-07-18T13:00:00.000Z',
    ])
  })

  it('name the weekday and the time every six hours for a festival of two days', () => {
    expect(germanTickTextsOf('2026-07-18T10:00:00Z', '2026-07-20T10:00:00Z')).toEqual([
      'Sa., 12:00',
      'Sa., 18:00',
      'So., 00:00',
      'So., 06:00',
      'So., 12:00',
      'So., 18:00',
      'Mo., 00:00',
      'Mo., 06:00',
      'Mo., 12:00',
    ])
  })

  it('name each date once, at local midnight, for a festival of five days', () => {
    expect(germanTickTextsOf('2026-07-18T08:00:00Z', '2026-07-22T18:00:00Z')).toEqual([
      '19.07.',
      '20.07.',
      '21.07.',
      '22.07.',
    ])
  })

  it('name every second date for a festival of two weeks', () => {
    expect(germanTickTextsOf('2026-07-01T08:00:00Z', '2026-07-15T18:00:00Z')).toEqual([
      '02.07.',
      '04.07.',
      '06.07.',
      '08.07.',
      '10.07.',
      '12.07.',
      '14.07.',
    ])
  })

  it('name no hour twice in the night the clocks go back', () => {
    expect(germanTickTextsOf('2026-10-24T20:00:00Z', '2026-10-25T04:00:00Z')).toEqual([
      '22:00',
      '23:00',
      '00:00',
      '01:00',
      '02:00',
      '03:00',
      '04:00',
      '05:00',
    ])
  })
})
