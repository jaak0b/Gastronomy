import { describe, expect, it } from 'vitest'
import { formatEuroInput, parseEuroInput } from '../../src/core/money'

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
