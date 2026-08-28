import { describe, expect, it } from 'vitest'
import { isTableNameValid } from '../../src/core/tableName'

describe('isTableNameValid', () => {
  it('accepts a table the server typed', () => {
    const valid = isTableNameValid('Tisch 12')

    expect(valid).toBe(true)
  })

  it('refuses an empty table field', () => {
    const valid = isTableNameValid('')

    expect(valid).toBe(false)
  })

  it('refuses a table field holding only spaces', () => {
    const valid = isTableNameValid('   ')

    expect(valid).toBe(false)
  })

  it('accepts a table named without a number, because tables are free text', () => {
    const valid = isTableNameValid('Zelt hinten')

    expect(valid).toBe(true)
  })
})

