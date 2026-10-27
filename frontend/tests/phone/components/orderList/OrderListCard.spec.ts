import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { enableAutoUnmount, flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import OrderListCard from '../../../../src/phone/components/orderList/OrderListCard.vue'
import type { TableOrderRecordItemView, TableOrderRecordView } from '../../../../src/shared/api/generatedSchemas'
import type { SelectionPurpose } from '../../../../src/phone/core/openItems'
import { useSessionStore } from '../../../../src/shared/stores/session'
import { testPlugins } from '../../../support/plugins'
import { neverAnswers, stubLaptop } from '../../../support/laptop'
import { isOnScreen } from '../../../support/dom'

enableAutoUnmount(afterEach)

function itemOfOrder141(orderItemId: string, itemName: string, overrides: Partial<TableOrderRecordItemView> = {}) {
  return {
    orderItemId,
    orderId: 'order-141',
    globalOrderNumber: 141,
    itemName,
    note: null,
    unitPriceCents: 400,
    orderedAtUtc: '2026-09-05T18:20:00Z',
    state: 'open' as const,
    stateSetAtUtc: '2026-09-05T18:20:00Z',
    wasHandedOutWhenCancelled: false,
    settledAtUtc: null,
    chargedPriceCents: null,
    ...overrides,
  }
}

const ORDER_141: TableOrderRecordView = {
  orderId: 'order-141',
  globalOrderNumber: 141,
  createdAtUtc: '2026-09-05T18:20:00Z',
  staffMemberName: 'Anna',
  notCancelledItemCount: 2,
  handedOutItemCount: 1,
  items: [
    itemOfOrder141('bier', 'Bier'),
    itemOfOrder141('wasser', 'Wasser', { state: 'handedOut', settledAtUtc: '2026-09-05T18:40:00Z', chargedPriceCents: 200 }),
    itemOfOrder141('cola', 'Cola', { state: 'cancelled' }),
  ],
}

function mountTheCard(purpose?: SelectionPurpose, selectedItemIds: string[] = []) {
  useSessionStore().deviceToken = 'token-here'
  return mount(OrderListCard, {
    props: { order: ORDER_141, language: 'de', purpose, selectedItemIds },
    global: { plugins: testPlugins() },
    attachTo: document.body,
  })
}

type Card = ReturnType<typeof mountTheCard>

function lineOf(card: Card, orderItemId: string) {
  return card.get(`[data-test="order-line"][data-test-id="${orderItemId}"]`)
}

beforeEach(() => {
  setActivePinia(createPinia())
  document.body.innerHTML = ''
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('an order shown to read', () => {
  it('heads the marks with Bezahlt and Ausgegeben and puts the counter on the same line', async () => {
    const card = mountTheCard()

    expect(card.findAll('[data-test="column-headings"] > *').map((heading) => heading.text())).toEqual(['1 / 2 zubereitet', 'Bezahlt', 'Ausgegeben'])
  })

  it('has no tick box column and no prices', async () => {
    const card = mountTheCard()

    expect(card.findAll('[data-test="tick-cell"], [data-test="whole-order"], [data-test="line-price"]')).toHaveLength(0)
  })
})

describe('an order on the settle screen', () => {
  it('keeps a tick box cell on every item, empty for the paid and the cancelled one, so the names line up', async () => {
    const card = mountTheCard('settling')

    expect(
      ['bier', 'wasser', 'cola'].map((id) => ({
        cell: lineOf(card, id).find('[data-test="tick-cell"]').exists(),
        tick: lineOf(card, id).find('[data-test="line-tick"]').exists(),
      })),
    ).toEqual([
      { cell: true, tick: true },
      { cell: true, tick: false },
      { cell: true, tick: false },
    ])
  })

  it('puts the tick box for the whole order in the line with the order number', async () => {
    const card = mountTheCard('settling')

    expect(card.get('[data-test="order-header"]').find('[data-test="whole-order"]').exists()).toBe(true)
  })

  it('shows each price beside the marks', async () => {
    const card = mountTheCard('settling')

    expect(lineOf(card, 'bier').get('[data-test="line-price"]').text()).toBe('4,00 €')
  })

  it('heads the marks with the counter on the left, as the overview does', async () => {
    const card = mountTheCard('settling')

    expect(card.findAll('[data-test="column-headings"] > *').map((heading) => heading.text())).toEqual(['1 / 2 zubereitet', 'Bezahlt', 'Ausgegeben'])
  })

  it('asks to tick an item when the waiter taps its tick box', async () => {
    const card = mountTheCard('settling')

    await lineOf(card, 'bier').get('[data-test="line-tick"] input').trigger('click')

    expect(card.emitted('toggle-item')).toEqual([['bier']])
  })
})

describe('an order on the cancel screen', () => {
  it('offers a tick box for the paid item and none for the cancelled one', async () => {
    const card = mountTheCard('cancelling')

    expect({
      paid: lineOf(card, 'wasser').find('[data-test="line-tick"]').exists(),
      cancelled: lineOf(card, 'cola').find('[data-test="line-tick"]').exists(),
    }).toEqual({ paid: true, cancelled: false })
  })
})

describe('tapping an item', () => {
  it('opens the history of the item when the waiter taps its row', async () => {
    const laptop = stubLaptop().answers('GET', '/api/open-items/items/bier/history', neverAnswers())
    const card = mountTheCard('settling')

    await lineOf(card, 'bier').get('[data-test="line-name"]').trigger('click')
    await flushPromises()

    expect({ open: isOnScreen('[data-test="item-history"]'), asked: laptop.urls() }).toEqual({
      open: true,
      asked: ['/api/open-items/items/bier/history'],
    })
  })

  it('only ticks, and opens no history, when the waiter taps the tick box', async () => {
    const laptop = stubLaptop()
    const card = mountTheCard('settling')

    await lineOf(card, 'bier').get('[data-test="line-tick"] input').trigger('click')
    await flushPromises()

    expect({ open: isOnScreen('[data-test="item-history"]'), asked: laptop.urls() }).toEqual({ open: false, asked: [] })
  })

  it('closes the history when the waiter taps Schließen', async () => {
    stubLaptop().answers('GET', '/api/open-items/items/bier/history', neverAnswers())
    const card = mountTheCard()
    await lineOf(card, 'bier').trigger('click')
    await flushPromises()

    ;(document.querySelector('[data-test="history-close"]') as HTMLElement).click()
    await flushPromises()

    expect(isOnScreen('[data-test="item-history"]')).toBe(false)
  })
})
