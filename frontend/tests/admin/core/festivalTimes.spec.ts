import { describe, expect, it } from 'vitest'
import { localInputToUtcIso, utcIsoToLocalInput } from '../../../src/admin/core/festivalTimes'

describe('the moment the admin types', () => {
  it('travels as an ISO string in UTC ending in Z', () => {
    const iso = localInputToUtcIso('2026-07-18T17:00')

    expect(iso).toBe(new Date('2026-07-18T17:00').toISOString())
    expect(iso?.endsWith('Z')).toBe(true)
  })

  it('is nothing when the field is empty or unreadable', () => {
    expect(localInputToUtcIso('')).toBeNull()
    expect(localInputToUtcIso('   ')).toBeNull()
    expect(localInputToUtcIso('the eighteenth')).toBeNull()
  })

  it('comes back into the field as the same local moment', () => {
    const iso = localInputToUtcIso('2026-07-18T17:00') as string

    expect(utcIsoToLocalInput(iso)).toBe('2026-07-18T17:00')
  })

  it('leaves the field empty when the laptop sent something unreadable', () => {
    expect(utcIsoToLocalInput('not a moment')).toBe('')
  })
})

