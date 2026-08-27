import { describe, expect, it } from 'vitest'
import { isTableLabelValid, suggestionMatches } from '../../src/core/tableLabel'

describe('isTableLabelValid', () => {
  it('accepts a table the server typed', () => {
    const valid = isTableLabelValid('Tisch 12')

    expect(valid).toBe(true)
  })

  it('refuses an empty table field', () => {
    const valid = isTableLabelValid('')

    expect(valid).toBe(false)
  })

  it('refuses a table field holding only spaces', () => {
    const valid = isTableLabelValid('   ')

    expect(valid).toBe(false)
  })

  it('accepts a table named without a number, because tables are free text', () => {
    const valid = isTableLabelValid('Zelt hinten')

    expect(valid).toBe(true)
  })
})

describe('suggestionMatches', () => {
  const suggestions = ['Tisch 1', 'Tisch 2', 'Theke', 'Zelt hinten']

  it('offers every suggestion while the field is still empty', () => {
    const matches = suggestionMatches('', suggestions)

    expect(matches).toEqual(['Tisch 1', 'Tisch 2', 'Theke', 'Zelt hinten'])
  })

  it('keeps the order the admin configured rather than putting the exact match first', () => {
    const matches = suggestionMatches('Tisch 1', ['Tisch 12', 'Theke', 'Tisch 1'])

    expect(matches).toEqual(['Tisch 12', 'Tisch 1'])
  })

  it('matches without caring about capitals, because a phone keyboard supplies them', () => {
    const matches = suggestionMatches('theke', suggestions)

    expect(matches).toEqual(['Theke'])
  })

  it('matches anywhere in the suggestion rather than only at its start', () => {
    const matches = suggestionMatches('hinten', suggestions)

    expect(matches).toEqual(['Zelt hinten'])
  })

  it('offers nothing when no suggestion matches what was typed', () => {
    const matches = suggestionMatches('Balkon', suggestions)

    expect(matches).toEqual([])
  })
})
