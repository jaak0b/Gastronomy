import { describe, expect, it } from 'vitest'
import { adminBlockingConditions, adminErrorMessage } from '../../src/core/adminErrorMessage'

describe('adminErrorMessage, the key the laptop actually sent', () => {
  it('renders an open-slips refusal as an open-slips message', () => {
    const message = adminErrorMessage({
      code: 'Conflict',
      messageKey: 'admin.locations.openTickets',
      parameters: { count: 3 },
      details: null,
    })

    expect(message).toEqual({
      key: 'admin.locations.openTickets',
      parameters: { count: 3 },
      count: 3,
    })
  })

  it('renders an orphaned-items refusal as its own message, never as open slips', () => {
    const message = adminErrorMessage({
      code: 'Conflict',
      messageKey: 'admin.itemsWouldHaveNoStation',
      parameters: { count: 2 },
      details: null,
    })

    expect(message.key).toBe('admin.itemsWouldHaveNoStation')
  })

  it('renders the missing printer connection as its own message', () => {
    const message = adminErrorMessage({
      code: 'Conflict',
      messageKey: 'admin.stationHasNoPrinterWorker',
      parameters: {},
      details: null,
    })

    expect(message.key).toBe('admin.stationHasNoPrinterWorker')
  })

  it('carries the count so the sentence can take its singular form', () => {
    const message = adminErrorMessage({
      code: 'Conflict',
      messageKey: 'admin.itemsWouldHaveNoStation',
      parameters: { count: 1 },
      details: null,
    })

    expect(message.count).toBe(1)
  })

  it('carries no count for a message that has no number in it', () => {
    const message = adminErrorMessage({
      code: 'Conflict',
      messageKey: 'admin.stationHasNoPrinterWorker',
      parameters: {},
      details: null,
    })

    expect(message.count).toBeNull()
  })
})

describe('adminErrorMessage, a key this app does not know', () => {
  it('falls back to the general message rather than mislabelling the refusal', () => {
    const message = adminErrorMessage({
      code: 'Conflict',
      messageKey: 'admin.somethingAddedLater',
      parameters: { count: 4 },
      details: null,
    })

    expect(message.key).toBe('admin.loadFailed')
  })

  it('drops the parameters of a key it cannot render', () => {
    const message = adminErrorMessage({
      code: 'Conflict',
      messageKey: 'admin.somethingAddedLater',
      parameters: { count: 4 },
      details: null,
    })

    expect(message.parameters).toEqual({})
  })

  it('falls back when the laptop sent no error body at all', () => {
    const message = adminErrorMessage(null)

    expect(message).toEqual({ key: 'admin.loadFailed', parameters: {}, count: null })
  })
})

