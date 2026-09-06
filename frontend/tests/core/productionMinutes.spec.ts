import { describe, expect, it } from 'vitest'
import { formatProductionMinutes, parseProductionMinutes } from '../../src/core/productionMinutes'

describe('parseProductionMinutes, the field on the item form', () => {
  it('reads an empty field as no preparation time', () => {
    expect(parseProductionMinutes('')).toEqual({ kind: 'valid', minutes: null })
  })

  it('reads a field of blanks as no preparation time', () => {
    expect(parseProductionMinutes('   ')).toEqual({ kind: 'valid', minutes: null })
  })

  it('reads whole minutes', () => {
    expect(parseProductionMinutes('15')).toEqual({ kind: 'valid', minutes: 15 })
  })

  it('accepts zero', () => {
    expect(parseProductionMinutes('0')).toEqual({ kind: 'valid', minutes: 0 })
  })

  it('accepts the upper bound of ten hours', () => {
    expect(parseProductionMinutes('600')).toEqual({ kind: 'valid', minutes: 600 })
  })

  it('refuses more than ten hours', () => {
    expect(parseProductionMinutes('601')).toEqual({ kind: 'invalid' })
  })

  it('refuses a negative number', () => {
    expect(parseProductionMinutes('-5')).toEqual({ kind: 'invalid' })
  })

  it('refuses fractions of a minute', () => {
    expect(parseProductionMinutes('2.5')).toEqual({ kind: 'invalid' })
  })

  it('refuses words', () => {
    expect(parseProductionMinutes('zehn')).toEqual({ kind: 'invalid' })
  })
})

describe('formatProductionMinutes, what the form shows for a saved item', () => {
  it('shows an empty field for an item without a preparation time', () => {
    expect(formatProductionMinutes(null)).toBe('')
  })

  it('shows the minutes as a plain number', () => {
    expect(formatProductionMinutes(15)).toBe('15')
  })
})
