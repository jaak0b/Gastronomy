import { describe, expect, it } from 'vitest'
import { canBeTypedIntoAEuroField, formatEuroInput, parseEuroInput } from '../../src/core/money'

describe('parseEuroInput, what an admin types', () => {
  it('reads a German price with a comma', () => {
    expect(parseEuroInput('3,50')).toBe(350)
  })

  it('reads the same price written with a dot', () => {
    expect(parseEuroInput('3.50')).toBe(350)
  })

  it('reads a whole euro amount with no decimals', () => {
    expect(parseEuroInput('4')).toBe(400)
  })

  it('reads a single decimal as tenths of a euro', () => {
    expect(parseEuroInput('3,5')).toBe(350)
  })

  it('reads a price under one euro', () => {
    expect(parseEuroInput('0,05')).toBe(5)
  })

  it('reads a free item as nothing owed', () => {
    expect(parseEuroInput('0')).toBe(0)
  })

  it('ignores spaces around what was typed', () => {
    expect(parseEuroInput('  3,50  ')).toBe(350)
  })
})

describe('parseEuroInput, what it refuses', () => {
  it('refuses an empty field', () => {
    expect(parseEuroInput('')).toBeNull()
  })

  it('refuses letters', () => {
    expect(parseEuroInput('drei Euro')).toBeNull()
  })

  it('refuses a negative price', () => {
    expect(parseEuroInput('-1,00')).toBeNull()
  })

  it('refuses more than two decimals, because a cent is the smallest coin', () => {
    expect(parseEuroInput('3,555')).toBeNull()
  })

  it('refuses a trailing separator', () => {
    expect(parseEuroInput('3,')).toBeNull()
  })

  it('refuses a missing whole part', () => {
    expect(parseEuroInput(',50')).toBeNull()
  })

  it('refuses a thousands separator, because no festival item costs that much', () => {
    expect(parseEuroInput('1.234,56')).toBeNull()
  })
})

describe('formatEuroInput, what the form shows back', () => {
  it('writes a German price with a comma and both decimals', () => {
    expect(formatEuroInput(350, 'de')).toBe('3,50')
  })

  it('writes an English price with a dot and both decimals', () => {
    expect(formatEuroInput(350, 'en')).toBe('3.50')
  })

  it('keeps both decimals on a whole euro amount', () => {
    expect(formatEuroInput(400, 'de')).toBe('4,00')
  })

  it('writes a price under one euro', () => {
    expect(formatEuroInput(5, 'de')).toBe('0,05')
  })

  it('leaves a new item empty rather than showing a price of nothing', () => {
    expect(formatEuroInput(null, 'de')).toBe('')
  })
})

describe('the amount a table hands over on the open items screen', () => {
  it('reads a part payment written the German way', () => {
    expect(parseEuroInput('20,50')).toBe(2050)
  })

  it('reads a part payment written the English way', () => {
    expect(parseEuroInput('20.50')).toBe(2050)
  })

  it('reads a whole tab typed without decimals', () => {
    expect(parseEuroInput('200')).toBe(20000)
  })

  it('reads a table that hands over nothing', () => {
    expect(parseEuroInput('0')).toBe(0)
  })

  it('offers a German tab back with a comma, ready to be overwritten', () => {
    expect(formatEuroInput(20000, 'de')).toBe('200,00')
  })

  it('offers an English tab back with a dot, ready to be overwritten', () => {
    expect(formatEuroInput(20000, 'en')).toBe('200.00')
  })
})

describe('canBeTypedIntoAEuroField, what the amount field lets a waiter type', () => {
  it('lets the field stand empty while the waiter clears the amount offered to them', () => {
    expect(canBeTypedIntoAEuroField('')).toBe(true)
  })

  it('lets a whole euro amount stand before any cents are typed', () => {
    expect(canBeTypedIntoAEuroField('5')).toBe(true)
  })

  it('lets the comma stand while the cents are still being typed', () => {
    expect(canBeTypedIntoAEuroField('5,')).toBe(true)
  })

  it('lets the dot stand while the cents are still being typed', () => {
    expect(canBeTypedIntoAEuroField('5.')).toBe(true)
  })

  it('lets a single cent digit stand on the way to two of them', () => {
    expect(canBeTypedIntoAEuroField('5,0')).toBe(true)
  })

  it('takes the finished amount written with a comma', () => {
    expect(canBeTypedIntoAEuroField('5,00')).toBe(true)
  })

  it('takes the finished amount written with a dot', () => {
    expect(canBeTypedIntoAEuroField('5.00')).toBe(true)
  })

  it('refuses a third decimal, because a cent is the smallest coin', () => {
    expect(canBeTypedIntoAEuroField('5,000')).toBe(false)
  })

  it('refuses a euro sign, because the field already stands for euros', () => {
    expect(canBeTypedIntoAEuroField('5€')).toBe(false)
  })

  it('refuses letters', () => {
    expect(canBeTypedIntoAEuroField('fünf')).toBe(false)
  })

  it('refuses a space', () => {
    expect(canBeTypedIntoAEuroField('5 ')).toBe(false)
  })

  it('refuses a second separator', () => {
    expect(canBeTypedIntoAEuroField('5,0,0')).toBe(false)
  })

  it('refuses a thousands separator, because no table hands over that much', () => {
    expect(canBeTypedIntoAEuroField('1.234,56')).toBe(false)
  })

  it('refuses a minus sign, because a table never hands over less than nothing', () => {
    expect(canBeTypedIntoAEuroField('-5')).toBe(false)
  })

  it('refuses a separator before the first digit, because that amount can never be read', () => {
    expect(canBeTypedIntoAEuroField(',50')).toBe(false)
  })
})
