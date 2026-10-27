import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { flushPromises } from '@vue/test-utils'
import { useOpenItemsStore } from '../../../src/phone/stores/openItems'
import { aHold, answer, heldUntil, noConnection, refusal, type LaptopReply } from '../../support/laptop'
import { TABLE_REPORT, aWaiterPhone, laptopAnsweringInTurn } from './openItemsFixture'

vi.mock('@microsoft/signalr', async () => (await import('../../support/hubConnection')).signalrModuleFake())

const REPORT_WITH_THE_BIER_CANCELLED = {
  ...TABLE_REPORT,
  orders: [
    {
      ...TABLE_REPORT.orders[0],
      items: [
        { ...TABLE_REPORT.orders[0].items[0], state: 'cancelled' as const },
        TABLE_REPORT.orders[0].items[1],
      ],
    },
  ],
}

async function storeCancellingAtTisch12(replies: LaptopReply[]) {
  const laptop = laptopAnsweringInTurn([answer(TABLE_REPORT), ...replies])
  aWaiterPhone()
  const openItems = useOpenItemsStore()
  openItems.openTableToCancelItems('table-12')
  await openItems.load()
  return { openItems, laptop }
}

describe('cancelling what the waiter ticked', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('lets the waiter tick an item that is already paid', async () => {
    const { openItems } = await storeCancellingAtTisch12([])

    openItems.toggleItem('wasser-settled')

    expect(openItems.selectedItemIds).toEqual(['wasser-settled'])
  })

  it('names the cash the guest gets back for the paid items in the selection', async () => {
    const { openItems } = await storeCancellingAtTisch12([])

    openItems.setWholeTable(true)

    expect(openItems.cashToReturnForTheItemsToCancel).toBe(200)
  })

  it('sends the ticked items to the laptop', async () => {
    const { openItems, laptop } = await storeCancellingAtTisch12([
      answer({ cashToReturnCents: 0 }),
      answer(REPORT_WITH_THE_BIER_CANCELLED),
    ])
    openItems.toggleItem('bier-open')

    await openItems.cancelSelectedItems()

    const { url, method, body } = laptop.calls[1]
    expect({ url, method, body }).toEqual({
      url: '/api/open-items/cancel',
      method: 'POST',
      body: { orderItemIds: ['bier-open'] },
    })
  })

  it('clears the selection and shows the table as the laptop holds it once the items are cancelled', async () => {
    const { openItems } = await storeCancellingAtTisch12([
      answer({ cashToReturnCents: 0 }),
      answer(REPORT_WITH_THE_BIER_CANCELLED),
    ])
    openItems.toggleItem('bier-open')

    const accepted = await openItems.cancelSelectedItems()

    expect({
      accepted,
      selected: openItems.selectedItemIds,
      bier: openItems.report?.orders[0].items[0].state,
    }).toEqual({ accepted: true, selected: [], bier: 'cancelled' })
  })

  it('never lets a load that was already on its way put a cancelled item back as open', async () => {
    const staleLoad = aHold()
    const { openItems } = await storeCancellingAtTisch12([
      heldUntil(staleLoad.released, answer(TABLE_REPORT)),
      answer({ cashToReturnCents: 0 }),
      answer(REPORT_WITH_THE_BIER_CANCELLED),
    ])
    openItems.toggleItem('bier-open')
    const loadOnItsWay = openItems.load()

    await openItems.cancelSelectedItems()
    staleLoad.release()
    await loadOnItsWay
    await flushPromises()

    expect(openItems.report?.orders[0].items[0].state).toBe('cancelled')
  })

  it('keeps the selection and names the item when the laptop refuses', async () => {
    const { openItems } = await storeCancellingAtTisch12([
      refusal('errors.cancellation.alreadyCancelled', { status: 422, parameters: { name: 'Bier' } }),
    ])
    openItems.toggleItem('bier-open')

    const accepted = await openItems.cancelSelectedItems()

    expect({ accepted, selected: openItems.selectedItemIds, notice: openItems.notice }).toEqual({
      accepted: false,
      selected: ['bier-open'],
      notice: { key: 'errors.cancellation.alreadyCancelled', parameters: { name: 'Bier' }, count: null },
    })
  })

  it('says it is unclear whether the cancellation arrived when no answer came', async () => {
    const { openItems } = await storeCancellingAtTisch12([noConnection()])
    openItems.toggleItem('bier-open')

    const accepted = await openItems.cancelSelectedItems()

    expect({ accepted, selected: openItems.selectedItemIds, key: openItems.notice?.key }).toEqual({
      accepted: false,
      selected: ['bier-open'],
      key: 'phone.openItems.errors.cancelAnswerNeverCame',
    })
  })

  it('goes back to settling when the waiter opens a table to settle it', async () => {
    const { openItems } = await storeCancellingAtTisch12([answer(TABLE_REPORT)])
    openItems.openTable('table-12')
    await openItems.load()

    openItems.toggleItem('wasser-settled')

    expect(openItems.selectedItemIds).toEqual([])
  })
})
