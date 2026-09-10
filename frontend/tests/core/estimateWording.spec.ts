import { describe, expect, it } from 'vitest'
import { estimateText, withEstimate } from '../../src/core/estimateWording'

function wording(key: string, values: Record<string, string | number>): string {
  if (key === 'estimates.inMinutes') {
    return `~${values.count} Min.`
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

describe('a line with the waiting time behind it', () => {
  it('writes the time behind the line it belongs to', () => {
    expect(withEstimate('1 x Bratwurst', 20, wording)).toBe('1 x Bratwurst (~20 Min.)')
  })

  it('leaves the line as it is when there is no time to promise', () => {
    expect(withEstimate('1 x Bratwurst', null, wording)).toBe('1 x Bratwurst')
  })
})
