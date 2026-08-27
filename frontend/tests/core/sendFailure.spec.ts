import { describe, expect, it } from 'vitest'
import { messageForSendFailure } from '../../src/core/sendFailure'

describe('messageForSendFailure, what went wrong', () => {
  it('tells the server to send again when the laptop could not be reached', () => {
    const message = messageForSendFailure({ kind: 'unreachable' }, 1)

    expect(message.key).toBe('review.sendFailed')
  })

  it('tells the server to wait a moment when the laptop has too many requests at once', () => {
    const message = messageForSendFailure({ kind: 'error', status: 429, body: null }, 1)

    expect(message.key).toBe('review.tooManyRequests')
  })

  it('names the saving problem when the laptop could not store the order', () => {
    const message = messageForSendFailure({ kind: 'error', status: 503, body: null }, 1)

    expect(message.key).toBe('review.sendFailedDatabase')
  })

  it('names the saving problem when the laptop answered with an internal error', () => {
    const message = messageForSendFailure({ kind: 'error', status: 500, body: null }, 1)

    expect(message.key).toBe('review.sendFailedDatabase')
  })

  it('warns that the order was already sent when the laptop reports a conflict', () => {
    const message = messageForSendFailure({ kind: 'error', status: 409, body: null }, 1)

    expect(message.key).toBe('review.duplicateRisk')
  })

  it('falls back to the plain failure for a rejection it does not recognise', () => {
    const message = messageForSendFailure({ kind: 'error', status: 422, body: null }, 1)

    expect(message.key).toBe('review.sendFailed')
  })
})

describe('messageForSendFailure, the fall back to paper', () => {
  it('says nothing about paper after a single failure', () => {
    const message = messageForSendFailure({ kind: 'unreachable' }, 1)

    expect(message.paperFallbackKey).toBeNull()
  })

  it('tells the server to write the order down after a second failure', () => {
    const message = messageForSendFailure({ kind: 'unreachable' }, 2)

    expect(message.paperFallbackKey).toBe('review.sendFailedAgain')
  })

  it('keeps telling the server to write the order down after further failures', () => {
    const message = messageForSendFailure({ kind: 'unreachable' }, 5)

    expect(message.paperFallbackKey).toBe('review.sendFailedAgain')
  })
})
