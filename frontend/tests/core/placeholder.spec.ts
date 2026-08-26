import { describe, expect, it } from 'vitest'
import { scaffoldingIdentity } from '../../src/core/placeholder'

describe('scaffoldingIdentity', () => {
  it('returns the value it was given', () => {
    expect(scaffoldingIdentity(42)).toBe(42)
  })
})
