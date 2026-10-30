import { describe, expect, it } from 'vitest'
import { stripeClassesOf } from '../../../src/shared/core/stripedRows'

describe('the stripe of a row in a striped list', () => {
  it.each([0, 2, 4])('is the light tint alone at position %i', (position) => {
    expect(stripeClassesOf(position)).toEqual({ 'striped-row': true, 'striped-row-strong': false })
  })

  it.each([1, 3, 5])('is the light tint and the stronger one at position %i', (position) => {
    expect(stripeClassesOf(position)).toEqual({ 'striped-row': true, 'striped-row-strong': true })
  })
})
