import { describe, expect, it } from 'vitest'
import { adminBlockingConditions, adminErrorMessage } from '../../src/core/adminErrorMessage'

describe('adminErrorMessage, the key the laptop actually sent', () => {
  it('renders a refusal about unfinished orders as that message', () => {
    const message = adminErrorMessage({
      code: 'Conflict',
      messageKey: 'admin.stationHasUnfinishedItems',
      parameters: { count: 3 },
      details: null,
    })

    expect(message).toEqual({
      key: 'admin.stationHasUnfinishedItems',
      parameters: { count: 3 },
      count: 3,
    })
  })

  it('renders an orphaned-items refusal as its own message, never as unfinished orders', () => {
    const message = adminErrorMessage({
      code: 'Conflict',
      messageKey: 'admin.itemsWouldHaveNoStation',
      parameters: { count: 2 },
      details: null,
    })

    expect(message.key).toBe('admin.itemsWouldHaveNoStation')
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
      messageKey: 'admin.stationHasUnfinishedItems',
      parameters: {},
      details: null,
    })

    expect(message.count).toBeNull()
  })
})

describe('adminErrorMessage, a count the laptop wrote as text', () => {
  it('reads it as a number, because the laptop sends every parameter as text', () => {
    const message = adminErrorMessage({
      code: 'Conflict',
      messageKey: 'admin.itemsWouldHaveNoStation',
      parameters: { count: '2' },
      details: null,
    })

    expect(message.count).toBe(2)
  })

  it('carries no count when the text is not a number at all', () => {
    const message = adminErrorMessage({
      code: 'Conflict',
      messageKey: 'admin.itemsWouldHaveNoStation',
      parameters: { count: 'einige' },
      details: null,
    })

    expect(message.count).toBeNull()
  })

  it('carries no count for a fraction, because half an item cannot be counted', () => {
    const message = adminErrorMessage({
      code: 'Conflict',
      messageKey: 'admin.itemsWouldHaveNoStation',
      parameters: { count: '2.5' },
      details: null,
    })

    expect(message.count).toBeNull()
  })

  it('carries no count when the text is empty', () => {
    const message = adminErrorMessage({
      code: 'Conflict',
      messageKey: 'admin.itemsWouldHaveNoStation',
      parameters: { count: '' },
      details: null,
    })

    expect(message.count).toBeNull()
  })
})

describe('adminErrorMessage, a refusal the laptop worded itself', () => {
  it('renders a station that still has unfinished orders', () => {
    const message = adminErrorMessage({
      code: 'Conflict',
      messageKey: 'admin.stationHasUnfinishedItems',
      parameters: {},
      details: null,
    })

    expect(message.key).toBe('admin.stationHasUnfinishedItems')
  })

  it('renders a refused enrolment invitation', () => {
    const message = adminErrorMessage({
      code: 'ValidationFailed',
      messageKey: 'enrolment.atMostOneOwner',
      parameters: {},
      details: null,
    })

    expect(message.key).toBe('enrolment.atMostOneOwner')
  })
})

describe('adminErrorMessage, a key this app does not know', () => {
  it('says the action did not happen, rather than blaming the page load', () => {
    const message = adminErrorMessage({
      code: 'Conflict',
      messageKey: 'admin.somethingAddedLater',
      parameters: { count: 4 },
      details: null,
    })

    expect(message.key).toBe('admin.actionFailed')
  })

  it('falls back rather than mislabelling the refusal', () => {
    const message = adminErrorMessage({
      code: 'Conflict',
      messageKey: 'admin.somethingAddedLater',
      parameters: { count: 4 },
      details: null,
    })

    expect(message.key).not.toBe('admin.stations.openTickets')
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

    expect(message).toEqual({ key: 'admin.actionFailed', parameters: {}, count: null })
  })
})

