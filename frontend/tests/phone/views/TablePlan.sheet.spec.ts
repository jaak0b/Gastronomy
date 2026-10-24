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
  it('names the table and offers to take an order, to settle and to cancel', async () => {
    await mountThePlan()

    await tapTheTable('table-12')

    expect({
      title: textOnScreen('[data-test="table-actions-title"]').trim(),
      takeOrder: textOnScreen('[data-test="take-order"]').trim(),
      settle: textOnScreen('[data-test="settle-table"]').trim(),
      cancel: textOnScreen('[data-test="cancel-table-actions"]').trim(),
    }).toEqual({ title: 'Tisch 12', takeOrder: 'Bestellung aufnehmen', settle: 'Abrechnen', cancel: 'Abbrechen' })
  })

  it('names the choices in English on an English phone', async () => {
    await mountThePlan('en')

    await tapTheTable('table-12')

    expect({
      takeOrder: textOnScreen('[data-test="take-order"]').trim(),
      settle: textOnScreen('[data-test="settle-table"]').trim(),
      cancel: textOnScreen('[data-test="cancel-table-actions"]').trim(),
    }).toEqual({ takeOrder: 'Take an order', settle: 'Settle', cancel: 'Cancel' })
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

  it('closes and changes nothing when the waiter cancels', async () => {
    const order = useOrderStore()
    await mountThePlan()
    await tapTheTable('table-12')

    await clickOn('[data-test="cancel-table-actions"]')

    expect({ sheet: isOnScreen(TABLE_ACTIONS), table: order.draft.table, route: currentRoute.value }).toEqual({
      sheet: false,
      table: null,
      route: { name: 'home' },
    })
  })
})

describe('taking an order at a table while another table\'s order is not sent yet', () => {
  async function askedAboutTheOrderForTable3() {
    const order = oneWasserOnTheOrderFor({ tableId: 'table-3', name: '3' })
    await mountThePlan()
    await tapTheTable('table-12')
    await clickOn('[data-test="take-order"]')
    return order
  }

  it('asks first, naming the table the unsent items belong to and the table that was tapped', async () => {
    await askedAboutTheOrderForTable3()

    expect({
      question: textOnScreen('[data-test="unsent-order-title"]').trim(),
      back: textOnScreen('[data-test="back-to-the-draft"]').trim(),
      discard: textOnScreen('[data-test="discard-the-draft"]').trim(),
    }).toEqual({
      question: 'Die Bestellung für Tisch 3 ist noch nicht gesendet.',
      back: 'Zurück zur Bestellung für Tisch 3',
      discard: 'Verwerfen und Bestellung für Tisch 12 aufnehmen',
    })
  })

  it('names the table of the unsent items by the name the plan holds now', async () => {
    oneWasserOnTheOrderFor({ tableId: 'table-14', name: '14 alt' })
    await mountThePlan()
    await tapTheTable('table-12')

    await clickOn('[data-test="take-order"]')

    expect(textOnScreen('[data-test="unsent-order-title"]').trim()).toBe(
      'Die Bestellung für Tisch 14 ist noch nicht gesendet.',
    )
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
