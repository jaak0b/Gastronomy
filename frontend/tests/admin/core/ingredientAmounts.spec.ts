import { describe, expect, it } from 'vitest'
import {
  amountInputFor,
  amountInputSwitchedTo,
  entryUnitsFor,
  ingredientUnitOf,
  parseAmountInput,
  displayedAmountFor,
} from '../../../src/admin/core/ingredientAmounts'

describe('the units an amount can be typed in', () => {
  it('offer grams and kilograms for an ingredient kept in grams', () => {
    expect(entryUnitsFor('gram')).toEqual(['gram', 'kilogram'])
  })

  it('offer millilitres and litres for an ingredient kept in millilitres', () => {
    expect(entryUnitsFor('millilitre')).toEqual(['millilitre', 'litre'])
  })

  it('offer only pieces for an ingredient counted in pieces', () => {
    expect(entryUnitsFor('piece')).toEqual(['piece'])
  })
})

describe('an amount the admin typed', () => {
  it('travels in grams when typed in grams', () => {
    expect(parseAmountInput({ typed: '250', entryUnit: 'gram' })).toEqual({
      kind: 'amount',
      baseAmount: 250,
    })
  })

  it('travels in grams when typed in kilograms with a German decimal comma', () => {
    expect(parseAmountInput({ typed: '1,5', entryUnit: 'kilogram' })).toEqual({
      kind: 'amount',
      baseAmount: 1500,
    })
  })

  it('travels in millilitres when typed in litres with a decimal point', () => {
    expect(parseAmountInput({ typed: '0.33', entryUnit: 'litre' })).toEqual({
      kind: 'amount',
      baseAmount: 330,
    })
  })

  it('keeps every digit of a kilogram amount with three decimals', () => {
    expect(parseAmountInput({ typed: '1,005', entryUnit: 'kilogram' })).toEqual({
      kind: 'amount',
      baseAmount: 1005,
    })
  })

  it('keeps a decimal number of pieces as typed', () => {
    expect(parseAmountInput({ typed: '0,5', entryUnit: 'piece' })).toEqual({
      kind: 'amount',
      baseAmount: 0.5,
    })
  })

  it('is empty when nothing was typed', () => {
    expect(parseAmountInput({ typed: '  ', entryUnit: 'gram' })).toEqual({ kind: 'empty' })
  })

  it('is unreadable when it is not a number', () => {
    expect(parseAmountInput({ typed: 'viel', entryUnit: 'gram' })).toEqual({ kind: 'unreadable' })
    expect(parseAmountInput({ typed: '-5', entryUnit: 'gram' })).toEqual({ kind: 'unreadable' })
    expect(parseAmountInput({ typed: '1,2,3', entryUnit: 'gram' })).toEqual({
      kind: 'unreadable',
    })
  })
})

describe('a saved amount shown back in its field', () => {
  it('is written in grams with no conversion', () => {
    expect(amountInputFor(1500, 'gram', 'de')).toEqual({ typed: '1500', entryUnit: 'gram' })
  })

  it('is written in kilograms with a German comma', () => {
    expect(amountInputFor(1500, 'kilogram', 'de')).toEqual({ typed: '1,5', entryUnit: 'kilogram' })
  })

  it('is written in litres with an English point', () => {
    expect(amountInputFor(1250, 'litre', 'en')).toEqual({ typed: '1.25', entryUnit: 'litre' })
  })

  it('keeps a decimal number of pieces', () => {
    expect(amountInputFor(0.5, 'piece', 'de')).toEqual({ typed: '0,5', entryUnit: 'piece' })
  })

  it('is an empty field when there is no amount', () => {
    expect(amountInputFor(null, 'millilitre', 'de')).toEqual({ typed: '', entryUnit: 'millilitre' })
  })
})

describe('an amount written on the screen', () => {
  it('is counted in kilograms from 1000 grams on, in German', () => {
    expect(displayedAmountFor(1500, 'gram', 'de')).toEqual({
      count: 1.5,
      formattedCount: '1,5',
      entryUnit: 'kilogram',
    })
  })

  it('is counted in grams below 1000', () => {
    expect(displayedAmountFor(999, 'gram', 'en')).toEqual({
      count: 999,
      formattedCount: '999',
      entryUnit: 'gram',
    })
  })

  it('is counted in litres from 1000 millilitres on, in English', () => {
    expect(displayedAmountFor(1250, 'millilitre', 'en')).toEqual({
      count: 1.25,
      formattedCount: '1.25',
      entryUnit: 'litre',
    })
  })

  it('stays in pieces with the number written in the device language', () => {
    expect(displayedAmountFor(12000, 'piece', 'de')).toEqual({
      count: 12000,
      formattedCount: '12.000',
      entryUnit: 'piece',
    })
  })
})

describe('the unit a new ingredient is kept in', () => {
  it('is grams when the amount was typed in kilograms', () => {
    expect(ingredientUnitOf('kilogram')).toBe('gram')
  })

  it('is millilitres when the amount was typed in litres', () => {
    expect(ingredientUnitOf('litre')).toBe('millilitre')
  })

  it('is pieces when the amount was typed in pieces', () => {
    expect(ingredientUnitOf('piece')).toBe('piece')
  })
})

describe('switching the unit an amount is shown in', () => {
  it('writes 5 kilograms as 5000 grams', () => {
    expect(amountInputSwitchedTo({ typed: '5', entryUnit: 'kilogram' }, 'gram', 'de')).toEqual({
      typed: '5000',
      entryUnit: 'gram',
    })
  })

  it('writes 1,5 kilograms as 1500 grams', () => {
    expect(amountInputSwitchedTo({ typed: '1,5', entryUnit: 'kilogram' }, 'gram', 'de')).toEqual({
      typed: '1500',
      entryUnit: 'gram',
    })
  })

  it('writes 250 grams as 0,25 kilograms in German', () => {
    expect(amountInputSwitchedTo({ typed: '250', entryUnit: 'gram' }, 'kilogram', 'de')).toEqual({
      typed: '0,25',
      entryUnit: 'kilogram',
    })
  })

  it('writes 250 millilitres as 0.25 litres in English', () => {
    expect(
      amountInputSwitchedTo({ typed: '250', entryUnit: 'millilitre' }, 'litre', 'en'),
    ).toEqual({ typed: '0.25', entryUnit: 'litre' })
  })

  it('keeps unreadable text exactly as typed and changes only the unit', () => {
    expect(amountInputSwitchedTo({ typed: 'viel ', entryUnit: 'kilogram' }, 'gram', 'de')).toEqual({
      typed: 'viel ',
      entryUnit: 'gram',
    })
  })

  it('keeps an empty field empty', () => {
    expect(amountInputSwitchedTo({ typed: '', entryUnit: 'gram' }, 'kilogram', 'de')).toEqual({
      typed: '',
      entryUnit: 'kilogram',
    })
  })
})

describe('switching between units of different kinds', () => {
  it('keeps the typed number and changes only the unit', () => {
    expect(amountInputSwitchedTo({ typed: '1,5', entryUnit: 'kilogram' }, 'piece', 'de')).toEqual({
      typed: '1,5',
      entryUnit: 'piece',
    })
  })
})
