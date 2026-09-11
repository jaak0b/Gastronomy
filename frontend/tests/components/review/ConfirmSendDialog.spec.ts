import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import ConfirmSendDialog from '../../../src/components/review/ConfirmSendDialog.vue'
import type { BasketLineView } from '../../../src/core/basket'
import type { DeliveryMode, StationEstimate } from '../../../src/core/apiTypes'
import { testPlugins } from '../../support/plugins'

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
  settleOnSend?: boolean
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
      settleOnSend: options.settleOnSend ?? true,
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
      'Gesammelt ausgeben (~40 Min.)',
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
      'Gesammelt ausgeben (~68 Min.)',
    ])
  })

  it('names no time for a station that hands its part out item by item', () => {
    mountDialog({ deliveryModes: { 'station-kueche': 'asItComes' } })

    expect(textsOf('.confirm-send-dialog .row-station .value')).toEqual(['Einzeln ausgeben'])
  })

  it('gives each station of a split order a line, and only the collected one a time', () => {
    mountDialog(ORDER_ACROSS_TWO_STATIONS)

    expect(textsOf('.confirm-send-dialog .row-station .label')).toEqual(['Küche:', 'Theke innen:'])
    expect(textsOf('.confirm-send-dialog .row-station .value')).toEqual([
      'Gesammelt ausgeben (~40 Min.)',
      'Einzeln ausgeben',
    ])
  })

  it('carries the same words on the confirming button that the waiter just tapped', () => {
    mountDialog({ settleOnSend: true })

    expect(textOf('.confirm-send-dialog .confirm')).toBe('Bestellung senden und abrechnen')

    mountDialog({ settleOnSend: false })

    expect(textOf('.confirm-send-dialog .confirm')).toBe('Bestellung senden')
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
      'Hand out together (~40 min)',
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
      'Hand out together (~68 min)',
    ])
  })

  it('gives each station of a split order a line, and only the collected one a time', () => {
    mountDialog({ ...ORDER_ACROSS_TWO_STATIONS, locale: 'en' })

    expect(textsOf('.confirm-send-dialog .row-station .label')).toEqual(['Küche:', 'Theke innen:'])
    expect(textsOf('.confirm-send-dialog .row-station .value')).toEqual([
      'Hand out together (~40 min)',
      'Hand out item by item',
    ])
  })

  it('carries the same words on the confirming button that the waiter just tapped', () => {
    mountDialog({ settleOnSend: true, locale: 'en' })

    expect(textOf('.confirm-send-dialog .confirm')).toBe('Send the order and settle it')

    mountDialog({ settleOnSend: false, locale: 'en' })

    expect(textOf('.confirm-send-dialog .confirm')).toBe('Send the order')
  })

  it('offers the way out beside it', () => {
    mountDialog({ locale: 'en' })

    expect(textOf('.confirm-send-dialog .cancel')).toBe('Cancel')
  })
})

describe('answering the question', () => {
  it('sends the order once when the waiter confirms it', async () => {
    const dialog = mountDialog()

    ;(document.querySelector('.confirm-send-dialog .confirm') as HTMLElement).click()
    await dialog.vm.$nextTick()

    expect(dialog.emitted('confirmed')).toHaveLength(1)
    expect(dialog.emitted('cancelled')).toBeUndefined()
  })

  it('asks for nothing to happen when the waiter backs out', async () => {
    const dialog = mountDialog({ settleOnSend: false })

    ;(document.querySelector('.confirm-send-dialog .cancel') as HTMLElement).click()
    await dialog.vm.$nextTick()

    expect(dialog.emitted('cancelled')).toHaveLength(1)
    expect(dialog.emitted('confirmed')).toBeUndefined()
  })
})
