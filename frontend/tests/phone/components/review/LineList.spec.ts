import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'

import { testPlugins } from '../../../support/plugins'
import LineList from '../../../../src/phone/components/review/LineList.vue'
import type { BasketLineView } from '../../../../src/phone/core/basket'
import { DeliveryMode, StationQuoteView } from '../../../../src/shared/api/generatedSchemas'
import type { AppLanguage } from '../../../../src/shared/core/deviceLanguage'
import { routedStationId } from '../../../../src/phone/core/routingPreview'

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
  quotedStations?: StationQuoteView[]
  deliveryModes?: Record<string, DeliveryMode>
  changesAreRefused?: boolean
  language?: AppLanguage
}

function mountList(lines: BasketLineView[], options: ListOptions = {}) {
  const language = options.language ?? 'de'
  const chosen = options.deliveryModes ?? {}
  return mount(LineList, {
    props: {
      lines,
      language,
      quotedStations: options.quotedStations ?? [],
      deliveryModeFor: (stationId: string) => chosen[stationId] ?? ('together' as DeliveryMode),
      changesAreRefused: options.changesAreRefused ?? false,
    },
    global: { plugins: testPlugins(language) },
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

  it('keeps a line on the element it was already drawn on when another line leaves the order', async () => {
    const bier = line({ catalogItemId: 'item-bier', name: 'Bier' })
    const wasser = line({ catalogItemId: 'item-wasser', name: 'Wasser' })
    const list = mountList([bier, wasser])
    const elementOfWasser = list.findAll('.line')[1].element

    await list.setProps({ lines: [wasser] })

    expect(list.findAll('.line')).toHaveLength(1)
    expect(list.get('.line').element).toBe(elementOfWasser)
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

  it('names both ways the station can hand its part out', () => {
    const list = mountList([line()])

    expect(list.get('.delivery-together').text()).toBe('Gemeinsam')
    expect(list.get('.delivery-as-it-comes').text()).toBe('Einzeln')
  })

  it('names both ways in English', () => {
    const list = mountList([line()], { language: 'en' })

    expect(list.get('.delivery-together').text()).toBe('Combined')
    expect(list.get('.delivery-as-it-comes').text()).toBe('Individual')
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

describe('how long the station will take for its part of the order', () => {
  it('shows the time the laptop calculated on the station header', () => {
    const list = mountList([line()], {
      quotedStations: [{ stationId: 'station-kueche', readyInMinutes: 152 }],
    })

    expect(list.get('.station-name').text()).toBe('Geht an Küche (~152 Min.)')
  })

  it('writes the same header in English', () => {
    const list = mountList([line()], {
      quotedStations: [{ stationId: 'station-kueche', readyInMinutes: 152 }],
      language: 'en',
    })

    expect(list.get('.station-name').text()).toBe('Goes to Küche (~152 min)')
  })

  it('keeps the header time while each item comes out on its own', () => {
    const list = mountList([line()], {
      quotedStations: [{ stationId: 'station-kueche', readyInMinutes: 20 }],
      deliveryModes: { 'station-kueche': 'asItComes' },
    })

    expect(list.get('.station-name').text()).toBe('Geht an Küche (~20 Min.)')
  })

  it('leaves the time off the header when the laptop could not calculate one', () => {
    const list = mountList([line()], {
      quotedStations: [{ stationId: 'station-kueche', readyInMinutes: null }],
    })

    expect(list.get('.station-name').text()).toBe('Geht an Küche')
  })
})
