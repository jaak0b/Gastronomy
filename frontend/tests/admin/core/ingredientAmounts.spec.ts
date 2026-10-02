import { describe, expect, it } from 'vitest'
import {
  amountInputFor,
  amountInputSwitchedTo,
  entryUnitsFor,
  formatAmount,
  parseAmountInput,
  unitSymbolOf,
} from '../../../src/admin/core/ingredientAmounts'

function pieces(formattedCount: string): string {
  return `${formattedCount} pcs`
}

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
  it('is shown in kilograms from 1000 grams on, with a German comma', () => {
    expect(amountInputFor(1500, 'gram', 'de')).toEqual({ typed: '1,5', entryUnit: 'kilogram' })
  })

  it('is shown in litres from 1000 millilitres on, with an English point', () => {
    expect(amountInputFor(1250, 'millilitre', 'en')).toEqual({ typed: '1.25', entryUnit: 'litre' })
  })

  it('stays in grams below 1000', () => {
    expect(amountInputFor(999, 'gram', 'de')).toEqual({ typed: '999', entryUnit: 'gram' })
  })

  it('stays in pieces however many there are', () => {
    expect(amountInputFor(2000, 'piece', 'de')).toEqual({ typed: '2000', entryUnit: 'piece' })
  })

  it('is an empty field in the smaller unit when there is no amount', () => {
    expect(amountInputFor(null, 'millilitre', 'de')).toEqual({ typed: '', entryUnit: 'millilitre' })
  })
})

describe('an amount written on the screen', () => {
  it('names kilograms in German from 1000 grams on', () => {
    expect(formatAmount(1500, 'gram', 'de', pieces)).toBe('1,5 kg')
  })

  it('names kilograms in English from 1000 grams on', () => {
    expect(formatAmount(1500, 'gram', 'en', pieces)).toBe('1.5 kg')
  })

  it('names grams below 1000', () => {
    expect(formatAmount(999, 'gram', 'de', pieces)).toBe('999 g')
  })

  it('names litres in German from 1000 millilitres on', () => {
    expect(formatAmount(1250, 'millilitre', 'de', pieces)).toBe('1,25 l')
  })

  it('names millilitres in English below 1000', () => {
    expect(formatAmount(250, 'millilitre', 'en', pieces)).toBe('250 mL')
  })

  it('hands pieces with the number written in the device language to the caller', () => {
    expect(formatAmount(12000, 'piece', 'de', pieces)).toBe('12.000 pcs')
    expect(formatAmount(1.5, 'piece', 'en', pieces)).toBe('1.5 pcs')
  })
})

describe('the symbol on a unit toggle', () => {
  it('is the short unit in the device language', () => {
    expect(unitSymbolOf('kilogram', 'de')).toBe('kg')
    expect(unitSymbolOf('litre', 'de')).toBe('l')
    expect(unitSymbolOf('litre', 'en')).toBe('L')
    expect(unitSymbolOf('millilitre', 'en')).toBe('mL')
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
