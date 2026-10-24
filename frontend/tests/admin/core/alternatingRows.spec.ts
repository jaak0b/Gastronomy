import { describe, expect, it } from 'vitest'
import { numberRowsAcrossGroups } from '../../../src/admin/core/alternatingRows'

function positionsOf(groups: { items: { id: string }[] }[]): [string, number][] {
  return numberRowsAcrossGroups(groups).flatMap((group) =>
    group.rows.map(({ item, position }): [string, number] => [item.id, position]),
  )
}

describe('the rows numbered so every second one can be tinted', () => {
  it('numbers the rows of one group from the top', () => {
    expect(positionsOf([{ items: [{ id: 'a' }, { id: 'b' }, { id: 'c' }] }])).toEqual([
      ['a', 0],
      ['b', 1],
      ['c', 2],
    ])
  })

  it('counts on through the next group instead of starting over', () => {
    expect(positionsOf([{ items: [{ id: 'a' }] }, { items: [{ id: 'b' }, { id: 'c' }] }])).toEqual([
      ['a', 0],
      ['b', 1],
      ['c', 2],
    ])
  })

  it('keeps everything else the group carries', () => {
    const numbered = numberRowsAcrossGroups([{ name: 'Speisen', items: [{ id: 'a' }] }])

    expect(numbered.map((group) => group.name)).toEqual(['Speisen'])
  })

  it('numbers nothing when there are no rows', () => {
    expect(positionsOf([])).toEqual([])
  })
})
