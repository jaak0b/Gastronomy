import { describe, expect, it } from 'vitest'
import { combineReleases } from '../../../src/shared/core/combineReleases'

describe('several releases combined into one', () => {
  it('runs every release when the combined one runs', () => {
    const released: string[] = []

    const releaseAll = combineReleases(
      () => released.push('categories'),
      () => released.push('items'),
    )
    releaseAll()

    expect(released).toEqual(['categories', 'items'])
  })

  it('runs nothing until the combined release runs', () => {
    const released: string[] = []

    combineReleases(() => released.push('categories'))

    expect(released).toEqual([])
  })
})
