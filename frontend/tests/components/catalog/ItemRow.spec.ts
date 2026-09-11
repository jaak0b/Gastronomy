import { beforeEach, describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import { createI18n } from 'vue-i18n'
import { createVuetify } from 'vuetify'
import ItemRow from '../../../src/components/catalog/ItemRow.vue'
import type { CatalogItem } from '../../../src/core/apiTypes'
import type { EstimateRange } from '../../../src/core/estimates'
import type { ItemPosition } from '../../../src/core/itemPositions'
import de from '../../../src/locales/de.json'
import en from '../../../src/locales/en.json'

function item(isAvailable: boolean): CatalogItem {
  return {
    id: 'item-wasser',
    name: 'Wasser',
    categoryId: 'category-getraenke',
    priceCents: 200,
    sortOrder: 1,
    isAvailable,
    stationIds: ['station-bar'],
  }
}

function plain(index: number): ItemPosition {
  return {
    index,
    note: null,
    hasAStationChoice: false,
    stationId: 'station-bar',
    stationName: null,
  }
}

function noted(index: number, note: string): ItemPosition {
  return {
    index,
    note,
    hasAStationChoice: false,
    stationId: 'station-bar',
    stationName: null,
  }
}

function mountRow(isAvailable: boolean, positions: ItemPosition[]) {
  const i18n = createI18n({ legacy: false, locale: 'de', messages: { de, en } })
  return mount(ItemRow, {
    props: { item: item(isAvailable), positions, language: 'de' as const, estimateRange: null },
    global: { plugins: [createVuetify(), i18n] },
    attachTo: document.body,
  })
}

function mountRowForAnItemAtSeveralStations(isAvailable: boolean, positions: ItemPosition[]) {
  const i18n = createI18n({ legacy: false, locale: 'de', messages: { de, en } })
  return mount(ItemRow, {
    props: {
      item: { ...item(isAvailable), stationIds: ['station-kueche', 'station-bar'] },
      positions,
      language: 'de' as const,
      estimateRange: null,
    },
    global: { plugins: [createVuetify(), i18n] },
    attachTo: document.body,
  })
}

function dialogField(): HTMLInputElement {
  return document.querySelector('.note-dialog .note-input input') as HTMLInputElement
}

async function typeIntoDialog(row: ReturnType<typeof mountRow>, text: string): Promise<void> {
  const field = dialogField()
  field.value = text
  field.dispatchEvent(new Event('input'))
  await row.vm.$nextTick()
}

async function confirmDialog(row: ReturnType<typeof mountRow>): Promise<void> {
  ;(document.querySelector('.note-dialog .note-confirm') as HTMLElement).click()
  await row.vm.$nextTick()
}

beforeEach(() => {
  document.body.innerHTML = ''
})

describe('the item row itself', () => {
  it('names the item and its price', () => {
    const row = mountRow(true, [])

    expect(row.get('.name').text()).toBe('Wasser')
    expect(row.get('.price').text()).toBe('2,00 €')
  })

  it('adds one of the item and opens nothing when the name is tapped', async () => {
    const row = mountRow(true, [])

    await row.get('.add').trigger('click')

    expect(row.emitted('add')).toHaveLength(1)
    expect(document.querySelector('.note-dialog')).toBeNull()
  })

  it('counts the portions that carry no note', () => {
    const row = mountRow(true, [plain(0), plain(1), noted(2, 'ohne Eis')])

    expect(row.get('.count').text()).toBe('2')
  })

  it('shows no count while nothing plain is on the order', () => {
    const row = mountRow(true, [noted(0, 'ohne Eis')])

    expect(row.find('.count').exists()).toBe(false)
  })

  it('takes the most recently added plain portion off again', async () => {
    const row = mountRow(true, [plain(0), plain(4)])

    await row.get('.remove-one').trigger('click')

    expect(row.emitted('removeOne')).toEqual([[4]])
  })

  it('offers nothing to remove while nothing plain is on the order', () => {
    const row = mountRow(true, [])

    expect(row.find('.remove-one').exists()).toBe(false)
  })

  it('cannot be tapped once the item has sold out', () => {
    const row = mountRow(false, [])

    expect(row.get('.add').attributes('disabled')).toBeDefined()
    expect(row.get('.add-note').attributes('disabled')).toBeDefined()
  })

  it('says that a sold out item is sold out', () => {
    const row = mountRow(false, [])

    expect(row.get('.sold-out').text()).toBe('Ausverkauft')
  })
})

describe('writing a note', () => {
  it('asks for the note first and adds nothing yet', async () => {
    const row = mountRow(true, [])

    await row.get('.add-note').trigger('click')

    expect(document.querySelector('.note-dialog')).not.toBeNull()
    expect(row.emitted('add')).toBeUndefined()
    expect(row.emitted('addWithANote')).toBeUndefined()
  })

  it('names the item it is asking about', async () => {
    const row = mountRow(true, [])

    await row.get('.add-note').trigger('click')

    expect(document.querySelector('.note-dialog .title')?.textContent).toContain('Wasser')
  })

  it('adds one portion carrying the note once it is confirmed', async () => {
    const row = mountRow(true, [])

    await row.get('.add-note').trigger('click')
    await typeIntoDialog(row, 'ohne Eis')
    await confirmDialog(row)

    expect(row.emitted('addWithANote')).toEqual([['ohne Eis']])
  })

  it('adds nothing when the server backs out', async () => {
    const row = mountRow(true, [])

    await row.get('.add-note').trigger('click')
    ;(document.querySelector('.note-dialog .note-cancel') as HTMLElement).click()
    await row.vm.$nextTick()

    expect(row.emitted('addWithANote')).toBeUndefined()
    expect(document.querySelector('.v-overlay--active')).toBeNull()
  })

  it('refuses an empty note, because a note nobody wrote says nothing', async () => {
    const row = mountRow(true, [])

    await row.get('.add-note').trigger('click')

    expect(
      (document.querySelector('.note-dialog .note-confirm') as HTMLButtonElement).disabled,
    ).toBe(true)
  })
})

describe('the portions that carry a note', () => {
  it('stands under the item on a line of its own', () => {
    const row = mountRow(true, [plain(0), noted(1, 'ohne Eis')])

    const groups = row.findAll('.note-group')

    expect(groups).toHaveLength(1)
    expect(groups[0].get('.group-label').text()).toBe('ohne Eis')
    expect(groups[0].get('.group-count').text()).toBe('1')
  })

  it('counts portions carrying the same note on one line', () => {
    const row = mountRow(true, [noted(0, 'ohne Eis'), noted(1, 'ohne Eis')])

    expect(row.get('.note-group .group-count').text()).toBe('2')
  })

  it('adds another portion carrying that same note at that same station', async () => {
    const row = mountRow(true, [noted(0, 'ohne Eis')])

    await row.get('.note-group .group-add').trigger('click')

    expect(row.emitted('addLikeGroup')).toEqual([['ohne Eis', 'station-bar']])
  })

  it('takes the most recently added portion of that note off again', async () => {
    const row = mountRow(true, [noted(0, 'ohne Eis'), noted(3, 'ohne Eis')])

    await row.get('.note-group .group-remove').trigger('click')

    expect(row.emitted('removeOne')).toEqual([[3]])
  })

  it('reopens the note for correcting, filled in as it stands', async () => {
    const row = mountRow(true, [noted(0, 'ohne Eis'), noted(2, 'ohne Eis')])

    await row.get('.note-group .group-note').trigger('click')

    expect(dialogField().value).toBe('ohne Eis')

    await typeIntoDialog(row, 'ohne Eis, bitte kalt')
    await confirmDialog(row)

    expect(row.emitted('renameNote')).toEqual([[[0, 2], 'ohne Eis, bitte kalt']])
    expect(row.emitted('addWithANote')).toBeUndefined()
  })

  it('names the station on a line whose item two stations could prepare', () => {
    const row = mountRow(true, [
      { index: 0, note: null, hasAStationChoice: true, stationId: 'station-1', stationName: 'Bar innen' },
    ])

    expect(row.get('.note-group .group-label').text()).toBe('Ausgabestelle: Bar innen')
  })

  it('keeps the station visible on a line that also carries a note', () => {
    const row = mountRow(true, [
      {
        index: 0,
        note: 'ohne Eis',
        hasAStationChoice: true,
        stationId: 'station-1',
        stationName: 'Bar innen',
      },
    ])

    const label = row.get('.note-group .group-label').text()
    expect(label).toContain('Ausgabestelle: Bar innen')
    expect(label).toContain('ohne Eis')
  })

  it('adds another portion to a station-only group', async () => {
    const row = mountRow(true, [
      { index: 0, note: null, hasAStationChoice: true, stationId: 'station-bar', stationName: 'Bar innen' },
    ])

    await row.get('.note-group .group-add').trigger('click')

    expect(row.emitted('addLikeGroup')).toEqual([[null, 'station-bar']])
  })

  it('offers no plus on a sold out item', () => {
    const row = mountRow(false, [
      { index: 0, note: null, hasAStationChoice: true, stationId: 'station-bar', stationName: 'Bar innen' },
    ])

    expect(row.get('.note-group .group-add').attributes('disabled')).toBeDefined()
  })

  it('offers to change the station of that line', async () => {
    const row = mountRow(true, [
      { index: 0, note: null, hasAStationChoice: true, stationId: 'station-1', stationName: 'Bar innen' },
      { index: 1, note: null, hasAStationChoice: true, stationId: 'station-1', stationName: 'Bar innen' },
    ])

    await row.get('.note-group .group-station').trigger('click')

    expect(row.emitted('changeStation')).toEqual([[[0, 1]]])
  })

  it('offers no station control on an item only one station prepares', () => {
    const row = mountRow(true, [noted(0, 'ohne Eis')])

    expect(row.find('.group-station').exists()).toBe(false)
  })
})

describe('asking for a note on an item several stations could prepare', () => {
  it('sends the waiter to the station question instead of opening the note dialog', async () => {
    const row = mountRowForAnItemAtSeveralStations(true, [])

    await row.get('.add-note').trigger('click')

    expect(row.emitted('addWithANoteAtAStation')).toHaveLength(1)
    expect(document.querySelector('.note-dialog')).toBeNull()
  })
})

describe('the length of a note on one line', () => {
  it('stops where the laptop stops storing it', async () => {
    const row = mountRow(true, [])

    await row.get('.add-note').trigger('click')

    expect(dialogField().getAttribute('maxlength')).toBe('200')
  })
})

function mountRowWithEstimate(
  range: EstimateRange | null,
  locale: 'de' | 'en' = 'de',
  isAvailable = true,
) {
  const i18n = createI18n({ legacy: false, locale, messages: { de, en } })
  return mount(ItemRow, {
    props: { item: item(isAvailable), positions: [], language: locale, estimateRange: range },
    global: { plugins: [createVuetify(), i18n] },
    attachTo: document.body,
  })
}

describe('the waiting time written on an item row', () => {
  it('rides in the item name in German, short enough to leave the price its place', () => {
    const row = mountRowWithEstimate({ min: 6, max: 6 })

    expect(row.get('.name').text()).toBe('Wasser (~6 Min.)')
  })

  it('stays just as short in English', () => {
    const row = mountRowWithEstimate({ min: 6, max: 6 }, 'en')

    expect(row.get('.name').text()).toBe('Wasser (~6 min)')
  })

  it('says right away when there is nothing to wait for', () => {
    const row = mountRowWithEstimate({ min: 0, max: 0 })

    expect(row.get('.name').text()).toBe('Wasser (~0 Min.)')
  })

  it('names the span between the quickest and the slowest station', () => {
    const row = mountRowWithEstimate({ min: 10, max: 62 })

    expect(row.get('.name').text()).toBe('Wasser (~10 - 62 Min.)')
  })

  it('names the same span in English', () => {
    const row = mountRowWithEstimate({ min: 10, max: 62 }, 'en')

    expect(row.get('.name').text()).toBe('Wasser (~10 - 62 min)')
  })

  it('names the item alone when it carries no time of its own', () => {
    const row = mountRowWithEstimate(null)

    expect(row.get('.name').text()).toBe('Wasser')
  })

  it('names a sold out item alone, because nobody can order it and wait for it', () => {
    const row = mountRowWithEstimate({ min: 6, max: 6 }, 'de', false)

    expect(row.get('.name').text()).toBe('Wasser')
  })
})
