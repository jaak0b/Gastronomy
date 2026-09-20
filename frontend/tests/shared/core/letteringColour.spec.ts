import { describe, expect, it } from 'vitest'
import { letteringColourOn } from '../../src/core/letteringColour'

describe('the lettering on a category colour', () => {
  it('is black on a bright colour', () => {
    expect(letteringColourOn('#FFEB3B')).toBe('#000000')
  })

  it('is white on a dark colour', () => {
    expect(letteringColourOn('#C62828')).toBe('#FFFFFF')
  })

  it('is white on black', () => {
    expect(letteringColourOn('#000000')).toBe('#FFFFFF')
  })

  it('is black on white', () => {
    expect(letteringColourOn('#FFFFFF')).toBe('#000000')
  })

  it('reads a colour written in small letters the same way', () => {
    expect(letteringColourOn('#c62828')).toBe(letteringColourOn('#C62828'))
  })

  it('weighs green heavier than blue, because the eye does', () => {
    expect(letteringColourOn('#00FF00')).toBe('#000000')
    expect(letteringColourOn('#0000FF')).toBe('#FFFFFF')
  })

  it('is black when the colour cannot be read, so the name stays legible on the page', () => {
    expect(letteringColourOn('rot')).toBe('#000000')
    expect(letteringColourOn('#FFF')).toBe('#000000')
    expect(letteringColourOn('')).toBe('#000000')
  })
})
