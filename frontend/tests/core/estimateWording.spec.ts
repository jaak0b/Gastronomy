import { describe, expect, it } from 'vitest'
import {
  estimateRangeText,
  estimateText,
  withEstimate,
  withRangeEstimate,
} from '../../src/core/estimateWording'

function wording(key: string, values: Record<string, string | number>): string {
  if (key === 'estimates.inMinutes') {
    return `~${values.count} Min.`
  }
  if (key === 'estimates.inMinuteRange') {
    return `~${values.min} bis ${values.max} Min.`
  }
  return `${values.line} (${values.estimate})`
}

describe('the waiting time on its own', () => {
  it('names the minutes the phone worked out', () => {
    expect(estimateText(20, wording)).toBe('~20 Min.')
  })

  it('names nothing when there is no time to promise', () => {
    expect(estimateText(null, wording)).toBeNull()
  })

  it('names no time at all as a time, because the item is ready right away', () => {
    expect(estimateText(0, wording)).toBe('~0 Min.')
  })
})

describe('the waiting time behind a station', () => {
  it('names one number when the quickest and the slowest station agree', () => {
    expect(estimateRangeText({ min: 20, max: 20 }, wording)).toBe('~20 Min.')
  })

  it('names the span between the quickest and the slowest station', () => {
    expect(estimateRangeText({ min: 10, max: 62 }, wording)).toBe('~10 bis 62 Min.')
  })

  it('names nothing when there is no time to promise', () => {
    expect(estimateRangeText(null, wording)).toBeNull()
  })
})

describe('a line with the waiting time behind it', () => {
  it('writes the time behind the line it belongs to', () => {
    expect(withEstimate('1 x Bratwurst', 20, wording)).toBe('1 x Bratwurst (~20 Min.)')
  })

  it('leaves the line as it is when there is no time to promise', () => {
    expect(withEstimate('1 x Bratwurst', null, wording)).toBe('1 x Bratwurst')
  })

  it('writes one number behind the line when both stations agree', () => {
    expect(withRangeEstimate('1 x Bratwurst', { min: 20, max: 20 }, wording)).toBe(
      '1 x Bratwurst (~20 Min.)',
    )
  })

  it('writes the span behind the line when the stations differ', () => {
    expect(withRangeEstimate('1 x Bratwurst', { min: 10, max: 62 }, wording)).toBe(
      '1 x Bratwurst (~10 bis 62 Min.)',
    )
  })

  it('leaves the line as it is when there is no range to promise', () => {
    expect(withRangeEstimate('1 x Bratwurst', null, wording)).toBe('1 x Bratwurst')
  })
})
