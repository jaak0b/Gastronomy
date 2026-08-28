import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import { createI18n } from 'vue-i18n'
import { createVuetify } from 'vuetify'
import LineList from '../../../src/components/review/LineList.vue'
import type { BasketLineView } from '../../../src/core/basket'
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
    isSoldOut: false,
    isNoLongerOnTheMenu: false,
    ...overrides,
  }
}

function mountList(lines: BasketLineView[], orderNote: string | null = null) {
  const i18n = createI18n({ legacy: false, locale: 'de', messages: { de, en } })
  return mount(LineList, {
    props: {
      lines,
      orderNote,
      language: 'de' as const,
      stationNameFor: (stationId: string) => STATION_NAMES[stationId] ?? '',
    },
    global: { plugins: [createVuetify(), i18n] },
  })
}

describe('the slip each station will receive', () => {
  it('draws one card per station and names it in the header', () => {
    const list = mountList([
      line(),
      line({
        catalogItemId: 'item-bier',
        name: 'Bier',
        candidateStationIds: ['station-theke-innen'],
      }),
    ])

    const cards = list.findAll('.station-slip')

    expect(cards).toHaveLength(2)
    expect(cards[0].get('.station-name').text()).toBe('Geht an Küche')
    expect(cards[1].get('.station-name').text()).toBe('Geht an Theke innen')
  })

  it('writes a line the way the printed slip writes it', () => {
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

  it('prints the note for the kitchen on every slip that goes out', () => {
    const list = mountList(
      [
        line(),
        line({
          catalogItemId: 'item-bier',
          name: 'Bier',
          candidateStationIds: ['station-theke-innen'],
        }),
      ],
      'Bitte alles zusammen bringen.',
    )

    const notes = list.findAll('.station-slip .order-note')

    expect(notes).toHaveLength(2)
    expect(notes[0].text()).toContain('Bitte alles zusammen bringen.')
  })

  it('leaves the note off the slips when nobody wrote one', () => {
    const list = mountList([line()])

    expect(list.find('.order-note').exists()).toBe(false)
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
