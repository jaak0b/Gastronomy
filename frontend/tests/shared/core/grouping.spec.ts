import { describe, expect, it } from 'vitest'
import { groupByCategory } from '../../src/core/grouping'

interface Category {
  categoryId: string
  name: string
}

interface Row {
  name: string
  categoryId: string
}

const DRINKS: Category = { categoryId: 'category-drinks', name: 'Getränke' }
const FOOD: Category = { categoryId: 'category-food', name: 'Speisen' }

function group(categories: Category[], rows: Row[]) {
  return groupByCategory(
    categories,
    rows,
    (category) => category.categoryId,
    (row) => row.categoryId,
    (row) => row.name,
  )
}

describe('groupByCategory', () => {
  it('puts every item under the category it belongs to', () => {
    const grouped = group(
      [DRINKS, FOOD],
      [
        { name: 'Wasser', categoryId: 'category-drinks' },
        { name: 'Schnitzel', categoryId: 'category-food' },
        { name: 'Bier', categoryId: 'category-drinks' },
      ],
    )

    expect(grouped.map((entry) => entry.category)).toEqual([DRINKS, FOOD])
    expect(grouped[0].items.map((row) => row.name)).toEqual(['Bier', 'Wasser'])
    expect(grouped[1].items.map((row) => row.name)).toEqual(['Schnitzel'])
  })

  it('keeps the order the categories were handed over in', () => {
    const grouped = group(
      [FOOD, DRINKS],
      [
        { name: 'Bier', categoryId: 'category-drinks' },
        { name: 'Schnitzel', categoryId: 'category-food' },
      ],
    )

    expect(grouped.map((entry) => entry.category.name)).toEqual(['Speisen', 'Getränke'])
  })

  it('keeps a category that holds no items, so it can still be worked on', () => {
    const grouped = group([DRINKS, FOOD], [{ name: 'Bier', categoryId: 'category-drinks' }])

    expect(grouped.map((entry) => entry.category.name)).toEqual(['Getränke', 'Speisen'])
    expect(grouped[1].items).toEqual([])
  })

  it('leaves out an item whose category is not on the list', () => {
    const grouped = group(
      [DRINKS],
      [
        { name: 'Bier', categoryId: 'category-drinks' },
        { name: 'Schnitzel', categoryId: 'category-food' },
      ],
    )

    expect(grouped[0].items.map((row) => row.name)).toEqual(['Bier'])
  })

  it('returns nothing when there are no categories', () => {
    expect(group([], [{ name: 'Bier', categoryId: 'category-drinks' }])).toEqual([])
  })
})
