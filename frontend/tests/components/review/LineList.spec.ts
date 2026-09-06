import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import { createI18n } from 'vue-i18n'
import { createVuetify } from 'vuetify'
import LineList from '../../../src/components/review/LineList.vue'
import type { BasketLineView } from '../../../src/core/basket'
import type { DeliveryMode, StationEstimate } from '../../../src/core/apiTypes'
import de from '../../../src/locales/de.json'
import en from '../../../src/locales/en.json'

const STATION_NAMES: Record<string, string> = {
  'station-theke-innen': 'Theke innen',
  'station-theke-aussen': 'Theke aussen',
  'station-kueche': 'Küche',
}

function line(overrides: Partial<BasketLineView> = {}): BasketLineView {
  return {
    catalogItemId: 'item-bratwurst',
    name: 'Bratwurst',
    unitPriceCents: 350,
    note: null,
    stationId: null,
    candidateStationIds: ['station-kueche'],
    productionMinutes: null,
    isSoldOut: false,
    isNoLongerOnTheMenu: false,
    ...overrides,
  }
}

interface ListOptions {
  orderNote?: string | null
  estimates?: StationEstimate[]
  deliveryModes?: Record<string, DeliveryMode>
}

function mountList(lines: BasketLineView[], options: ListOptions = {}) {
  const i18n = createI18n({ legacy: false, locale: 'de', messages: { de, en } })
  const chosen = options.deliveryModes ?? {}
  return mount(LineList, {
    props: {
      lines,
      orderNote: options.orderNote ?? null,
      language: 'de' as const,
      stationNameFor: (stationId: string) => STATION_NAMES[stationId] ?? '',
      estimates: options.estimates ?? [],
      deliveryModeFor: (stationId: string) => chosen[stationId] ?? ('together' as DeliveryMode),
    },
    global: { plugins: [createVuetify(), i18n] },
  })
}

describe('the part of the order each station will receive', () => {
  it('draws one card per station and names it in the header', () => {
    const list = mountList([
      line(),
      line({
        catalogItemId: 'item-bier',
        name: 'Bier',
        candidateStationIds: ['station-theke-innen'],
      }),
    ])

    const cards = list.findAll('.station-part')

    expect(cards).toHaveLength(2)
    expect(cards[0].get('.station-name').text()).toBe('Geht an Küche')
    expect(cards[1].get('.station-name').text()).toBe('Geht an Theke innen')
  })

  it('writes a line with its count, its name and its price', () => {
    const list = mountList([line(), line()])

    expect(list.get('.line .line-name').text()).toBe('2 x Bratwurst')
    expect(list.get('.line .price').text()).toBe('7,00 €')
  })

  it('puts a note under the line it belongs to', () => {
    const list = mountList([line({ note: 'ohne Zwiebeln' })])

    expect(list.get('.line .line-note').text()).toBe('ohne Zwiebeln')
  })

  it('shows no note line on an item nobody wrote a note for', () => {
    const list = mountList([line()])

    expect(list.find('.line-note').exists()).toBe(false)
  })

  it('sorts the lines by item name', () => {
    const list = mountList([
      line({ catalogItemId: 'item-wasser', name: 'Wasser' }),
      line({ catalogItemId: 'item-bier', name: 'Bier' }),
    ])

    expect(list.findAll('.line .line-name').map((element) => element.text())).toEqual([
      '1 x Bier',
      '1 x Wasser',
    ])
  })

  it('keeps a noted line directly under the plain line of the same item', () => {
    const list = mountList([
      line({ catalogItemId: 'item-wasser', name: 'Wasser', note: 'mit Zitrone' }),
      line({ catalogItemId: 'item-wasser', name: 'Wasser' }),
      line({ catalogItemId: 'item-wasser', name: 'Wasser' }),
    ])

    expect(list.findAll('.line .line-name').map((element) => element.text())).toEqual([
      '2 x Wasser',
      '1 x Wasser',
    ])
    expect(list.findAll('.line')[1].get('.line-note').text()).toBe('mit Zitrone')
  })

  it('repeats the note for the kitchen on every station card', () => {
    const list = mountList(
      [
        line(),
        line({
          catalogItemId: 'item-bier',
          name: 'Bier',
          candidateStationIds: ['station-theke-innen'],
        }),
      ],
      { orderNote: 'Bitte alles zusammen bringen.' },
    )

    const notes = list.findAll('.station-part .order-note')

    expect(notes).toHaveLength(2)
    expect(notes[0].text()).toContain('Bitte alles zusammen bringen.')
  })

  it('leaves the note off the cards when nobody wrote one', () => {
    const list = mountList([line()])

    expect(list.find('.order-note').exists()).toBe(false)
  })
})

