import { describe, expect, it } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import ConfirmSendDialog from '../../../../src/phone/components/review/ConfirmSendDialog.vue'
import type { BasketLineView } from '../../../../src/phone/core/basket'
import type { DeliveryMode, StationEstimate } from '../../../../src/shared/api/apiTypes'
import { testPlugins } from '../../../support/plugins'

const QUEUES: StationEstimate[] = [{ stationId: 'station-kueche', queuedMinutes: 12 }]

function bratwurst(overrides: Partial<BasketLineView> = {}): BasketLineView {
  return {
    catalogItemId: 'item-bratwurst',
    name: 'Bratwurst',
    unitPriceCents: 350,
    note: null,
    stationId: 'station-kueche',
    stationName: 'Küche',
    candidateStationIds: ['station-kueche'],
    productionMinutes: 28,
    isQueueIndependent: false,
    isSoldOut: false,
    isNoLongerOnTheMenu: false,
    isNoLongerPreparedAtItsStation: false,
    ...overrides,
  }
}

function bier(): BasketLineView {
  return bratwurst({
    catalogItemId: 'item-bier',
    name: 'Bier',
    stationId: 'station-theke-innen',
    stationName: 'Theke innen',
    candidateStationIds: ['station-theke-innen'],
    productionMinutes: null,
  })
}

interface DialogOptions {
  locale?: 'de' | 'en'
  lines?: BasketLineView[]
  deliveryModes?: Record<string, DeliveryMode>
}

const ORDER_ACROSS_TWO_STATIONS: DialogOptions = {
  lines: [bratwurst(), bier()],
  deliveryModes: { 'station-theke-innen': 'asItComes' },
}

function mountDialog(options: DialogOptions = {}) {
  document.body.innerHTML = ''
  const locale = options.locale ?? 'de'
  const chosen = options.deliveryModes ?? {}
  return mount(ConfirmSendDialog, {
    props: {
      tableName: '4',
      totalCents: 6600,
      language: locale,
      lines: options.lines ?? [bratwurst()],
      estimates: QUEUES,
      deliveryModeFor: (stationId: string) => chosen[stationId] ?? ('together' as DeliveryMode),
    },
    global: { plugins: testPlugins(locale) },
    attachTo: document.body,
  })
}

function textOf(selector: string): string {
  return document.querySelector(selector)?.textContent?.trim() ?? ''
}

function textsOf(selector: string): string[] {
  return [...document.querySelectorAll(selector)].map((element) => element.textContent?.trim() ?? '')
}

function confirmButton(): HTMLButtonElement {
  return document.querySelector('.confirm-send-dialog .confirm') as HTMLButtonElement
}

function amountFieldIn(): HTMLInputElement {
  return document.querySelector('.confirm-send-dialog .amount-field input') as HTMLInputElement
}

async function chooseToSettleNow(): Promise<void> {
  ;(document.querySelector('.confirm-send-dialog .settle-now') as HTMLElement).click()
  await flushPromises()
}

async function typeIn(selector: string, typed: string): Promise<void> {
  const field = document.querySelector(`.confirm-send-dialog ${selector} input`) as HTMLInputElement
  field.value = typed
  field.dispatchEvent(new Event('input', { bubbles: true }))
  await flushPromises()
}

