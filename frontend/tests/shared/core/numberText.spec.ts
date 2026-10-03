import { describe, expect, it } from 'vitest'
import { formatNumber } from '../../../src/shared/core/numberText'

describe('formatNumber', () => {
  it('writes a German number with a decimal comma and a thousands point', () => {
    expect(formatNumber(12345.5, 'de', 1)).toBe('12.345,5')
  })

  it('writes an English number with a decimal point and a thousands comma', () => {
    expect(formatNumber(12345.5, 'en', 1)).toBe('12,345.5')
  })

  it('rounds to the number of decimals it is given', () => {
    expect(formatNumber(1.2345, 'en', 2)).toBe('1.23')
  })
})
