import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { enableAutoUnmount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { useOpenItemsStore } from '../../../src/phone/stores/openItems'
import { useOrderStore } from '../../../src/phone/stores/order'
import { currentRoute, navigate } from '../../../src/shared/router/router'
import { clickOn, isOnScreen, textOnScreen } from '../../support/dom'
import { theWaiterTakesTheOrderAt } from '../../support/waiterPhone'
import { wasserOnTheMenu } from './reviewFixture'
import { HALL, TABLE_ACTIONS, mountThePlan, planLaptop, tapTheTable } from './tablePlanFixture'

enableAutoUnmount(afterEach)

const UNSENT_ORDER = '[data-test="unsent-order"]'

const TABLE_12 = { tableId: 'table-12', name: '12' }

beforeEach(() => {
  setActivePinia(createPinia())
  localStorage.clear()
  document.body.innerHTML = ''
  navigate('/')
  planLaptop([HALL])
  wasserOnTheMenu()
})

afterEach(() => {
  vi.unstubAllGlobals()
})

function oneWasserOnTheOrderFor(table: { tableId: string; name: string }) {
  theWaiterTakesTheOrderAt(table)
  const order = useOrderStore()
  order.addItem({ catalogItemId: 'item-wasser', note: null, stationId: 'station-bar', name: 'Wasser' })
  return order
}

describe('the choices a tapped table offers', () => {
  it('names the table and offers to take an order, to settle, to cancel items and to go back', async () => {
    await mountThePlan()

    await tapTheTable('table-12')

    expect({
      title: textOnScreen('[data-test="table-actions-title"]').trim(),
      takeOrder: textOnScreen('[data-test="take-order"]').trim(),
      settle: textOnScreen('[data-test="settle-table"]').trim(),
      cancelItems: textOnScreen('[data-test="cancel-items"]').trim(),
      back: textOnScreen('[data-test="back-from-table-actions"]').trim(),
    }).toEqual({
      title: 'Tisch 12',
      takeOrder: 'Bestellung aufnehmen',
      settle: 'Position kassieren',
      cancelItems: 'Position stornieren',
      back: 'Zurück',
    })
  })

  it('names the choices in English on an English phone', async () => {
    await mountThePlan('en')

    await tapTheTable('table-12')

    expect({
      takeOrder: textOnScreen('[data-test="take-order"]').trim(),
      settle: textOnScreen('[data-test="settle-table"]').trim(),
      cancelItems: textOnScreen('[data-test="cancel-items"]').trim(),
      back: textOnScreen('[data-test="back-from-table-actions"]').trim(),
    }).toEqual({ takeOrder: 'Take an order', settle: 'Take payment', cancelItems: 'Cancel items', back: 'Back' })
  })

  it('opens the table to cancel items when the waiter taps that choice', async () => {
    await mountThePlan()
    await tapTheTable('table-12')

    await clickOn('[data-test="cancel-items"]')

    const openItems = useOpenItemsStore()
    expect({ table: openItems.tableId, purpose: openItems.purpose, route: currentRoute.value }).toEqual({
      table: 'table-12',
      purpose: 'cancelling',
      route: { name: 'openItems' },
    })
  })

  it('takes the order for the table when nothing is waiting to be sent', async () => {
    const order = useOrderStore()
    await mountThePlan()
    await tapTheTable('table-12')

    await clickOn('[data-test="take-order"]')

    expect({ table: order.draft.table, route: currentRoute.value }).toEqual({
      table: TABLE_12,
      route: { name: 'catalog' },
    })
  })

  it('carries on with the unsent order when the waiter takes the order at its own table again', async () => {
    const order = oneWasserOnTheOrderFor(TABLE_12)
    await mountThePlan()
    await tapTheTable('table-12')

    await clickOn('[data-test="take-order"]')

    expect({ lines: order.draft.lines.length, asked: isOnScreen(UNSENT_ORDER), route: currentRoute.value }).toEqual({
      lines: 1,
      asked: false,
      route: { name: 'catalog' },
    })
  })

  it('opens the settle view of the table', async () => {
    await mountThePlan()
    await tapTheTable('table-12')

    await clickOn('[data-test="settle-table"]')

    expect({ table: useOpenItemsStore().tableId, route: currentRoute.value }).toEqual({
      table: 'table-12',
      route: { name: 'openItems' },
    })
  })

  it('settles another table without asking and leaves the unsent order alone', async () => {
    const order = oneWasserOnTheOrderFor({ tableId: 'table-3', name: '3' })
    await mountThePlan()
    await tapTheTable('table-12')

    await clickOn('[data-test="settle-table"]')

    expect({
      asked: isOnScreen(UNSENT_ORDER),
      table: order.draft.table,
      lines: order.draft.lines.length,
    }).toEqual({ asked: false, table: { tableId: 'table-3', name: '3' }, lines: 1 })
  })

  it('opens another table to cancel items without asking and leaves the unsent order alone', async () => {
    const order = oneWasserOnTheOrderFor({ tableId: 'table-3', name: '3' })
    await mountThePlan()
    await tapTheTable('table-12')

    await clickOn('[data-test="cancel-items"]')

    expect({
      asked: isOnScreen(UNSENT_ORDER),
      table: order.draft.table,
      lines: order.draft.lines.length,
    }).toEqual({ asked: false, table: { tableId: 'table-3', name: '3' }, lines: 1 })
  })

  it('closes and changes nothing when the waiter goes back', async () => {
    const order = useOrderStore()
    await mountThePlan()
    await tapTheTable('table-12')

    await clickOn('[data-test="back-from-table-actions"]')

    expect({ sheet: isOnScreen(TABLE_ACTIONS), table: order.draft.table, route: currentRoute.value }).toEqual({
      sheet: false,
      table: null,
      route: { name: 'home' },
    })
  })
})

describe('taking an order at a table while another table\'s order is not sent yet', () => {
  async function askedAboutTheOrderForTable3(locale: 'de' | 'en' = 'de') {
    const order = oneWasserOnTheOrderFor({ tableId: 'table-3', name: '3' })
    await mountThePlan(locale)
    await tapTheTable('table-12')
    await clickOn('[data-test="take-order"]')
    return order
  }

  it('says which table holds the unsent item and what it costs', async () => {
    await askedAboutTheOrderForTable3()

    expect(textOnScreen('[data-test="unsent-order-text"]').trim()).toBe(
      'Tisch 3 hat 1 Artikel für 2,00 €, der noch nicht gesendet ist.',
    )
  })

  it('counts every unsent item and adds up their prices', async () => {
    oneWasserOnTheOrderFor({ tableId: 'table-3', name: '3' }).addItem({
      catalogItemId: 'item-wasser',
      note: 'ohne Eis',
      stationId: 'station-bar',
      name: 'Wasser',
    })
    await mountThePlan()
    await tapTheTable('table-12')

    await clickOn('[data-test="take-order"]')

    expect(textOnScreen('[data-test="unsent-order-text"]').trim()).toBe(
      'Tisch 3 hat 2 Artikel für 4,00 €, die noch nicht gesendet sind.',
    )
  })

  it('says the same in English on an English phone', async () => {
    await askedAboutTheOrderForTable3('en')

    expect(textOnScreen('[data-test="unsent-order-text"]').trim()).toBe('Table 3 has 1 unsent item for €2.00.')
  })

  it('offers to go to that table, to discard its items and to go back', async () => {
    await askedAboutTheOrderForTable3()

    expect({
      toTable: textOnScreen('[data-test="back-to-the-draft"]').trim(),
      discard: textOnScreen('[data-test="discard-the-draft"]').trim(),
      back: textOnScreen('[data-test="stay-on-the-plan"]').trim(),
    }).toEqual({ toTable: 'Zu Tisch 3', discard: 'Verwerfen', back: 'Zurück' })
  })

  it('names the three choices in English on an English phone', async () => {
    await askedAboutTheOrderForTable3('en')

    expect({
      toTable: textOnScreen('[data-test="back-to-the-draft"]').trim(),
      discard: textOnScreen('[data-test="discard-the-draft"]').trim(),
      back: textOnScreen('[data-test="stay-on-the-plan"]').trim(),
    }).toEqual({ toTable: 'Go to table 3', discard: 'Discard', back: 'Back' })
  })

  it('names the table of the unsent items by the name the plan holds now', async () => {
    oneWasserOnTheOrderFor({ tableId: 'table-14', name: '14 alt' })
    await mountThePlan()
    await tapTheTable('table-12')

    await clickOn('[data-test="take-order"]')

    expect(textOnScreen('[data-test="unsent-order-text"]').trim()).toBe(
      'Tisch 14 hat 1 Artikel für 2,00 €, der noch nicht gesendet ist.',
    )
  })

  it('goes back to the plan and leaves the unsent items and the plan as they were', async () => {
    const order = await askedAboutTheOrderForTable3()

    await clickOn('[data-test="stay-on-the-plan"]')

    expect({
      asked: isOnScreen(UNSENT_ORDER),
      table: order.draft.table,
      lines: order.draft.lines.length,
      route: currentRoute.value,
      plan: isOnScreen('[data-test="area-plan"]'),
    }).toEqual({ asked: false, table: { tableId: 'table-3', name: '3' }, lines: 1, route: { name: 'home' }, plan: true })
  })

  it('goes back to the order of its own table and keeps every item', async () => {
    const order = await askedAboutTheOrderForTable3()

    await clickOn('[data-test="back-to-the-draft"]')

    expect({ table: order.draft.table, lines: order.draft.lines.length, route: currentRoute.value }).toEqual({
      table: { tableId: 'table-3', name: '3' },
      lines: 1,
      route: { name: 'catalog' },
    })
  })

  it('throws the unsent items away and takes the order for the tapped table', async () => {
    const order = await askedAboutTheOrderForTable3()

    await clickOn('[data-test="discard-the-draft"]')

    expect({ table: order.draft.table, lines: order.draft.lines.length, route: currentRoute.value }).toEqual({
      table: TABLE_12,
      lines: 0,
      route: { name: 'catalog' },
    })
  })
})
