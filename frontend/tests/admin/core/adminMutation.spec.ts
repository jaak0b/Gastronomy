import { describe, expect, it } from 'vitest'
import type { ApiResult } from '../../../src/shared/api/client'
import { adminFailureFrom, reloadOrFailureOf } from '../../../src/admin/core/adminMutation'
import { GENERIC_ADMIN_ERROR_KEY } from '../../../src/admin/core/adminErrorMessage'

const ACCEPTED: ApiResult<null> = { kind: 'ok', status: 200, data: null }

const REFUSED: ApiResult<null> = {
  kind: 'error',
  status: 422,
  body: { code: 'stationHasOpenOrders', messageKey: 'common.errors.actionFailed', parameters: {}, details: null },
  raw: null,
}

const UNREACHABLE: ApiResult<null> = { kind: 'unreachable' }

describe('turning the laptop answer to a change into something the screen can show', () => {
  it('reloads the list once the laptop accepted the change', async () => {
    let reloads = 0

    const reported = await reloadOrFailureOf(ACCEPTED, async () => {
      reloads += 1
    })

    expect(reported.kind).toBe('ok')
    expect(reloads).toBe(1)
  })

  it('hands back what the laptop answered once it accepted the change', async () => {
    const created: ApiResult<{ areaId: string }> = { kind: 'ok', status: 201, data: { areaId: 'area-halle' } }

    const reported = await reloadOrFailureOf(created, async () => undefined)

    expect(reported).toEqual({ kind: 'ok', value: { areaId: 'area-halle' } })
  })

  it('carries the laptop refusal and leaves the list alone', async () => {
    let reloads = 0

    const reported = await reloadOrFailureOf(REFUSED, async () => {
      reloads += 1
    })

    expect(reported).toEqual({
      kind: 'failed',
      message: { key: 'common.errors.actionFailed', parameters: {}, count: null },
    })
    expect(reloads).toBe(0)
  })

  it('falls back to the general message when the laptop could not be reached', () => {
    const failure = adminFailureFrom(UNREACHABLE)

    expect(failure).toEqual({
      kind: 'failed',
      message: { key: GENERIC_ADMIN_ERROR_KEY, parameters: {}, count: null },
    })
  })
})
