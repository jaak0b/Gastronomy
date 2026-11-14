import { describe, expect, it } from 'vitest'
import {
  amountFieldRoomFor,
  amountInputFor,
  parseAmountInput,
} from '../../../src/admin/core/ingredientAmounts'

describe('the room of an amount field', () => {
  it('holds the characters of the typed amount and of the unit name', () => {
    expect(amountFieldRoomFor('1250', 'Flasche')).toEqual({
      amountCharacters: 4,
      unitCharacters: 7,
      unitIsShortened: false,
    })
  })

  it('holds at least three characters of the amount', () => {
    expect(amountFieldRoomFor('1', 'g').amountCharacters).toBe(3)
    expect(amountFieldRoomFor('', 'g').amountCharacters).toBe(3)
  })

  it('holds at most ten characters of the unit name and marks it shortened', () => {
    expect(amountFieldRoomFor('1', 'Stück!!!!!!!!!!!')).toEqual({
      amountCharacters: 3,
      unitCharacters: 10,
      unitIsShortened: true,
    })
  })

  it('does not shorten a unit name of exactly ten characters', () => {
    expect(amountFieldRoomFor('1', 'Milliliter').unitIsShortened).toBe(false)
  })

  it('counts the typed text as it is, without trimming', () => {
    expect(amountFieldRoomFor('12345 ', 'g').amountCharacters).toBe(6)
  })
})

describe('an amount the admin typed', () => {
  it('is the number that was typed', () => {
    expect(parseAmountInput('250')).toEqual({ kind: 'amount', amount: 250 })
  })

  it('accepts a German decimal comma', () => {
    expect(parseAmountInput('1,5')).toEqual({ kind: 'amount', amount: 1.5 })
  })

  it('accepts an English decimal point', () => {
    expect(parseAmountInput('0.33')).toEqual({ kind: 'amount', amount: 0.33 })
  })

  it('keeps every digit of a decimal number', () => {
    expect(parseAmountInput('1,005')).toEqual({ kind: 'amount', amount: 1.005 })
    expect(parseAmountInput('0,0000001')).toEqual({ kind: 'amount', amount: 0.0000001 })
  })

  it('keeps a decimal number of pieces as typed', () => {
    expect(parseAmountInput('0,5')).toEqual({ kind: 'amount', amount: 0.5 })
  })

  it('is empty when nothing was typed', () => {
    expect(parseAmountInput('  ')).toEqual({ kind: 'empty' })
  })

  it('is unreadable when it is not a number', () => {
    expect(parseAmountInput('viel')).toEqual({ kind: 'unreadable' })
    expect(parseAmountInput('-5')).toEqual({ kind: 'unreadable' })
    expect(parseAmountInput('1,2,3')).toEqual({ kind: 'unreadable' })
  })
})

describe('a saved amount shown back in its field', () => {
  it('is written with no grouping and no conversion', () => {
    expect(amountInputFor(1500, 'de')).toBe('1500')
    expect(amountInputFor(1250, 'en')).toBe('1250')
  })

  it('is written with the decimal comma of the language', () => {
    expect(amountInputFor(0.5, 'de')).toBe('0,5')
    expect(amountInputFor(0.5, 'en')).toBe('0.5')
  })

  it('keeps every significant digit', () => {
    expect(amountInputFor(0.0001, 'de')).toBe('0,0001')
    expect(amountInputFor(1234.5678, 'en')).toBe('1234.5678')
  })

  it('is an empty field when there is no amount', () => {
    expect(amountInputFor(null, 'de')).toBe('')
  })
})
