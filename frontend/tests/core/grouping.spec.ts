import { describe, expect, it } from 'vitest'
import { groupByCategory } from '../../src/core/grouping'

interface Row {
  name: string
  categoryName: string
}

function group(rows: Row[]) {
  return groupByCategory(
    rows,
    (row) => row.categoryName,
    (row) => row.name,
  )
}

describe('groupByCategory', () => {
  it('puts every item under the heading of its category', () => {
    const grouped = group([
      { name: 'Wasser', categoryName: 'Getränke' },
      { name: 'Schnitzel', categoryName: 'Speisen' },
      { name: 'Bier', categoryName: 'Getränke' },
    ])

    expect(grouped.map((entry) => entry.name)).toEqual(['Getränke', 'Speisen'])
    expect(grouped[0].items.map((row) => row.name)).toEqual(['Bier', 'Wasser'])
    expect(grouped[1].items.map((row) => row.name)).toEqual(['Schnitzel'])
  })

  it('sorts the categories by name rather than by the order they arrived in', () => {
    const grouped = group([
      { name: 'Schnitzel', categoryName: 'Speisen' },
      { name: 'Bier', categoryName: 'Getränke' },
    ])

    expect(grouped.map((entry) => entry.name)).toEqual(['Getränke', 'Speisen'])
  })

  it('returns nothing for no items', () => {
    expect(group([])).toEqual([])
  })
})
