import { describe, expect, it } from 'vitest'
import { formatFestivalMoment } from '../../../src/shared/core/festivalTimes'

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
