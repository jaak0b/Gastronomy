import { describe, expect, it } from 'vitest'
import {
  estimateRangeText,
  estimateText,
  withEstimate,
} from '../../src/core/estimateWording'

function wording(key: string, values: Record<string, string | number>): string {
  if (key === 'estimates.inMinutes') {
    return `~${values.count} Min.`
  }
  if (key === 'estimates.inMinuteRange') {
    return `~${values.min} - ${values.max} Min.`
  }
  return `${values.line} (${values.estimate})`
}

describe('the waiting time on its own', () => {
  it('names the minutes the phone worked out', () => {
    expect(estimateText(20, wording, 'de')).toBe('~20 Min.')
  })

  it('names half a minute the way German writes it', () => {
    expect(estimateText(1.5, wording, 'de')).toBe('~1,5 Min.')
  })

  it('names half a minute the way English writes it', () => {
    expect(estimateText(1.5, wording, 'en')).toBe('~1.5 Min.')
  })

  it('names nothing when there is no time to promise', () => {
    expect(estimateText(null, wording, 'de')).toBeNull()
  })

  it('names no time at all as a time, because the item is ready right away', () => {
    expect(estimateText(0, wording, 'de')).toBe('~0 Min.')
  })
})

describe('the waiting time behind a station', () => {
  it('names one number when the quickest and the slowest station agree', () => {
    expect(estimateRangeText({ min: 20, max: 20 }, wording, 'de')).toBe('~20 Min.')
  })

  it('names the span between the quickest and the slowest station', () => {
    expect(estimateRangeText({ min: 10, max: 62 }, wording, 'de')).toBe('~10 - 62 Min.')
  })

  it('writes both ends of the span the way German writes them', () => {
    expect(estimateRangeText({ min: 1.5, max: 2.5 }, wording, 'de')).toBe('~1,5 - 2,5 Min.')
  })

  it('writes both ends of the span the way English writes them', () => {
    expect(estimateRangeText({ min: 1.5, max: 2.5 }, wording, 'en')).toBe('~1.5 - 2.5 Min.')
  })

  it('names nothing when there is no time to promise', () => {
    expect(estimateRangeText(null, wording, 'de')).toBeNull()
  })
})

describe('a line with the waiting time behind it', () => {
  it('writes the time behind the line it belongs to', () => {
    expect(withEstimate('1 x Bratwurst', 20, wording, 'de')).toBe('1 x Bratwurst (~20 Min.)')
  })

  it('leaves the line as it is when there is no time to promise', () => {
    expect(withEstimate('1 x Bratwurst', null, wording, 'de')).toBe('1 x Bratwurst')
  })
})
