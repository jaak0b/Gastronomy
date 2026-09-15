import { describe, expect, it } from 'vitest'
import {
  formatMinutes,
  formatProductionMinutes,
  parseProductionMinutes,
} from '../../src/core/productionMinutes'

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

  it('reads half a minute written with a comma, the way German writes it', () => {
    expect(parseProductionMinutes('1,5')).toEqual({ kind: 'valid', minutes: 1.5 })
  })

  it('reads half a minute written with a dot, the way English writes it', () => {
    expect(parseProductionMinutes('1.5')).toEqual({ kind: 'valid', minutes: 1.5 })
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

  it('refuses a fraction that would push it over ten hours', () => {
    expect(parseProductionMinutes('600,5')).toEqual({ kind: 'invalid' })
  })

  it('refuses a second decimal place', () => {
    expect(parseProductionMinutes('1,55')).toEqual({ kind: 'invalid' })
  })

  it('refuses a trailing separator without a digit', () => {
    expect(parseProductionMinutes('1,')).toEqual({ kind: 'invalid' })
  })

  it('refuses a negative number', () => {
    expect(parseProductionMinutes('-5')).toEqual({ kind: 'invalid' })
  })

  it('refuses words', () => {
    expect(parseProductionMinutes('zehn')).toEqual({ kind: 'invalid' })
  })
})

describe('formatProductionMinutes, what the form shows for a saved item', () => {
  it('shows an empty field for an item without a preparation time', () => {
    expect(formatProductionMinutes(null, 'de')).toBe('')
  })

  it('shows whole minutes as a plain number in both languages', () => {
    expect(formatProductionMinutes(15, 'de')).toBe('15')
    expect(formatProductionMinutes(15, 'en')).toBe('15')
  })

  it('shows half a minute with the German comma', () => {
    expect(formatProductionMinutes(1.5, 'de')).toBe('1,5')
  })

  it('shows half a minute with the English dot', () => {
    expect(formatProductionMinutes(1.5, 'en')).toBe('1.5')
  })
})

describe('formatMinutes, what a waiting time shows', () => {
  it('keeps a whole number as it is', () => {
    expect(formatMinutes(40, 'de')).toBe('40')
  })

  it('rounds binary dust down to one decimal place', () => {
    expect(formatMinutes(0.30000000000000004, 'de')).toBe('0,3')
  })
})
