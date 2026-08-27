import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import { createI18n } from 'vue-i18n'
import LineList from '../../../src/components/review/LineList.vue'
import type { BasketLineView } from '../../../src/core/basket'
import de from '../../../src/locales/de.json'
import en from '../../../src/locales/en.json'

const LOCATION_NAMES: Record<string, string> = {
  'location-theke-innen': 'Theke innen',
  'location-theke-aussen': 'Theke aussen',
  'location-kueche': 'Küche',
}

function line(overrides: Partial<BasketLineView> = {}): BasketLineView {
  return {
    catalogItemId: 'item-bratwurst',
    name: 'Bratwurst',
    unitPriceCents: 350,
    quantity: 2,
    note: null,
    productionLocationId: null,
    candidateLocationIds: ['location-kueche'],
    isSoldOut: false,
    isNoLongerOnTheMenu: false,
    ...overrides,
  }
}

function mountList(lines: BasketLineView[]) {
  const i18n = createI18n({ legacy: false, locale: 'de', messages: { de, en } })
  return mount(LineList, {
    props: {
      lines,
      language: 'de' as const,
      locationNameFor: (locationId: string) => LOCATION_NAMES[locationId] ?? '',
    },
    global: { plugins: [i18n] },
  })
}

describe('the note on a line', () => {
  it('offers a note field labelled for this one item', () => {
    const list = mountList([line()])

    expect(list.get('.line-note label').text()).toBe('Hinweis für diese Position')
  })

  it('suggests what a note looks like', () => {
    const list = mountList([line()])

    expect(list.get('.line-note input').attributes('placeholder')).toBe(
      'Zum Beispiel: ohne Zwiebeln',
    )
  })

  it('shows the note the server already typed', () => {
    const list = mountList([line({ note: 'ohne Zwiebeln' })])

    expect((list.get('.line-note input').element as HTMLInputElement).value).toBe('ohne Zwiebeln')
  })

  it('reports a typed note against the line it was typed on', async () => {
    const list = mountList([line(), line({ catalogItemId: 'item-bier' })])

    await list.findAll('.line-note input')[1].setValue('ohne Eis')

    expect(list.emitted('changeNote')).toEqual([[1, 'ohne Eis']])
  })

  it('reports an emptied note as no note at all', async () => {
    const list = mountList([line({ note: 'ohne Zwiebeln' })])

    await list.get('.line-note input').setValue('   ')

    expect(list.emitted('changeNote')).toEqual([[0, null]])
  })
})

describe('the station a line goes to', () => {
  it('names the chosen station on a line that had a choice', () => {
    const list = mountList([
      line({
        candidateLocationIds: ['location-theke-innen', 'location-theke-aussen'],
        productionLocationId: 'location-theke-aussen',
      }),
    ])

    expect(list.get('.line-station').text()).toBe('Station: Theke aussen')
  })

  it('offers to change the station on a line that had a choice', () => {
    const list = mountList([
      line({
        candidateLocationIds: ['location-theke-innen', 'location-theke-aussen'],
        productionLocationId: 'location-theke-innen',
      }),
    ])

    expect(list.get('.change-station').text()).toBe('Station ändern')
  })

  it('stays invisible on a line only one station can prepare', () => {
    const list = mountList([line()])

    expect(list.find('.line-station').exists()).toBe(false)
    expect(list.find('.change-station').exists()).toBe(false)
  })

  it('groups the lines under the station they will be printed at', () => {
    const list = mountList([line()])

    expect(list.get('.group h3').text()).toBe('Geht an Küche')
  })
})

describe('a line whose item is no longer on the menu', () => {
  it('stays on the screen with the name the guest ordered it by', () => {
    const list = mountList([line({ name: 'Currywurst', isNoLongerOnTheMenu: true })])

    expect(list.get('.name').text()).toBe('Currywurst')
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
      line({ name: 'Currywurst', unitPriceCents: 400, quantity: 2, isNoLongerOnTheMenu: true }),
    ])

    expect(list.get('.price').text()).toBe('8,00 €')
  })

  it('falls back to a label when the draft predates the stored name', () => {
    const list = mountList([line({ name: '', isNoLongerOnTheMenu: true })])

    expect(list.get('.name').text()).toBe('Nicht mehr auf der Karte.')
  })
})

describe('the quantity buttons', () => {
  it('names one more and one less for a screen reader, since the buttons carry icons', () => {
    const list = mountList([line()])

    expect(list.get('.less').attributes('aria-label')).toBe('Eins weniger')
    expect(list.get('.more').attributes('aria-label')).toBe('Eins mehr')
  })

  it('asks for one less when the server taps the minus', async () => {
    const list = mountList([line({ quantity: 2 })])

    await list.get('.less').trigger('click')

    expect(list.emitted('changeQuantity')).toEqual([[0, 1]])
  })

  it('asks for one more when the server taps the plus', async () => {
    const list = mountList([line({ quantity: 2 })])

    await list.get('.more').trigger('click')

    expect(list.emitted('changeQuantity')).toEqual([[0, 3]])
  })
})
