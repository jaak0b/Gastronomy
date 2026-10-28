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

function mountTheCard(purpose?: SelectionPurpose, selectedItemIds: string[] = [], order: TableOrderRecordView = ORDER_141) {
  useSessionStore().deviceToken = 'token-here'
  return mount(OrderListCard, {
    props: { order, language: 'de', purpose, selectedItemIds },
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

    expect(card.findAll('[data-test="column-headings"] > [data-test]').map((heading) => heading.text())).toEqual(['1 / 2 zubereitet', 'Bezahlt', 'Ausgegeben'])
  })

  it('has no tick box column', async () => {
    const card = mountTheCard()

    expect(card.findAll('[data-test="tick-cell"], [data-test="whole-order"]')).toHaveLength(0)
  })
})

describe('an order with nothing to tick on an acting screen', () => {
  const NOTHING_TO_TICK: TableOrderRecordView = {
    ...ORDER_141,
    items: [ORDER_141.items[1], ORDER_141.items[2]],
  }

  it.each(['settling', 'cancelling'] as const)('has no tick box column on the screen for %s', async (purpose) => {
    const card = mountTheCard(purpose === 'settling' ? 'settling' : 'cancelling', [], purpose === 'cancelling' ? { ...ORDER_141, items: [ORDER_141.items[2]] } : NOTHING_TO_TICK)

    expect(card.findAll('[data-test="tick-cell"], [data-test="whole-order"]')).toHaveLength(0)
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

  it('heads the marks with the counter on the left, as the overview does', async () => {
    const card = mountTheCard('settling')

    expect(card.findAll('[data-test="column-headings"] > [data-test]').map((heading) => heading.text())).toEqual(['1 / 2 zubereitet', 'Bezahlt', 'Ausgegeben'])
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

function detailsButtonOf(card: Card, orderItemId: string) {
  return lineOf(card, orderItemId).get('[data-test="item-details"]')
}

describe('the details button of an item', () => {
  it.each([undefined, 'settling', 'cancelling'] as const)(
    'opens the details of the item and ticks nothing, on the screen for %s',
    async (purpose) => {
      const laptop = stubLaptop().answers('GET', '/api/open-items/items/bier/history', neverAnswers())
      const card = mountTheCard(purpose)

      await detailsButtonOf(card, 'bier').trigger('click')
      await flushPromises()

      expect({
        open: isOnScreen('[data-test="item-history"]'),
        asked: laptop.urls(),
        ticked: card.emitted('toggle-item'),
      }).toEqual({ open: true, asked: ['/api/open-items/items/bier/history'], ticked: undefined })
    },
  )

  it('sits on the paid and the cancelled item too, where nothing can be ticked', async () => {
    const card = mountTheCard('settling')

    expect(['wasser', 'cola'].map((id) => lineOf(card, id).find('[data-test="item-details"]').exists())).toEqual([true, true])
  })

  it('closes the details when the waiter taps Schließen', async () => {
    stubLaptop().answers('GET', '/api/open-items/items/bier/history', neverAnswers())
    const card = mountTheCard()
    await detailsButtonOf(card, 'bier').trigger('click')
    await flushPromises()

    ;(document.querySelector('[data-test="history-close"]') as HTMLElement).click()
    await flushPromises()

    expect(isOnScreen('[data-test="item-history"]')).toBe(false)
  })
})

describe('tapping the row of an item', () => {
  it.each(['settling', 'cancelling'] as const)('asks to tick the item on the screen for %s', async (purpose) => {
    const card = mountTheCard(purpose)

    await lineOf(card, 'bier').get('[data-test="line-name"]').trigger('click')

    expect(card.emitted('toggle-item')).toEqual([['bier']])
  })

  it('asks to tick nothing for an item that cannot be ticked', async () => {
    const card = mountTheCard('settling')

    await lineOf(card, 'wasser').get('[data-test="line-name"]').trigger('click')

    expect(card.emitted('toggle-item')).toBeUndefined()
  })

  it('asks to tick only once when the waiter taps the tick box itself', async () => {
    const card = mountTheCard('settling')

    await lineOf(card, 'bier').get('[data-test="line-tick"] input').trigger('click')

    expect(card.emitted('toggle-item')).toEqual([['bier']])
  })

  it('opens nothing on the overview, where the row is only read', async () => {
    const laptop = stubLaptop()
    const card = mountTheCard()

    await lineOf(card, 'bier').get('[data-test="line-name"]').trigger('click')
    await flushPromises()

    expect({ open: isOnScreen('[data-test="item-history"]'), asked: laptop.urls(), ticked: card.emitted('toggle-item') }).toEqual({
      open: false,
      asked: [],
      ticked: undefined,
    })
  })
})