describe('the question before an order goes out, in German', () => {
  it('asks whether the order is to be sent', () => {
    mountDialog()

    expect(textOf('.confirm-send-dialog .title')).toBe('Bestellung senden?')
  })

  it('names the table on a line of its own', () => {
    mountDialog()

    expect(textOf('.confirm-send-dialog .row-table .label')).toBe('Tisch:')
    expect(textOf('.confirm-send-dialog .row-table .value')).toBe('4')
  })

  it('names the amount on a line of its own', () => {
    mountDialog()

    expect(textOf('.confirm-send-dialog .row-amount .label')).toBe('Betrag:')
    expect(textOf('.confirm-send-dialog .row-amount .value')).toBe('66,00 €')
  })

  it('gives the one station a line of its own, with the time behind what it will do', () => {
    mountDialog()

    expect(textsOf('.confirm-send-dialog .row-station .label')).toEqual(['Küche:'])
    expect(textsOf('.confirm-send-dialog .row-station .value')).toEqual([
      'Gemeinsam (~40 Min.)',
    ])
  })

  it('adds every line of the station to the queue, not just the slowest one', () => {
    mountDialog({
      lines: [
        bratwurst(),
        bratwurst({ catalogItemId: 'item-currywurst', name: 'Currywurst' }),
      ],
    })

    expect(textsOf('.confirm-send-dialog .row-station .value')).toEqual([
      'Gemeinsam (~68 Min.)',
    ])
  })

  it('names no time for a station that hands its part out item by item', () => {
    mountDialog({ deliveryModes: { 'station-kueche': 'asItComes' } })

    expect(textsOf('.confirm-send-dialog .row-station .value')).toEqual(['Einzeln'])
  })

  it('gives each station of a split order a line, and only the collected one a time', () => {
    mountDialog(ORDER_ACROSS_TWO_STATIONS)

    expect(textsOf('.confirm-send-dialog .row-station .label')).toEqual(['Küche:', 'Theke innen:'])
    expect(textsOf('.confirm-send-dialog .row-station .value')).toEqual([
      'Gemeinsam (~40 Min.)',
      'Einzeln',
    ])
  })

  it('offers sending the order open first and settling it as the other choice', () => {
    mountDialog()

    expect(textOf('.confirm-send-dialog .settle-later')).toBe('Später abrechnen')
    expect(textOf('.confirm-send-dialog .settle-now')).toBe('Jetzt abrechnen')
  })

  it('sends the order open and asks for no amount until the waiter picks settling', () => {
    mountDialog()

    expect(textOf('.confirm-send-dialog .confirm')).toBe('Bestellung senden')
    expect(document.querySelector('.confirm-send-dialog .amount-field')).toBeNull()
  })

  it('turns the confirming button into a settlement and asks for the amount once settling is picked', async () => {
    mountDialog()

    await chooseToSettleNow()

    expect(textOf('.confirm-send-dialog .confirm')).toBe('Bestellung senden und abrechnen')
    expect(amountFieldIn().value).toBe('66,00')
  })

  it('offers the way out beside it', () => {
    mountDialog()

    expect(textOf('.confirm-send-dialog .cancel')).toBe('Abbrechen')
  })
})

