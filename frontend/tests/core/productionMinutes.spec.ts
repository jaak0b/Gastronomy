import { describe, expect, it } from 'vitest'
import { formatMinutes } from '../../src/core/productionMinutes'

describe('formatMinutes, what a waiting time shows', () => {
  it('keeps a whole number as it is', () => {
    expect(formatMinutes(142, 'de')).toBe('142')
  })

  it('rounds a minute and a bit up to the next whole minute', () => {
    expect(formatMinutes(139.2, 'de')).toBe('140')
  })

  it('rounds half a minute up to a whole one', () => {
    expect(formatMinutes(0.5, 'en')).toBe('1')
  })

  it('keeps nothing to wait for at nothing', () => {
    expect(formatMinutes(0, 'de')).toBe('0')
  })

  it('rounds binary dust up too', () => {
    expect(formatMinutes(0.30000000000000004, 'de')).toBe('1')
  })

  it('groups four digits the way German writes them', () => {
    expect(formatMinutes(1234.5, 'de')).toBe('1.235')
  })

  it('groups four digits the way English writes them', () => {
    expect(formatMinutes(1234.5, 'en')).toBe('1,235')
  })
})
