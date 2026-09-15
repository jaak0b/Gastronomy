import { describe, expect, it } from 'vitest'
import { adminFailed, adminOk, refusalFrom } from '../../src/core/adminActionResult'
import { adminMessage } from '../../src/core/adminErrorMessage'

describe('unwrapping an admin action result', () => {
  it('leaves nothing behind when the laptop accepted the action', () => {
    expect(refusalFrom(adminOk(null))).toBeNull()
  })

  it('carries the message the laptop refused with', () => {
    const message = adminMessage('admin.actionFailed')

    expect(refusalFrom(adminFailed(message))).toBe(message)
  })
})
