import { describe, expect, it } from 'vitest'
import { nameFontSizeFittingEveryLineInside, nameFontSizeFittingEveryWordAcross } from '../../../src/shared/core/shapeNameFit'

function sevenUnitsPerLetter(word: string): number {
  return word.length * 7
}

describe('the size of a name in the table plan editor, where every word must fit across its shape', () => {
  it('stays at its largest when every word fits across the table', () => {
    expect(nameFontSizeFittingEveryWordAcross('Tisch 12', 72, sevenUnitsPerLetter)).toBe(14)
  })

  it('stays at its largest for a long name of short words, which wraps between them', () => {
    expect(nameFontSizeFittingEveryWordAcross('Langer Tisch am Fenster', 82, sevenUnitsPerLetter)).toBe(14)
  })

  it('shrinks until a word wider than the table fits across it', () => {
    expect(nameFontSizeFittingEveryWordAcross('Abcd', 17, sevenUnitsPerLetter)).toBe(8.5)
  })

  it('shrinks for the widest word of a name with several words', () => {
    expect(nameFontSizeFittingEveryWordAcross('Biergarten Nord', 50, sevenUnitsPerLetter)).toBe(10)
  })
})

describe('the size of a name on the waiter\'s plan, where every line must fit inside its shape', () => {
  it('stays at its largest while every line of a wrapped name fits the height', () => {
    expect(nameFontSizeFittingEveryLineInside('Tisch Mitte', { width: 44, height: 28 }, sevenUnitsPerLetter)).toBe(14)
  })

  it('shrinks until both lines of a wrapped name fit the height', () => {
    expect(nameFontSizeFittingEveryLineInside('Tisch Mitte', { width: 44, height: 24 }, sevenUnitsPerLetter)).toBe(12)
  })

  it('shrinks a name onto one line when that keeps it larger than two smaller lines', () => {
    expect(nameFontSizeFittingEveryLineInside('Tisch 1', { width: 44, height: 20 }, sevenUnitsPerLetter)).toBe(12.5)
  })

  it('keeps a name that fits on one line at its size, since a single line is always drawn whole', () => {
    expect(nameFontSizeFittingEveryLineInside('Tisch 12', { width: 72, height: 10 }, sevenUnitsPerLetter)).toBe(14)
  })

  it('starts from the size at which every word fits across', () => {
    expect(nameFontSizeFittingEveryLineInside('Abcd', { width: 17, height: 40 }, sevenUnitsPerLetter)).toBe(8.5)
  })
})
