import { describe, expect, it } from 'vitest'
import { nameFontSizeKeepingWordsWhole } from '../../../src/shared/core/shapeNameFit'

function sevenUnitsPerLetter(word: string): number {
  return word.length * 7
}

describe('the size of the name inside a table or a station on a plan', () => {
  it('stays at its largest when every word fits across the table', () => {
    expect(nameFontSizeKeepingWordsWhole('Tisch 12', 72, sevenUnitsPerLetter)).toBe(14)
  })

  it('stays at its largest for a long name of short words, which wraps between them', () => {
    expect(nameFontSizeKeepingWordsWhole('Langer Tisch am Fenster', 82, sevenUnitsPerLetter)).toBe(14)
  })

  it('shrinks until a word wider than the table fits across it', () => {
    expect(nameFontSizeKeepingWordsWhole('Abcd', 17, sevenUnitsPerLetter)).toBe(8.5)
  })

  it('shrinks for the widest word of a name with several words', () => {
    expect(nameFontSizeKeepingWordsWhole('Biergarten Nord', 50, sevenUnitsPerLetter)).toBe(10)
  })
})
