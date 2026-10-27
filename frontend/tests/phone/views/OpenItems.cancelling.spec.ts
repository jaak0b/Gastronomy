import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { enableAutoUnmount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { TABLE_REPORT, lineNamed, mountScreenToCancelItems, openItemsLaptop, orderCard } from './openItemsFixture'
import { answer, inTurn, noConnection, refusal, stubLaptop } from '../../support/laptop'
import { clickOn, isOnScreen, textOnScreen } from '../../support/dom'

enableAutoUnmount(afterEach)

const CONFIRMATION = '[data-test="cancel-items-dialog"]'

const CANCELLED = { cashToReturnCents: 200 }

const REPORT_WITH_THE_BRATWURST_CANCELLED = {
  ...TABLE_REPORT,
  orders: [
    {
      ...TABLE_REPORT.orders[0],
      items: [{ ...TABLE_REPORT.orders[0].items[0], state: 'cancelled' }],
    },
    TABLE_REPORT.orders[1],
    TABLE_REPORT.orders[2],
  ],
}

type Screen = Awaited<ReturnType<typeof mountScreenToCancelItems>>

async function tick(screen: Screen, orderNumber: number, itemName: string): Promise<void> {
  await lineNamed(orderCard(screen, orderNumber), itemName).trigger('click')
  await flushPromises()
}

async function openTheConfirmation(screen: Screen): Promise<void> {
  await screen.get('[data-test="cancel-items"]').trigger('click')
  await flushPromises()
}

function namesOnTheConfirmation(): string[] {
  return [...document.querySelectorAll(`${CONFIRMATION} [data-test="item-name"]`)].map(
    (name) => name.textContent?.trim() ?? '',
  )
}

describe('the table opened to cancel items', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
    openItemsLaptop(TABLE_REPORT, answer(CANCELLED))
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('is titled like the button that opened it', async () => {
    const screen = await mountScreenToCancelItems()

    expect(screen.get('[data-test="settle-title"]').text()).toBe('Position stornieren')
  })

  it('lets the waiter tick an item that is already paid', async () => {
    const screen = await mountScreenToCancelItems()

    expect(lineNamed(orderCard(screen, 138), 'Wasser').find('[data-test="line-tick"]').exists()).toBe(true)
  })

  it('counts the ticked items and offers to cancel them', async () => {
    const screen = await mountScreenToCancelItems()

    await tick(screen, 137, 'Bratwurst')
    await tick(screen, 138, 'Wasser')

    expect({
      selected: screen.get('[data-test="selected-total"]').text(),
      action: screen.get('[data-test="cancel-items"]').text(),
    }).toEqual({ selected: 'Ausgewählt: 2 Positionen', action: 'Stornieren' })
  })

  it('names the items in English on an English phone', async () => {
    const screen = await mountScreenToCancelItems('en')

    await tick(screen, 137, 'Bratwurst')

    expect({
      title: screen.get('[data-test="settle-title"]').text(),
      selected: screen.get('[data-test="selected-total"]').text(),
      action: screen.get('[data-test="cancel-items"]').text(),
    }).toEqual({ title: 'Cancel items', selected: 'Selected: 1 item', action: 'Cancel items' })
  })
})

