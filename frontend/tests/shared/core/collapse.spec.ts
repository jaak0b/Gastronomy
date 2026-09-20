import { describe, expect, it } from 'vitest'
import { collapseLines } from '../../src/core/collapse'

interface Line {
  name: string
  note: string | null
}

function collapse(...lines: Line[]) {
  return collapseLines(
    lines,
    (line) => line.name,
    (line) => line.note,
  )
}

describe('collapseLines', () => {
  it('counts identical positions as one line', () => {
    const collapsed = collapse(
      { name: 'Bier', note: null },
      { name: 'Bier', note: null },
      { name: 'Bier', note: null },
    )

    expect(collapsed).toHaveLength(1)
    expect(collapsed[0].quantity).toBe(3)
    expect(collapsed[0].line.name).toBe('Bier')
  })

  it('keeps a position with a note on its own line', () => {
    const collapsed = collapse(
      { name: 'Bier', note: null },
      { name: 'Bier', note: 'ohne Schaum' },
      { name: 'Bier', note: null },
    )

    expect(collapsed).toHaveLength(2)
    expect(collapsed[0].quantity).toBe(2)
    expect(collapsed[1].quantity).toBe(1)
    expect(collapsed[1].line.note).toBe('ohne Schaum')
  })

  it('counts positions carrying the same note together', () => {
    const collapsed = collapse(
      { name: 'Bier', note: 'ohne Schaum' },
      { name: 'Bier', note: 'ohne Schaum' },
    )

    expect(collapsed).toHaveLength(1)
    expect(collapsed[0].quantity).toBe(2)
  })

  it('keeps the order the positions were added in', () => {
    const collapsed = collapse(
      { name: 'Schnitzel', note: null },
      { name: 'Bier', note: null },
      { name: 'Schnitzel', note: null },
    )

    expect(collapsed.map((entry) => entry.line.name)).toEqual(['Schnitzel', 'Bier'])
    expect(collapsed[0].quantity).toBe(2)
  })

  it('treats an empty note and no note as the same line', () => {
    const collapsed = collapse({ name: 'Bier', note: null }, { name: 'Bier', note: '' })

    expect(collapsed).toHaveLength(1)
    expect(collapsed[0].quantity).toBe(2)
  })

  it('returns nothing for no positions', () => {
    expect(collapse()).toEqual([])
  })
})
