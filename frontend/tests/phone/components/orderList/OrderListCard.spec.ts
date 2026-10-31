import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { enableAutoUnmount, flushPromises, mount } from '@vue/test-utils'
import type { VCheckboxBtn } from 'vuetify/components'
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

function wholeOrderBoxOf(card: Card) {
  return card.getComponent<typeof VCheckboxBtn>('[data-test="whole-order"]')
}

beforeEach(() => {
  setActivePinia(createPinia())
  document.body.innerHTML = ''
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('an order shown to read', () => {
  it('heads the marks with Bezahlt und Ausgegeben and puts the counter on the same line', async () => {
    const card = mountTheCard()

    expect(card.findAll('[data-test="column-headings"] > [data-test]').map((heading) => heading.text())).toEqual(['1 / 2 zubereitet', 'Bezahlt', 'Ausgegeben'])
  })

  it('has no tick box and no selected row, because the overview is only read', async () => {
    const card = mountTheCard()

    expect(card.findAll('[data-test="whole-order"], [data-test="line-tick"], [data-test="order-line"][data-selected]')).toHaveLength(0)
  })
})

describe('an order with nothing to select on an acting screen', () => {
  const NOTHING_TO_SELECT_CASES: Array<{
    purpose: SelectionPurpose
    order: TableOrderRecordView
    orderItemId: string
    rowCount: number
  }> = [
    {
      purpose: 'settling',
      order: { ...ORDER_141, items: [ORDER_141.items[1], ORDER_141.items[2]] },
      orderItemId: 'wasser',
      rowCount: 2,
    },
    {
      purpose: 'cancelling',
      order: { ...ORDER_141, items: [ORDER_141.items[2]] },
      orderItemId: 'cola',
      rowCount: 1,
    },
  ]

  it.each(NOTHING_TO_SELECT_CASES)(
    'shows no whole-order box and no selected row on the screen for $purpose',
    async ({ purpose, order, orderItemId, rowCount }) => {
      const card = mountTheCard(purpose, [], order)

      expect({
        wholeOrder: card.find('[data-test="whole-order"]').exists(),
        selectedRows: card.findAll('[data-test="order-line"][data-selected="false"]').length,
      }).toEqual({ wholeOrder: false, selectedRows: rowCount })

      await lineOf(card, orderItemId).trigger('click')

      expect(card.emitted('toggle-item')).toBeUndefined()
    },
  )
})

describe('an order on the settle screen', () => {
  it('puts the tick box for the whole order in the line with the order number', async () => {
    const card = mountTheCard('settling')

    expect(card.get('[data-test="order-header"]').find('[data-test="whole-order"]').exists()).toBe(true)
  })

  it('heads the marks with the counter on the left, as the overview does', async () => {
    const card = mountTheCard('settling')

    expect(card.findAll('[data-test="column-headings"] > [data-test]').map((heading) => heading.text())).toEqual(['1 / 2 zubereitet', 'Bezahlt', 'Ausgegeben'])
  })
})

describe('an order on the cancel screen', () => {
  it('leaves the paid item and the cancelled item to the existing selectability rules', async () => {
    const card = mountTheCard('cancelling', ['wasser'])

    expect({
      paid: lineOf(card, 'wasser').attributes('data-selected'),
      cancelled: lineOf(card, 'cola').attributes('data-selected'),
    }).toEqual({ paid: 'true', cancelled: 'false' })
  })
})

describe('the whole-order box', () => {
  it.each([
    { selectedItemIds: [] as string[], checked: false, indeterminate: false },
    { selectedItemIds: ['bier'], checked: false, indeterminate: true },
    { selectedItemIds: ['bier', 'wasser'], checked: true, indeterminate: false },
  ])('shows checked=$checked and indeterminate=$indeterminate for $selectedItemIds', ({ selectedItemIds, checked, indeterminate }) => {
    const card = mountTheCard('cancelling', selectedItemIds)
    const box = wholeOrderBoxOf(card)

    expect({ checked: box.props('modelValue'), indeterminate: box.props('indeterminate') }).toEqual({ checked, indeterminate })
  })

  it('asks to set the whole order when the waiter taps it', async () => {
    const card = mountTheCard('settling')

    await card.get('[data-test="whole-order"] input').trigger('click')

    const emitted = card.emitted('set-whole-order')
    expect({ orderId: (emitted?.[0]?.[0] as TableOrderRecordView).orderId, isWanted: emitted?.[0]?.[1] }).toEqual({
      orderId: 'order-141',
      isWanted: true,
    })
  })
})

describe('the selected state of a row', () => {
  it('marks the selected line and leaves the selectable and the unselectable ones unselected', async () => {
    const card = mountTheCard('cancelling', ['wasser'])

    expect({
      bier: lineOf(card, 'bier').attributes('data-selected'),
      wasser: lineOf(card, 'wasser').attributes('data-selected'),
      cola: lineOf(card, 'cola').attributes('data-selected'),
    }).toEqual({ bier: 'false', wasser: 'true', cola: 'false' })
  })

  it('leaves a paid and a cancelled line unselected on the settle screen', async () => {
    const card = mountTheCard('settling', ['bier'])

    expect({
      bier: lineOf(card, 'bier').attributes('data-selected'),
      wasser: lineOf(card, 'wasser').attributes('data-selected'),
      cola: lineOf(card, 'cola').attributes('data-selected'),
    }).toEqual({ bier: 'true', wasser: 'false', cola: 'false' })
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
  const ORDER_WITH_NOTE: TableOrderRecordView = {
    ...ORDER_141,
    items: [itemOfOrder141('bier', 'Bier', { note: 'ohne Zwiebeln' }), ORDER_141.items[1], ORDER_141.items[2]],
  }

  it.each([
    ['line name', '[data-test="line-name"]'],
    ['note', '[data-test="line-note"]'],
    ['paid mark', '[data-test="paid-mark"]'],
    ['handed-out mark', '[data-test="handed-out-mark"]'],
  ])('asks to tick the item when the waiter taps its %s', async (_, selector) => {
    const card = mountTheCard('settling', [], ORDER_WITH_NOTE)

    await lineOf(card, 'bier').get(selector).trigger('click')

    expect(card.emitted('toggle-item')).toEqual([['bier']])
  })

  it('asks to tick the item when the waiter taps the empty part of its row', async () => {
    const card = mountTheCard('settling')

    await lineOf(card, 'bier').trigger('click')

    expect(card.emitted('toggle-item')).toEqual([['bier']])
  })

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

  it('opens nothing on the overview, where the row is only read', async () => {
    const laptop = stubLaptop()
    const card = mountTheCard()

    await lineOf(card, 'bier').get('[data-test="line-name"]').trigger('click')
    await flushPromises()

    expect({
      open: isOnScreen('[data-test="item-history"]'),
      asked: laptop.urls(),
      ticked: card.emitted('toggle-item'),
      selected: card.findAll('[data-test="order-line"][data-selected]').length,
    }).toEqual({
      open: false,
      asked: [],
      ticked: undefined,
      selected: 0,
    })
  })
})
