import { describe, expect, it } from 'vitest'
import { adminFailed, adminOk, refusalFrom } from '../../../src/admin/core/adminActionResult'
import { adminErrorMessageForKey } from '../../../src/admin/core/adminErrorMessage'

describe('unwrapping an admin action result', () => {
  it('leaves nothing behind when the laptop accepted the action', () => {
    expect(refusalFrom(adminOk(null))).toBeNull()
  })

  it('carries the message the laptop refused with', () => {
    const message = adminErrorMessageForKey('admin.actionFailed')

    expect(refusalFrom(adminFailed(message))).toBe(message)
  })
})