describe('the page that confirms the cancellation', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('lists the ticked items', async () => {
    openItemsLaptop(TABLE_REPORT, answer(CANCELLED))
    const screen = await mountScreenToCancelItems()
    await tick(screen, 137, 'Bratwurst')
    await tick(screen, 138, 'Cola')

    await openTheConfirmation(screen)

    expect(namesOnTheConfirmation()).toEqual(['Bratwurst', 'Cola'])
  })

  it('asks whether to cancel the items in its title, with the buttons to cancel and go back', async () => {
    openItemsLaptop(TABLE_REPORT, answer(CANCELLED))
    const screen = await mountScreenToCancelItems()
    await tick(screen, 137, 'Bratwurst')

    await openTheConfirmation(screen)

    expect({
      title: textOnScreen(`${CONFIRMATION} [data-test="page-title"]`),
      buttons: Array.from(document.querySelectorAll(`${CONFIRMATION} [data-test="actions"] button`)).map((button) =>
        button.textContent?.trim(),
      ),
    }).toEqual({ title: 'Wollen Sie diese Positionen stornieren?', buttons: ['Stornieren', 'Zurück'] })
  })

  it('says how much cash the guest gets back when a paid item is among them', async () => {
    openItemsLaptop(TABLE_REPORT, answer(CANCELLED))
    const screen = await mountScreenToCancelItems()
    await tick(screen, 137, 'Bratwurst')
    await tick(screen, 138, 'Wasser')

    await openTheConfirmation(screen)

    expect(textOnScreen(`${CONFIRMATION} [data-test="cash-to-return"]`).trim()).toBe(
      'Der Gast bekommt 2,00 € bar zurück.',
    )
  })

  it('says it in English on an English phone', async () => {
    openItemsLaptop(TABLE_REPORT, answer(CANCELLED))
    const screen = await mountScreenToCancelItems('en')
    await tick(screen, 138, 'Wasser')

    await openTheConfirmation(screen)

    expect(textOnScreen(`${CONFIRMATION} [data-test="cash-to-return"]`).trim()).toBe(
      'The guest gets €2.00 back in cash.',
    )
  })

  it('says nothing about cash when none of the items is paid', async () => {
    openItemsLaptop(TABLE_REPORT, answer(CANCELLED))
    const screen = await mountScreenToCancelItems()
    await tick(screen, 137, 'Bratwurst')

    await openTheConfirmation(screen)

    expect(isOnScreen(`${CONFIRMATION} [data-test="cash-to-return"]`)).toBe(false)
  })

  it('sends the cancellation and goes back to the table, which shows the item cancelled', async () => {
    const laptop = stubLaptop()
      .answers('POST', '/api/open-items/tables/table-12/search', inTurn(answer(TABLE_REPORT), answer(REPORT_WITH_THE_BRATWURST_CANCELLED)))
      .answers('POST', '/api/open-items/cancel', answer({ cashToReturnCents: 0 }))
    const screen = await mountScreenToCancelItems()
    await tick(screen, 137, 'Bratwurst')
    await openTheConfirmation(screen)

    await clickOn(`${CONFIRMATION} [data-test="confirm-cancel"]`)

    expect({
      sent: laptop.bodiesSentTo('POST', '/api/open-items/cancel'),
      confirmationOpen: isOnScreen(CONFIRMATION),
      bratwurst: lineNamed(orderCard(screen, 137), 'Bratwurst').classes(),
    }).toEqual({
      sent: [{ orderItemIds: ['item-plain'] }],
      confirmationOpen: false,
      bratwurst: expect.arrayContaining(['cancelled-item']),
    })
  })

  it('stays open with the reason and a reload when the laptop refuses', async () => {
    openItemsLaptop(
      TABLE_REPORT,
      refusal('errors.cancellation.alreadyCancelled', { status: 422, parameters: { name: 'Bratwurst' } }),
    )
    const screen = await mountScreenToCancelItems()
    await tick(screen, 137, 'Bratwurst')
    await openTheConfirmation(screen)

    await clickOn(`${CONFIRMATION} [data-test="confirm-cancel"]`)

    expect({
      reason: textOnScreen(`${CONFIRMATION} [data-test="settle-notice"]`).trim(),
      reload: textOnScreen(`${CONFIRMATION} [data-test="reload-after-refusal"]`).trim(),
    }).toEqual({
      reason: 'Bratwurst ist schon storniert. Tippen Sie auf "Neu laden" und wählen Sie die Positionen noch einmal aus.',
      reload: 'Neu laden',
    })
  })

  it('reloads the table and goes back to it when the waiter taps the reload after a refusal', async () => {
    const laptop = openItemsLaptop(TABLE_REPORT, refusal('errors.cancellation.unknownItem', { status: 422 }))
    const screen = await mountScreenToCancelItems()
    await tick(screen, 137, 'Bratwurst')
    await openTheConfirmation(screen)
    await clickOn(`${CONFIRMATION} [data-test="confirm-cancel"]`)

    await clickOn(`${CONFIRMATION} [data-test="reload-after-refusal"]`)

    expect({
      confirmationOpen: isOnScreen(CONFIRMATION),
      tableLoads: laptop.callsTo('POST', '/api/open-items/tables/table-12/search').length,
    }).toEqual({ confirmationOpen: false, tableLoads: 2 })
  })

  it('leaves no reason on the table once the waiter taps the reload after a refusal', async () => {
    openItemsLaptop(TABLE_REPORT, refusal('errors.cancellation.unknownItem', { status: 422 }))
    const screen = await mountScreenToCancelItems()
    await tick(screen, 137, 'Bratwurst')
    await openTheConfirmation(screen)
    await clickOn(`${CONFIRMATION} [data-test="confirm-cancel"]`)

    await clickOn(`${CONFIRMATION} [data-test="reload-after-refusal"]`)

    expect(isOnScreen('[data-test="settle-notice"]')).toBe(false)
  })

  it('shows the old reason neither on the table nor on the page once the waiter went back', async () => {
    openItemsLaptop(TABLE_REPORT, refusal('errors.cancellation.unknownItem', { status: 422 }))
    const screen = await mountScreenToCancelItems()
    await tick(screen, 137, 'Bratwurst')
    await openTheConfirmation(screen)
    await clickOn(`${CONFIRMATION} [data-test="confirm-cancel"]`)

    await clickOn(`${CONFIRMATION} [data-test="back"]`)
    const onTheTable = isOnScreen('[data-test="settle-notice"]')
    await openTheConfirmation(screen)

    expect({ onTheTable, onThePage: isOnScreen(`${CONFIRMATION} [data-test="settle-notice"]`) }).toEqual({
      onTheTable: false,
      onThePage: false,
    })
  })

  it('stays open and says it is unclear whether the cancellation arrived when no answer came', async () => {
    openItemsLaptop(TABLE_REPORT, noConnection())
    const screen = await mountScreenToCancelItems()
    await tick(screen, 137, 'Bratwurst')
    await openTheConfirmation(screen)

    await clickOn(`${CONFIRMATION} [data-test="confirm-cancel"]`)

    expect(textOnScreen(`${CONFIRMATION} [data-test="settle-notice"]`).trim()).toBe(
      'Es ist nicht klar, ob die Stornierung angekommen ist. Tippen Sie auf "Neu laden" und schauen Sie nach, ob die Positionen storniert sind.',
    )
  })

  it('goes back to the table without sending anything when the waiter taps back', async () => {
    const laptop = openItemsLaptop(TABLE_REPORT, answer(CANCELLED))
    const screen = await mountScreenToCancelItems()
    await tick(screen, 137, 'Bratwurst')
    await openTheConfirmation(screen)

    await clickOn(`${CONFIRMATION} [data-test="back"]`)

    expect({ confirmationOpen: isOnScreen(CONFIRMATION), sent: laptop.callsTo('POST', '/api/open-items/cancel') }).toEqual({
      confirmationOpen: false,
      sent: [],
    })
  })
})