describe('the question before an order goes out, in English', () => {
  it('asks whether the order is to be sent', () => {
    mountDialog({ locale: 'en' })

    expect(textOf('.confirm-send-dialog .title')).toBe('Send the order?')
  })

  it('names the table on a line of its own', () => {
    mountDialog({ locale: 'en' })

    expect(textOf('.confirm-send-dialog .row-table .label')).toBe('Table:')
    expect(textOf('.confirm-send-dialog .row-table .value')).toBe('4')
  })

  it('names the amount on a line of its own', () => {
    mountDialog({ locale: 'en' })

    expect(textOf('.confirm-send-dialog .row-amount .label')).toBe('Amount:')
    expect(textOf('.confirm-send-dialog .row-amount .value')).toBe('€66.00')
  })

  it('gives the one station a line of its own, with the time behind what it will do', () => {
    mountDialog({ locale: 'en' })

    expect(textsOf('.confirm-send-dialog .row-station .label')).toEqual(['Küche:'])
    expect(textsOf('.confirm-send-dialog .row-station .value')).toEqual([
      'Combined (~40 min)',
    ])
  })

  it('adds every line of the station to the queue, not just the slowest one', () => {
    mountDialog({
      lines: [
        bratwurst(),
        bratwurst({ catalogItemId: 'item-currywurst', name: 'Currywurst' }),
      ],
      locale: 'en',
    })

    expect(textsOf('.confirm-send-dialog .row-station .value')).toEqual([
      'Combined (~68 min)',
    ])
  })

  it('gives each station of a split order a line, and only the collected one a time', () => {
    mountDialog({ ...ORDER_ACROSS_TWO_STATIONS, locale: 'en' })

    expect(textsOf('.confirm-send-dialog .row-station .label')).toEqual(['Küche:', 'Theke innen:'])
    expect(textsOf('.confirm-send-dialog .row-station .value')).toEqual([
      'Combined (~40 min)',
      'Individual',
    ])
  })

  it('offers sending the order open first and settling it as the other choice', () => {
    mountDialog({ locale: 'en' })

    expect(textOf('.confirm-send-dialog .settle-later')).toBe('Settle later')
    expect(textOf('.confirm-send-dialog .settle-now')).toBe('Settle now')
  })

  it('sends the order open and asks for no amount until the waiter picks settling', () => {
    mountDialog({ locale: 'en' })

    expect(textOf('.confirm-send-dialog .confirm')).toBe('Send the order')
    expect(document.querySelector('.confirm-send-dialog .amount-field')).toBeNull()
  })

  it('turns the confirming button into a settlement and asks for the amount once settling is picked', async () => {
    mountDialog({ locale: 'en' })

    await chooseToSettleNow()

    expect(textOf('.confirm-send-dialog .confirm')).toBe('Send the order and settle it')
    expect(amountFieldIn().value).toBe('66.00')
  })

  it('offers the way out beside it', () => {
    mountDialog({ locale: 'en' })

    expect(textOf('.confirm-send-dialog .cancel')).toBe('Cancel')
  })
})

describe('answering the question', () => {
  it('sends the order open once when the waiter confirms it', async () => {
    const dialog = mountDialog()

    confirmButton().click()
    await flushPromises()

    expect(dialog.emitted('confirmed')).toEqual([[null]])
    expect(dialog.emitted('cancelled')).toBeUndefined()
  })

  it('hands the full amount over as a settlement when the waiter picks settling and pays the whole price', async () => {
    const dialog = mountDialog()
    await chooseToSettleNow()

    confirmButton().click()
    await flushPromises()

    expect(dialog.emitted('confirmed')).toEqual([
      [{ amountPaidCents: 6600, paymentNotice: null }],
    ])
  })

  it('asks for the reason and keeps the confirm shut until a short amount carries one', async () => {
    const dialog = mountDialog()
    await chooseToSettleNow()
    await typeIn('.amount-field', '20,00')

    expect(document.querySelector('.confirm-send-dialog .reason-field')).not.toBeNull()
    confirmButton().click()
    await flushPromises()

    expect(confirmButton().hasAttribute('disabled')).toBe(true)
    expect(dialog.emitted('confirmed')).toBeUndefined()

    await typeIn('.reason-field', 'Stammgast')
    confirmButton().click()
    await flushPromises()

    expect(dialog.emitted('confirmed')).toEqual([
      [{ amountPaidCents: 2000, paymentNotice: 'Stammgast' }],
    ])
  })

  it('leaves the reason out when the guest rounded up', async () => {
    const dialog = mountDialog()
    await chooseToSettleNow()
    await typeIn('.amount-field', '80,00')

    confirmButton().click()
    await flushPromises()

    expect(dialog.emitted('confirmed')).toEqual([
      [{ amountPaidCents: 8000, paymentNotice: null }],
    ])
  })

  it('asks for nothing to happen when the waiter backs out', async () => {
    const dialog = mountDialog()

    ;(document.querySelector('.confirm-send-dialog .cancel') as HTMLElement).click()
    await flushPromises()

    expect(dialog.emitted('cancelled')).toHaveLength(1)
    expect(dialog.emitted('confirmed')).toBeUndefined()
  })
})
