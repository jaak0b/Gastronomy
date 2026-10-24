import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { SEND_TIMEOUT_MS } from '../../../src/phone/core/sendTimeout'
import { useOpenItemsStore } from '../../../src/phone/stores/openItems'
import { stubLaptop, answer, neverAnswers } from '../../support/laptop'
import { SETTLED, TWO_BRATWURST, aWaiterPhone, storeShowingTisch12 } from './openItemsFixture'

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
    const { openItems, laptop } = await storeShowingTisch12([answer(SETTLED), answer(TWO_BRATWURST)], TWO_BRATWURST)
    openItems.toggleItem('item-1')

    await openItems.settle(350, null, 'cash')

    const { url, method, body } = laptop.calls[1]
    expect({ url, method, body }).toEqual({
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
    const { openItems, laptop } = await storeShowingTisch12([answer(SETTLED), answer(TWO_BRATWURST)], TWO_BRATWURST)
    openItems.toggleItem('item-1')

    await openItems.settle(350, null, 'card')

    expect(laptop.calls[1].body).toEqual({
      lines: [{ orderItemId: 'item-1', paidPriceCents: 350, paymentNotice: null }],
      paymentMethod: 'card',
    })
  })

  it('carries the typed reason when the table pays less than it owes', async () => {
    const { openItems, laptop } = await storeShowingTisch12([answer(SETTLED), answer(TWO_BRATWURST)], TWO_BRATWURST)
    openItems.toggleItem('item-1')

    await openItems.settle(0, 'Essen fuer die Kapelle', 'none')

    const { url, method, body } = laptop.calls[1]
    expect({ url, method, body }).toEqual({
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
    const { openItems } = await storeShowingTisch12([
      answer({
        settledOrderItemIds: ['item-1'],
        reappliedOrderItemIds: [],
        alreadySettledByOthersOrderItemIds: ['item-2'],
      }),
      answer(TWO_BRATWURST),
    ], TWO_BRATWURST)
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
    const { openItems } = await storeShowingTisch12([
      answer(
        {
          code: 'ValidationFailed',
          messageKey: 'errors.settlement.unknownItem',
          parameters: {},
          details: null,
        },
        400,
      ),
      answer(TWO_BRATWURST),
    ], TWO_BRATWURST)
    openItems.toggleItem('item-1')

    const outcome = await openItems.settle(200, '   ', 'cash')

    expect(outcome).toBe('refused')
    expect(openItems.notice?.key).toBe('errors.settlement.unknownItem')
  })

  it('stops waiting after ten seconds and claims nothing about what was settled', async () => {
    vi.useFakeTimers()
    try {
      stubLaptop().answersEverythingElse(neverAnswers())
      aWaiterPhone()
      const openItems = useOpenItemsStore()
      openItems.openTable('table-12')
      openItems.report = TWO_BRATWURST
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
    const { openItems } = await storeShowingTisch12(
      [
        () => {
          throw new TypeError('the laptop cannot be reached')
        },
      ],
      TWO_BRATWURST,
    )
    openItems.toggleItem('item-1')

    await openItems.settle(350, null, 'cash')

    expect(openItems.notice?.key).toBe('phone.openItems.errors.settleAnswerNeverCame')
    expect(openItems.selectedItemIds).toEqual(['item-1'])
  })

  it('keeps saying what a refusal from the laptop said, because a refusal is knowledge', async () => {
    const { openItems } = await storeShowingTisch12([
      answer(
        {
          code: 'ValidationFailed',
          messageKey: 'errors.settlement.cannotBeProcessed',
          parameters: {},
          details: null,
        },
        400,
      ),
      answer(TWO_BRATWURST),
    ], TWO_BRATWURST)
    openItems.toggleItem('item-1')

    await openItems.settle(200, null, 'cash')

    expect(openItems.notice?.key).toBe('errors.settlement.cannotBeProcessed')
  })
})

describe('a notice about settling one table', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  async function aNoticeToHandMoneyBackAtTisch12() {
    const { openItems } = await storeShowingTisch12(
      [
        answer({
          settledOrderItemIds: ['item-1'],
          reappliedOrderItemIds: [],
          alreadySettledByOthersOrderItemIds: ['item-2'],
        }),
        answer(TWO_BRATWURST),
      ],
      TWO_BRATWURST,
    )
    openItems.toggleItem('item-1')
    openItems.toggleItem('item-2')
    await openItems.settle(700, null, 'cash')
    return openItems
  }

  it('stays behind when the waiter leaves for another table', async () => {
    const openItems = await aNoticeToHandMoneyBackAtTisch12()

    openItems.closeTable()
    openItems.openTable('table-14')

    expect(openItems.notice).toBeNull()
  })

  it('stays behind when an order just sent opens another table', async () => {
    const openItems = await aNoticeToHandMoneyBackAtTisch12()

    openItems.closeTable()
    openItems.openTableAndSelectItemsOnceLoaded('table-14', ['new-1'])

    expect(openItems.notice).toBeNull()
  })
})
