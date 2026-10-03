import { describe, expect, it } from 'vitest'
import { idsOfEverySecondRow } from '../../../src/admin/core/alternatingRows'

describe('the rows tinted to guide the eye', () => {
  it('tints the second and fourth row of one group', () => {
    const groups = [{ items: [{ id: 'a' }, { id: 'b' }, { id: 'c' }, { id: 'd' }] }]

    expect([...idsOfEverySecondRow(groups, (row) => row.id)]).toEqual(['b', 'd'])
  })

  it('counts on through the next group instead of starting over', () => {
    const groups = [{ items: [{ id: 'a' }] }, { items: [{ id: 'b' }, { id: 'c' }] }]

    expect([...idsOfEverySecondRow(groups, (row) => row.id)]).toEqual(['b'])
  })

  it('tints nothing when there are no rows', () => {
    expect([...idsOfEverySecondRow([], (row: { id: string }) => row.id)]).toEqual([])
  })
})
