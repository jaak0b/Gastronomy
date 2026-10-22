import { describe, expect, it } from 'vitest'
import {
  amountInputFor,
  amountInputSwitchedTo,
  entryUnitsFor,
  parseAmountInput,
  displayedAmountIn,
  shownEntryUnitFor,
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

  it('travels in grams when a tiny amount is typed in kilograms', () => {
    expect(parseAmountInput({ typed: '0,0000001', entryUnit: 'kilogram' })).toEqual({
      kind: 'amount',
      baseAmount: 0.0001,
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

  it('writes a tiny amount in kilograms as the digits the admin typed', () => {
    expect(amountInputFor(0.0001, 'kilogram', 'de')).toEqual({ typed: '0,0000001', entryUnit: 'kilogram' })
  })

  it('is an empty field when there is no amount', () => {
    expect(amountInputFor(null, 'millilitre', 'de')).toEqual({ typed: '', entryUnit: 'millilitre' })
  })
})

describe('the unit an amount is shown in', () => {
  it.each([
    { baseAmount: 999, unit: 'gram', entryUnit: 'gram' },
    { baseAmount: 1000, unit: 'gram', entryUnit: 'kilogram' },
    { baseAmount: 1000, unit: 'millilitre', entryUnit: 'litre' },
    { baseAmount: 999.9996, unit: 'millilitre', entryUnit: 'litre' },
    { baseAmount: 5000, unit: 'piece', entryUnit: 'piece' },
  ] as const)('is $entryUnit for $baseAmount in $unit', ({ baseAmount, unit, entryUnit }) => {
    expect(shownEntryUnitFor(baseAmount, unit)).toBe(entryUnit)
  })
})

describe('an amount written in a unit chosen beforehand', () => {
  it('writes 250 grams as a quarter kilogram', () => {
    expect(displayedAmountIn(250, 'kilogram', 'de')).toEqual({
      count: 0.25,
      formattedCount: '0,25',
      entryUnit: 'kilogram',
    })
  })

  it('writes 1500 grams in grams when grams were chosen', () => {
    expect(displayedAmountIn(1500, 'gram', 'en')).toEqual({
      count: 1500,
      formattedCount: '1,500',
      entryUnit: 'gram',
    })
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
