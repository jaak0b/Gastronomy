import { describe, expect, it } from 'vitest'
import { formatMinutes } from '../../src/core/productionMinutes'

describe('formatMinutes, what a waiting time shows', () => {
  it('keeps a whole number as it is', () => {
    expect(formatMinutes(40, 'de')).toBe('40')
  })

  it('writes half a minute the way German writes it', () => {
    expect(formatMinutes(1.5, 'de')).toBe('1,5')
  })

  it('writes half a minute the way English writes it', () => {
    expect(formatMinutes(1.5, 'en')).toBe('1.5')
  })

  it('rounds binary dust down to one decimal place', () => {
    expect(formatMinutes(0.30000000000000004, 'de')).toBe('0,3')
  })
})
