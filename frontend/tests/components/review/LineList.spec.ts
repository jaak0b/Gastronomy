import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import { createI18n } from 'vue-i18n'
import { createVuetify } from 'vuetify'
import LineList from '../../../src/components/review/LineList.vue'
import type { BasketLineView } from '../../../src/core/basket'
import type { AppLanguage, DeliveryMode, StationEstimate } from '../../../src/core/apiTypes'
import { routedStationId } from '../../../src/core/routingPreview'
import de from '../../../src/locales/de.json'
import en from '../../../src/locales/en.json'

const STATION_NAMES: Record<string, string> = {
  'station-theke-innen': 'Theke innen',
  'station-theke-aussen': 'Theke aussen',
  'station-kueche': 'Küche',
}

function line(overrides: Partial<BasketLineView> = {}): BasketLineView {
  const chosen: BasketLineView = {
    catalogItemId: 'item-bratwurst',
    name: 'Bratwurst',
    unitPriceCents: 350,
    note: null,
    stationId: null,
    stationName: '',
    candidateStationIds: ['station-kueche'],
    productionMinutes: null,
    isSoldOut: false,
    isNoLongerOnTheMenu: false,
    isNoLongerPreparedAtItsStation: false,
    ...overrides,
  }
  if (overrides.stationName !== undefined) {
    return chosen
  }
  const stationId = routedStationId(chosen)
  return { ...chosen, stationName: stationId === null ? '' : STATION_NAMES[stationId] ?? '' }
}

interface ListOptions {
  orderNote?: string | null
  estimates?: StationEstimate[]
  deliveryModes?: Record<string, DeliveryMode>
  changesAreRefused?: boolean
  language?: AppLanguage
}

