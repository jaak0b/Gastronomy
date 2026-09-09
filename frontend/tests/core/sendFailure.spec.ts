import { describe, expect, it } from 'vitest'
import {
  messageForAnInterruptedSend,
  messageForSendFailure,
} from '../../src/core/sendFailure'

describe('messageForSendFailure, what went wrong', () => {
  it('tells the server to send again when the laptop could not be reached', () => {
    const message = messageForSendFailure({ kind: 'unreachable' })

    expect(message.key).toBe('review.sendFailed')
  })

  it('tells the server to wait a moment when the laptop has too many requests at once', () => {
    const message = messageForSendFailure({ kind: 'error', status: 429, body: null })

    expect(message.key).toBe('session.tooManyRequests')
  })

  it('names the saving problem when the laptop could not store the order', () => {
    const message = messageForSendFailure({ kind: 'error', status: 503, body: null })

    expect(message.key).toBe('review.sendFailedDatabase')
  })

  it('names the saving problem when the laptop answered with an internal error', () => {
    const message = messageForSendFailure({ kind: 'error', status: 500, body: null })

    expect(message.key).toBe('review.sendFailedDatabase')
  })

  it('shows the reason the laptop gave when the laptop named one', () => {
    const message = messageForSendFailure(
      {
        kind: 'error',
        status: 422,
        body: {
          code: 'UnprocessableEntity',
          messageKey: 'order.unknownItem',
          parameters: {},
          details: null,
        },
      },
    )

    expect(message.key).toBe('order.unknownItem')
  })

  it('falls back to the status when the laptop answered without naming a reason', () => {
    const message = messageForSendFailure(
      {
        kind: 'error',
        status: 503,
        body: { code: 'DatabaseUnavailable', messageKey: '', parameters: {}, details: null },
      },
    )

    expect(message.key).toBe('review.sendFailedDatabase')
  })

  it('falls back to the plain failure for a rejection it does not recognise', () => {
    const message = messageForSendFailure({ kind: 'error', status: 422, body: null })

    expect(message.key).toBe('review.sendFailed')
  })
})

describe('messageForAnInterruptedSend', () => {
  it('says the attempt was cut off instead of naming a cause nobody can know', () => {
    const message = messageForAnInterruptedSend()

    expect(message.key).toBe('review.sendInterrupted')
  })
})