describe('choosing how a station hands its part of the order out', () => {
  it('asks the question once per station, naming that station', () => {
    const list = mountList([
      line(),
      line({
        catalogItemId: 'item-bier',
        name: 'Bier',
        candidateStationIds: ['station-theke-innen'],
      }),
    ])

    expect(list.findAll('.delivery-question').map((element) => element.text())).toEqual([
      'Wie soll Küche die Positionen ausgeben?',
      'Wie soll Theke innen die Positionen ausgeben?',
    ])
  })

  it('starts on handing everything out together', () => {
    const list = mountList([line()])

    expect(list.get('.delivery-together').classes()).toContain('v-btn--active')
  })

  it('shows the choice the server already made', () => {
    const list = mountList([line()], {
      deliveryModes: { 'station-kueche': 'asItComes' },
    })

    expect(list.get('.delivery-as-it-comes').classes()).toContain('v-btn--active')
  })

  it('reports the station and the mode when the server taps the other choice', async () => {
    const list = mountList([line()])

    await list.get('.delivery-as-it-comes').trigger('click')

    expect(list.emitted('chooseDeliveryMode')).toEqual([['station-kueche', 'asItComes']])
  })

  it('explains what handing everything out together means', () => {
    const list = mountList([line()])

    expect(list.get('.delivery-help').text()).toBe(
      'Die Ausgabestelle hält alles zurück, bis die letzte Position fertig ist.',
    )
  })

  it('explains what handing each item out on its own means', () => {
    const list = mountList([line()], { deliveryModes: { 'station-kueche': 'asItComes' } })

    expect(list.get('.delivery-help').text()).toBe(
      'Jede Position wird für sich ausgegeben, sobald sie fertig ist.',
    )
  })

  it('asks nothing for lines that have no station yet', () => {
    const list = mountList([
      line({ candidateStationIds: ['station-kueche', 'station-theke-innen'] }),
    ])

    expect(list.find('.delivery-question').exists()).toBe(false)
  })
})

describe('how long the order will take', () => {
  it('adds the queue of the station to the time the item itself needs', () => {
    const list = mountList([line({ productionMinutes: 8 })], {
      estimates: [{ stationId: 'station-kueche', queuedMinutes: 12 }],
    })

    expect(list.get('.line-ready').text()).toBe('Fertig in etwa 20 Minuten')
  })

  it('says the item is ready right away when nothing stands in front of it', () => {
    const list = mountList([line({ productionMinutes: null })], {
      estimates: [{ stationId: 'station-kueche', queuedMinutes: 0 }],
    })

    expect(list.get('.line-ready').text()).toBe('Sofort fertig')
  })

  it('names the slowest item as the time the whole station part takes', () => {
    const list = mountList(
      [
        line({ productionMinutes: 8 }),
        line({ catalogItemId: 'item-pommes', name: 'Pommes', productionMinutes: 3 }),
      ],
      { estimates: [{ stationId: 'station-kueche', queuedMinutes: 12 }] },
    )

    expect(list.get('.slice-ready').text()).toBe('Alles ist in etwa 20 Minuten fertig.')
  })

  it('names no time for the whole part when each item comes out on its own', () => {
    const list = mountList([line({ productionMinutes: 8 })], {
      estimates: [{ stationId: 'station-kueche', queuedMinutes: 12 }],
      deliveryModes: { 'station-kueche': 'asItComes' },
    })

    expect(list.find('.slice-ready').exists()).toBe(false)
    expect(list.get('.line-ready').text()).toBe('Fertig in etwa 20 Minuten')
  })
})

describe('a line whose item is no longer on the menu', () => {
  it('stays on the screen with the name the guest ordered it by', () => {
    const list = mountList([line({ name: 'Currywurst', isNoLongerOnTheMenu: true })])

    expect(list.get('.line-name').text()).toBe('1 x Currywurst')
  })

  it('says that the item is no longer on the menu', () => {
    const list = mountList([line({ name: 'Currywurst', isNoLongerOnTheMenu: true })])

    expect(list.get('.no-longer-on-the-menu').text()).toBe('Nicht mehr auf der Karte.')
  })

  it('is greyed the same way a sold out line is', () => {
    const list = mountList([line({ name: 'Currywurst', isNoLongerOnTheMenu: true })])

    expect(list.get('.line').classes()).toContain('is-unavailable')
  })

  it('still shows its price, because the server reads the total out loud', () => {
    const list = mountList([
      line({ name: 'Currywurst', unitPriceCents: 400, isNoLongerOnTheMenu: true }),
      line({ name: 'Currywurst', unitPriceCents: 400, isNoLongerOnTheMenu: true }),
    ])

    expect(list.get('.price').text()).toBe('8,00 €')
  })

  it('falls back to a label when the draft predates the stored name', () => {
    const list = mountList([line({ name: '', isNoLongerOnTheMenu: true })])

    expect(list.get('.line-name').text()).toContain('Nicht mehr auf der Karte.')
  })
})
