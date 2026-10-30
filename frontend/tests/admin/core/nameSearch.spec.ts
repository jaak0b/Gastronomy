import { describe, expect, it } from 'vitest'
import { nameMatchesTheSearch } from '../../../src/admin/core/nameSearch'

describe('a name searched for a typed text', () => {
  it.each([
    ['the whole name', 'Milch', 'Milch'],
    ['the start of the name', 'Milch', 'Mil'],
    ['a part inside the name', 'Brötchen', 'röt'],
    ['the name in other case', 'Milch', 'mILCH'],
    ['an umlaut in other case', 'Äpfel', 'äpf'],
    ['an empty search', 'Milch', ''],
    ['the name typed with a space after it', 'Milch', 'Milch '],
    ['a part typed with spaces around it', 'Brötchen', '  röt '],
  ])('matches %s', (_description, name, search) => {
    expect(nameMatchesTheSearch(name, search)).toBe(true)
  })

  it.each([
    ['text the name does not hold', 'Milch', 'Sahne'],
    ['the letters of the name in another order', 'Milch', 'hclim'],
    ['a space inside the typed text that the name does not hold', 'Milch', 'Mil ch'],
  ])('does not match %s', (_description, name, search) => {
    expect(nameMatchesTheSearch(name, search)).toBe(false)
  })
})
