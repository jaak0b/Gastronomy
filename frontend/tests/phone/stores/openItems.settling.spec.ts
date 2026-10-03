import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { SEND_TIMEOUT_MS } from '../../../src/phone/core/sendTimeout'
import { useOpenItemsStore } from '../../../src/phone/stores/openItems'
import { TOKEN_STORAGE_KEY, useSessionStore } from '../../../src/shared/stores/session'
import { stubLaptop, answer, neverAnswers } from '../../support/laptop'
import { OPEN_LIST, EMPTY_LIST, SETTLED, storeWithTheOpenList } from './openItemsFixture'

vi.mock('@microsoft/signalr', async () => (await import('../../support/hubConnection')).signalrModuleFake())

describe('settling what the waiter ticked', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('sends the ticked items with the amount paid and clears the selection', async () => {
    const { openItems, laptop } = await storeWithTheOpenList([answer(SETTLED), answer(EMPTY_LIST)])
    openItems.toggleItem('item-1')

    await openItems.settle(350, null, 'cash')

    expect(laptop.calls[1]).toEqual({
      url: '/api/open-items/settle',
      method: 'POST',
      body: {
        lines: [{ orderItemId: 'item-1', paidPriceCents: 350, paymentNotice: null }],
        paymentMethod: 'cash',
      },
    })
    expect(openItems.selectedItemIds).toEqual([])
    expect(openItems.notice).toBeNull()
  })

  it('sends card as the way the table paid when the waiter settles by card', async () => {
    const { openItems, laptop } = await storeWithTheOpenList([answer(SETTLED), answer(EMPTY_LIST)])
    openItems.toggleItem('item-1')

    await openItems.settle(350, null, 'card')

    expect(laptop.calls[1].body).toEqual({
      lines: [{ orderItemId: 'item-1', paidPriceCents: 350, paymentNotice: null }],
      paymentMethod: 'card',
    })
  })

  it('carries the typed reason when the table pays less than it owes', async () => {
    const { openItems, laptop } = await storeWithTheOpenList([answer(SETTLED), answer(EMPTY_LIST)])
    openItems.toggleItem('item-1')

    await openItems.settle(0, 'Essen fuer die Kapelle', 'none')

    expect(laptop.calls[1]).toEqual({
      url: '/api/open-items/settle',
      method: 'POST',
      body: {
        lines: [
          { orderItemId: 'item-1', paidPriceCents: 0, paymentNotice: 'Essen fuer die Kapelle' },
        ],
        paymentMethod: 'none',
      },
    })
  })

  it('names the amount to hand back for the items another phone had already taken', async () => {
    const { openItems } = await storeWithTheOpenList([
      answer({
        settledOrderItemIds: ['item-1'],
        reappliedOrderItemIds: [],
        alreadySettledByOthersOrderItemIds: ['item-2'],
      }),
      answer(EMPTY_LIST),
    ])
    openItems.toggleItem('item-1')
    openItems.toggleItem('item-2')

    await openItems.settle(700, null, 'cash')

    expect(openItems.notice).toEqual({
      key: 'phone.openItems.messages.someWereAlreadySettled',
      parameters: { count: 1, amount: '3,50 €' },
      count: 1,
    })
  })

  it('shows the laptop wording when the laptop refuses, so no raw key reaches the phone', async () => {
    const { openItems } = await storeWithTheOpenList([
      answer(
        {
          code: 'ValidationFailed',
          messageKey: 'errors.settlement.unknownItem',
          parameters: {},
          details: null,
        },
        400,
      ),
      answer(OPEN_LIST),
    ])
    openItems.toggleItem('item-1')

    const outcome = await openItems.settle(200, '   ', 'cash')

    expect(outcome).toBe('refused')
    expect(openItems.notice?.key).toBe('errors.settlement.unknownItem')
  })

  it('stops waiting after ten seconds and claims nothing about what was settled', async () => {
    vi.useFakeTimers()
    try {
      stubLaptop().answersEverythingElse(neverAnswers())
      localStorage.setItem(TOKEN_STORAGE_KEY, 'lookup.token-here')
      useSessionStore().deviceToken = 'token-here'
      const openItems = useOpenItemsStore()
      openItems.tables = OPEN_LIST.tables
      openItems.toggleItem('item-1')

      const settling = openItems.settle(350, null, 'cash')
      await vi.advanceTimersByTimeAsync(SEND_TIMEOUT_MS)

      expect(await settling).toBe('answerNeverCame')
      expect(openItems.notice?.key).toBe('phone.openItems.errors.settleAnswerNeverCame')
      expect(openItems.selectedItemIds).toEqual(['item-1'])
      expect(openItems.isSettling).toBe(false)
    } finally {
      vi.useRealTimers()
    }
  })

  it('claims nothing about what was settled when the laptop was not reached at all', async () => {
    const { openItems } = await storeWithTheOpenList([
      () => {
        throw new TypeError('the laptop cannot be reached')
      },
    ])
    openItems.toggleItem('item-1')

    await openItems.settle(350, null, 'cash')

    expect(openItems.notice?.key).toBe('phone.openItems.errors.settleAnswerNeverCame')
    expect(openItems.selectedItemIds).toEqual(['item-1'])
  })

  it('keeps saying what a refusal from the laptop said, because a refusal is knowledge', async () => {
    const { openItems } = await storeWithTheOpenList([
      answer(
        {
          code: 'ValidationFailed',
          messageKey: 'errors.settlement.cannotBeProcessed',
          parameters: {},
          details: null,
        },
        400,
      ),
      answer(OPEN_LIST),
    ])
    openItems.toggleItem('item-1')

    await openItems.settle(200, null, 'cash')

    expect(openItems.notice?.key).toBe('errors.settlement.cannotBeProcessed')
  })
})
