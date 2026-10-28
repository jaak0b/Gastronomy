import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { enableAutoUnmount, flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import ItemHistoryDialog from '../../../../src/phone/components/orderList/ItemHistoryDialog.vue'
import type { AppLanguage } from '../../../../src/shared/core/deviceLanguage'
import { useSessionStore } from '../../../../src/shared/stores/session'
import { testPlugins } from '../../../support/plugins'
import { answer, inTurn, neverAnswers, noConnection, refusal, stubLaptop } from '../../../support/laptop'
import { allOnScreen, clickOn, isOnScreen, textOnScreen } from '../../../support/dom'

enableAutoUnmount(afterEach)

const HISTORY = '/api/open-items/items/item-bier/history'

const BIER = {
  orderItemId: 'item-bier',
  orderId: 'order-141',
  globalOrderNumber: 141,
  itemName: 'Bier',
  note: 'ohne Schaum',
  unitPriceCents: 400,
  state: 'cancelled' as const,
  stateSetAtUtc: '2026-09-05T18:06:00Z',
  wasHandedOutWhenCancelled: false,
  settledAtUtc: '2026-09-05T18:04:00Z',
  chargedPriceCents: 380,
}

const WHOLE_LIFE = {
  statusChanges: [
    { state: 'open', changedAtUtc: '2026-09-05T18:00:00Z', staffMemberName: 'Anna', stationName: null },
    { state: 'handedOut', changedAtUtc: '2026-09-05T18:02:00Z', staffMemberName: null, stationName: 'Schänke' },
    { state: 'open', changedAtUtc: '2026-09-05T18:03:00Z', staffMemberName: null, stationName: 'Schänke' },
    { state: 'cancelled', changedAtUtc: '2026-09-05T18:06:00Z', staffMemberName: 'Anna', stationName: null },
    { state: 'cancellationAcknowledged', changedAtUtc: '2026-09-05T18:07:00Z', staffMemberName: null, stationName: 'Schänke' },
  ],
  settledAtUtc: '2026-09-05T18:04:00Z',
  chargedPriceCents: 380,
  paymentMethod: 'card',
  paymentNotice: 'Stammgast',
  settledByStaffMemberName: 'Berta',
}

function mountTheHistory(language: AppLanguage = 'de') {
  const session = useSessionStore()
  session.deviceToken = 'token-here'
  session.language = language
  return mount(ItemHistoryDialog, {
    props: { item: BIER, language },
    global: { plugins: testPlugins(language) },
    attachTo: document.body,
  })
}

function textsOf(selector: string): string[] {
  return allOnScreen(selector).map((element) => element.textContent?.trim() ?? '')
}

beforeEach(() => {
  setActivePinia(createPinia())
  localStorage.clear()
  document.body.innerHTML = ''
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('the history of one item', () => {
  it('asks the laptop for the item the waiter tapped', async () => {
    const laptop = stubLaptop().answers('GET', HISTORY, answer(WHOLE_LIFE))

    mountTheHistory()
    await flushPromises()

    expect(laptop.urls()).toEqual([HISTORY])
  })

  it('shows that it is loading while the laptop has not answered', async () => {
    stubLaptop().answers('GET', HISTORY, neverAnswers())

    mountTheHistory()
    await flushPromises()

    expect(isOnScreen('[data-test="history-loading"]')).toBe(true)
  })

  it('heads the page with the item and its note', async () => {
    stubLaptop().answers('GET', HISTORY, answer(WHOLE_LIFE))

    mountTheHistory()
    await flushPromises()

    expect([textOnScreen('[data-test="page-title"]').trim(), textOnScreen('[data-test="history-note"]').trim()]).toEqual([
      'Bier',
      'Hinweis: ohne Schaum',
    ])
  })

  it('shows the price of the item at the top, in German and in English', async () => {
    stubLaptop().answers('GET', HISTORY, answer(WHOLE_LIFE))
    mountTheHistory()
    await flushPromises()
    const german = textOnScreen('[data-test="details-price"]').trim()
    document.body.innerHTML = ''

    mountTheHistory('en')
    await flushPromises()

    expect([german, textOnScreen('[data-test="details-price"]').trim()]).toEqual(['Preis: 4,00 €', 'Price: €4.00'])
  })

  it('lists every step in the order it happened, each with its date and time', async () => {
    stubLaptop().answers('GET', HISTORY, answer(WHOLE_LIFE))

    mountTheHistory()
    await flushPromises()

    expect({ events: textsOf('[data-test="entry-event"]'), times: textsOf('[data-test="entry-time"]') }).toEqual({
      events: ['Bestellt', 'Ausgegeben', 'Zurückgelegt', 'Bezahlt', 'Storniert', 'Stornierung bestätigt'],
      times: [
        '05.09.2026, 20:00',
        '05.09.2026, 20:02',
        '05.09.2026, 20:03',
        '05.09.2026, 20:04',
        '05.09.2026, 20:06',
        '05.09.2026, 20:07',
      ],
    })
  })

  it('names who made each step, the waiter or the station', async () => {
    stubLaptop().answers('GET', HISTORY, answer(WHOLE_LIFE))

    mountTheHistory()
    await flushPromises()

    expect(textsOf('[data-test="entry-by"]')).toEqual(['Anna', 'Schänke', 'Schänke', 'Berta', 'Anna', 'Schänke'])
  })

  it('writes the amount, the card and the reason under the payment', async () => {
    stubLaptop().answers('GET', HISTORY, answer(WHOLE_LIFE))

    mountTheHistory()
    await flushPromises()

    expect([textOnScreen('[data-test="entry-payment"]').trim(), textOnScreen('[data-test="entry-notice"]').trim()]).toEqual([
      '3,80 € mit Karte',
      'Grund: Stammgast',
    ])
  })

  it('writes a cash payment in English on an English phone, without a reason when none was typed', async () => {
    stubLaptop().answers('GET', HISTORY, answer({ ...WHOLE_LIFE, paymentMethod: 'cash', paymentNotice: null }))

    mountTheHistory('en')
    await flushPromises()

    expect({
      payment: textOnScreen('[data-test="entry-payment"]').trim(),
      reason: isOnScreen('[data-test="entry-notice"]'),
    }).toEqual({ payment: '€3.80 in cash', reason: false })
  })

  it('writes the amount alone for a payment stored before the method was kept', async () => {
    stubLaptop().answers('GET', HISTORY, answer({ ...WHOLE_LIFE, paymentMethod: null, paymentNotice: null }))

    mountTheHistory()
    await flushPromises()

    expect(textOnScreen('[data-test="entry-payment"]').trim()).toBe('3,80 €')
  })
})

describe('the history of an item the laptop no longer knows', () => {
  it('says so and offers no reload of its own, because the list has to be reloaded', async () => {
    stubLaptop().answers('GET', HISTORY, refusal('errors.itemHistory.unknownItem', { status: 422 }))

    mountTheHistory()
    await flushPromises()

    expect({
      reason: textOnScreen('[data-test="history-failed"]').trim(),
      reload: isOnScreen('[data-test="history-reload"]'),
    }).toEqual({
      reason: 'Diese Position gibt es am Rechner nicht mehr. Schließen Sie das Fenster. Laden Sie die Seite neu.',
      reload: false,
    })
  })

  it('says the same in English on an English phone', async () => {
    stubLaptop().answers('GET', HISTORY, refusal('errors.itemHistory.unknownItem', { status: 422 }))

    mountTheHistory('en')
    await flushPromises()

    expect(textOnScreen('[data-test="history-failed"]').trim()).toBe(
      'This item is no longer on the laptop. Close this window. Reload the page.',
    )
  })
})

describe('the history when the laptop does not answer', () => {
  it('says the laptop could not be reached and offers to load again', async () => {
    stubLaptop().answers('GET', HISTORY, noConnection())

    mountTheHistory()
    await flushPromises()

    expect({
      reason: textOnScreen('[data-test="history-failed"]').trim(),
      reload: textOnScreen('[data-test="history-reload"]').trim(),
    }).toEqual({ reason: 'Der Rechner war nicht erreichbar. Laden Sie die Seite neu.', reload: 'Neu laden' })
  })

  it('shows the history once the waiter loads again and the laptop answers', async () => {
    stubLaptop().answers('GET', HISTORY, inTurn(noConnection(), answer(WHOLE_LIFE)))
    mountTheHistory()
    await flushPromises()

    await clickOn('[data-test="history-reload"]')

    expect({
      failed: isOnScreen('[data-test="history-failed"]'),
      first: textsOf('[data-test="entry-event"]')[0],
    }).toEqual({ failed: false, first: 'Bestellt' })
  })
})

describe('closing the history', () => {
  it('asks to close when the waiter taps Schließen', async () => {
    stubLaptop().answers('GET', HISTORY, answer(WHOLE_LIFE))
    const dialog = mountTheHistory()
    await flushPromises()

    await clickOn('[data-test="history-close"]')

    expect({ text: textOnScreen('[data-test="history-close"]').trim(), closed: dialog.emitted('close') }).toEqual({
      text: 'Schließen',
      closed: [[]],
    })
  })
})