function mountList(lines: BasketLineView[], options: ListOptions = {}) {
  const language = options.language ?? 'de'
  const i18n = createI18n({ legacy: false, locale: language, messages: { de, en } })
  const chosen = options.deliveryModes ?? {}
  return mount(LineList, {
    props: {
      lines,
      orderNote: options.orderNote ?? null,
      language,
      estimates: options.estimates ?? [],
      deliveryModeFor: (stationId: string) => chosen[stationId] ?? ('together' as DeliveryMode),
      changesAreRefused: options.changesAreRefused ?? false,
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
  it('offers the choice once per station', () => {
    const list = mountList([
      line(),
      line({
        catalogItemId: 'item-bier',
        name: 'Bier',
        candidateStationIds: ['station-theke-innen'],
      }),
    ])

    expect(list.findAll('.delivery-modes')).toHaveLength(2)
  })

  it('names both ways the station can hand its part out, the first with the time it takes', () => {
    const list = mountList([line({ productionMinutes: 8 })], {
      estimates: [{ stationId: 'station-kueche', queuedMinutes: 12 }],
    })

    expect(list.get('.delivery-together').text()).toBe('Gesammelt ausgeben (~20 Min.)')
    expect(list.get('.delivery-as-it-comes').text()).toBe('Einzeln ausgeben')
  })

  it('names both ways in English, with the same time behind the first one', () => {
    const list = mountList([line({ productionMinutes: 8 })], {
      estimates: [{ stationId: 'station-kueche', queuedMinutes: 12 }],
      language: 'en',
    })

    expect(list.get('.delivery-together').text()).toBe('Hand out together (~20 min)')
    expect(list.get('.delivery-as-it-comes').text()).toBe('Hand out item by item')
  })

  it('holds the choice shut once the order has been sent and the send failed', () => {
    const list = mountList([line()], { changesAreRefused: true })

    expect(list.get('.delivery-together').attributes('disabled')).toBeDefined()
    expect(list.get('.delivery-as-it-comes').attributes('disabled')).toBeDefined()
  })

  it('leaves the choice open while the order has not been sent', () => {
    const list = mountList([line()])

    expect(list.get('.delivery-together').attributes('disabled')).toBeUndefined()
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

  it('reports the station whose card was tapped, not the first one on the screen', async () => {
    const list = mountList([
      line(),
      line({
        catalogItemId: 'item-bier',
        name: 'Bier',
        candidateStationIds: ['station-theke-innen'],
      }),
    ])

    await list.findAll('.station-part')[1].get('.delivery-as-it-comes').trigger('click')

    expect(list.emitted('chooseDeliveryMode')).toEqual([['station-theke-innen', 'asItComes']])
  })

  it('asks nothing for lines that have no station yet', () => {
    const list = mountList([
      line({ candidateStationIds: ['station-kueche', 'station-theke-innen'] }),
    ])

    expect(list.find('.delivery-modes').exists()).toBe(false)
  })
})

describe('how long the order will take', () => {
  it('adds the queue of the station to the time the item itself needs', () => {
    const list = mountList([line({ productionMinutes: 8 })], {
      estimates: [{ stationId: 'station-kueche', queuedMinutes: 12 }],
    })

    expect(list.get('.line-name').text()).toBe('1 x Bratwurst (~20 Min.)')
  })

  it('writes the same time beside the item in English', () => {
    const list = mountList([line({ productionMinutes: 8 })], {
      estimates: [{ stationId: 'station-kueche', queuedMinutes: 12 }],
      language: 'en',
    })

    expect(list.get('.line-name').text()).toBe('1 x Bratwurst (~20 min)')
  })

  it('says the item is ready right away when it takes no time and nothing is queued', () => {
    const list = mountList([line({ productionMinutes: 0 })], {
      estimates: [{ stationId: 'station-kueche', queuedMinutes: 0 }],
    })

    expect(list.get('.line-name').text()).toBe('1 x Bratwurst (~0 Min.)')
  })

  it('names the item alone when nobody gave it a preparation time', () => {
    const list = mountList([line({ productionMinutes: null })], {
      estimates: [{ stationId: 'station-kueche', queuedMinutes: 12 }],
    })

    expect(list.get('.line-name').text()).toBe('1 x Bratwurst')
  })

  it('leaves the button plain when no item of the part carries a preparation time', () => {
    const list = mountList([line({ productionMinutes: null })], {
      estimates: [{ stationId: 'station-kueche', queuedMinutes: 12 }],
    })

    expect(list.get('.delivery-together').text()).toBe('Gesammelt ausgeben')
  })

  it('names the item alone while the line still waits for its station', () => {
    const list = mountList([
      line({ candidateStationIds: ['station-kueche', 'station-theke-innen'] }),
    ])

    expect(list.get('.line-name').text()).toBe('1 x Bratwurst')
  })

  it('carries the slowest item of the part as the time on the button', () => {
    const list = mountList(
      [
        line({ productionMinutes: 8 }),
        line({ catalogItemId: 'item-pommes', name: 'Pommes', productionMinutes: 3 }),
      ],
      { estimates: [{ stationId: 'station-kueche', queuedMinutes: 12 }] },
    )

    expect(list.get('.delivery-together').text()).toBe('Gesammelt ausgeben (~20 Min.)')
  })

  it('leaves the button plain when each item comes out on its own and keeps the item time', () => {
    const list = mountList([line({ productionMinutes: 8 })], {
      estimates: [{ stationId: 'station-kueche', queuedMinutes: 12 }],
      deliveryModes: { 'station-kueche': 'asItComes' },
    })

    expect(list.get('.delivery-together').text()).toBe('Gesammelt ausgeben')
    expect(list.get('.line-name').text()).toBe('1 x Bratwurst (~20 Min.)')
  })
})

describe('a line whose item has sold out', () => {
  it('promises no waiting time, because the item cannot be ordered at all', () => {
    const list = mountList([line({ productionMinutes: 8, isSoldOut: true })], {
      estimates: [{ stationId: 'station-kueche', queuedMinutes: 12 }],
    })

    expect(list.get('.line-name').text()).toBe('1 x Bratwurst')
  })

  it('is left out of the time on the button, because the order will not carry it', () => {
    const list = mountList(
      [
        line({ productionMinutes: 4 }),
        line({
          catalogItemId: 'item-pommes',
          name: 'Pommes',
          productionMinutes: 8,
          isSoldOut: true,
        }),
      ],
      { estimates: [{ stationId: 'station-kueche', queuedMinutes: 12 }] },
    )

    expect(list.get('.delivery-together').text()).toBe('Gesammelt ausgeben (~16 Min.)')
  })

  it('leaves the button plain when no item of the part can be ordered at all', () => {
    const list = mountList([line({ productionMinutes: 8, isSoldOut: true })], {
      estimates: [{ stationId: 'station-kueche', queuedMinutes: 12 }],
    })

    expect(list.get('.delivery-together').text()).toBe('Gesammelt ausgeben')
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

  it('promises no waiting time, because the item cannot be ordered at all', () => {
    const list = mountList(
      [line({ name: 'Currywurst', productionMinutes: 8, isNoLongerOnTheMenu: true })],
      { estimates: [{ stationId: 'station-kueche', queuedMinutes: 12 }] },
    )

    expect(list.get('.line-name').text()).toBe('1 x Currywurst')
  })

  it('shows no price, because the laptop no longer names one and the total must match it', () => {
    const list = mountList([
      line({ name: 'Currywurst', unitPriceCents: null, isNoLongerOnTheMenu: true }),
      line({ name: 'Currywurst', unitPriceCents: null, isNoLongerOnTheMenu: true }),
    ])

    expect(list.find('.line .price').exists()).toBe(false)
  })

  it('falls back to a label when the draft predates the stored name', () => {
    const list = mountList([line({ name: '', isNoLongerOnTheMenu: true })])

    expect(list.get('.line-name').text()).toContain('Nicht mehr auf der Karte.')
  })
})

describe('a line whose station no longer prepares its item', () => {
  function movedLine(overrides: Partial<BasketLineView> = {}) {
    return line({
      name: 'Bier',
      stationId: 'station-theke-innen',
      candidateStationIds: ['station-theke-aussen'],
      isNoLongerPreparedAtItsStation: true,
      ...overrides,
    })
  }

  it('says that the station on the card no longer prepares the item', () => {
    const list = mountList([movedLine()])

    expect(list.get('.station-no-longer-prepares-it').text()).toBe(
      'Diese Ausgabestelle bereitet den Artikel nicht mehr zu.',
    )
  })

  it('is greyed the way every line that cannot be ordered is', () => {
    const list = mountList([movedLine()])

    expect(list.get('.line').classes()).toContain('is-unavailable')
  })

  it('promises no waiting time, because the station will not prepare it', () => {
    const list = mountList([movedLine({ productionMinutes: 8 })], {
      estimates: [{ stationId: 'station-theke-innen', queuedMinutes: 12 }],
    })

    expect(list.get('.line-name').text()).toBe('1 x Bier')
  })

  it('says nothing of the sort while the station still prepares the item', () => {
    const list = mountList([line({ stationId: 'station-kueche' })])

    expect(list.find('.station-no-longer-prepares-it').exists()).toBe(false)
  })
})

describe('a station that has left the item list while the order stood on the summary', () => {
  function beerFromAStationNobodyCanNameAnyMore(): BasketLineView {
    return line({
      catalogItemId: 'item-bier',
      name: 'Bier',
      stationId: 'station-theke-abgebaut',
      candidateStationIds: ['station-theke-abgebaut'],
      stationName: 'Theke aussen',
    })
  }

  it('is still named in the header of its card', () => {
    const list = mountList([beerFromAStationNobodyCanNameAnyMore()])

    expect(list.get('.station-name').text()).toBe('Geht an Theke aussen')
  })
})
