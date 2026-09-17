import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { enableAutoUnmount, mount } from '@vue/test-utils'
import { defineComponent, h, nextTick, ref } from 'vue'
import { createI18n } from 'vue-i18n'

import ItemRow from '../../../src/components/catalog/ItemRow.vue'
import { useKeyboardInset } from '../../../src/composables/useKeyboardInset'
import type { CatalogItem } from '../../../src/core/apiTypes'
import type { EstimateRange } from '../../../src/core/estimates'
import type { ItemPosition } from '../../../src/core/itemPositions'
import de from '../../../src/locales/de.json'
import en from '../../../src/locales/en.json'

enableAutoUnmount(afterEach)

function item(isAvailable: boolean): CatalogItem {
  return {
    id: 'item-wasser',
    name: 'Wasser',
    categoryId: 'category-getraenke',
    priceCents: 200,
    sortOrder: 1,
    isAvailable,
    stationIds: ['station-bar'],
    isQueueIndependent: false,
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
    global: { plugins: [i18n] },
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
    global: { plugins: [i18n] },
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

describe('the note dialog on a phone whose keyboard covers the lower screen', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
    document.body.innerHTML = ''
  })

  it('keeps the dialog above the keyboard', async () => {
    vi.stubGlobal('innerHeight', 800)
    vi.stubGlobal('visualViewport', theKeyboard(400))
    const row = mountRow(true, [])

    await row.get('.add-note').trigger('click')

    const overlay = document.querySelector('.v-overlay--active') as HTMLElement
    expect(overlay.style.height).toBe('calc(100% - 400px)')
    expect(overlay.style.bottom).toBe('auto')
  })
})

function theKeyboard(height: number, scale = 1) {
  return {
    height,
    scale,
    addEventListener: vi.fn(),
    removeEventListener: vi.fn(),
  }
}

const InsetProbe = defineComponent({
  setup() {
    const inset = useKeyboardInset()
    return () => h('div', { class: 'inset-probe' }, String(inset.value))
  },
})

function mountTwoProbes() {
  const firstIsThere = ref(true)
  const secondIsThere = ref(true)
  mount(
    defineComponent({
      setup() {
        return () =>
          h('div', [
            firstIsThere.value ? h(InsetProbe) : null,
            secondIsThere.value ? h(InsetProbe) : null,
          ])
      },
    }),
    { attachTo: document.body },
  )
  return { firstIsThere, secondIsThere }
}

describe('the keyboard inset every consumer shares', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
    document.body.innerHTML = ''
  })

  it('follows the visual viewport while the keyboard grows and shrinks', async () => {
    vi.stubGlobal('innerHeight', 800)
    const keyboard = theKeyboard(400)
    vi.stubGlobal('visualViewport', keyboard)
    mount(InsetProbe, { attachTo: document.body })
    await nextTick()

    expect(document.querySelector('.inset-probe')?.textContent).toBe('400')

    keyboard.height = 300
    ;(keyboard.addEventListener.mock.calls[0][1] as () => void)()
    await nextTick()

    expect(document.querySelector('.inset-probe')?.textContent).toBe('500')
  })

  it('takes no room while the waiter is zoomed in', async () => {
    vi.stubGlobal('innerHeight', 800)
    vi.stubGlobal('visualViewport', theKeyboard(400, 2))
    mount(InsetProbe, { attachTo: document.body })
    await nextTick()

    expect(document.querySelector('.inset-probe')?.textContent).toBe('0')
  })

  it('takes no room when the browser holds no visual viewport', async () => {
    vi.stubGlobal('innerHeight', 800)
    vi.stubGlobal('visualViewport', undefined)
    mount(InsetProbe, { attachTo: document.body })
    await nextTick()

    expect(document.querySelector('.inset-probe')?.textContent).toBe('0')
  })

  it('listens once while any consumer is mounted and stops with the last', async () => {
    vi.stubGlobal('innerHeight', 800)
    const keyboard = theKeyboard(400)
    vi.stubGlobal('visualViewport', keyboard)
    const probes = mountTwoProbes()

    expect(keyboard.addEventListener).toHaveBeenCalledTimes(1)
    expect(keyboard.addEventListener).toHaveBeenCalledWith('resize', expect.any(Function))

    probes.firstIsThere.value = false
    await nextTick()

    expect(keyboard.removeEventListener).not.toHaveBeenCalled()

    probes.secondIsThere.value = false
    await nextTick()

    expect(keyboard.removeEventListener).toHaveBeenCalledTimes(1)
    expect(keyboard.removeEventListener).toHaveBeenCalledWith(
      'resize',
      keyboard.addEventListener.mock.calls[0][1],
    )
  })
})

describe('the portions that carry a note', () => {
  it('stands under the item on a line of its own', () => {
    const row = mountRow(true, [plain(0), noted(1, 'ohne Eis')])

    const groups = row.findAll('.note-group')

    expect(groups).toHaveLength(1)
    expect(groups[0].get('.group-note').text()).toBe('Hinweis: ohne Eis')
    expect(groups[0].get('.group-count').text()).toBe('1')
  })

  it('labels the note in English too', () => {
    const row = mountRowWithEstimate(null, 'en', true, [noted(0, 'no ice')])

    expect(row.get('.group-note').text()).toBe('Note: no ice')
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
    expect(label).toContain('Hinweis: ohne Eis')
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
  positions: ItemPosition[] = [],
) {
  const i18n = createI18n({ legacy: false, locale, messages: { de, en } })
  return mount(ItemRow, {
    props: { item: item(isAvailable), positions, language: locale, estimateRange: range },
    global: { plugins: [i18n] },
    attachTo: document.body,
  })
}

describe('the waiting time written on an item row', () => {
  it('sits on the facts line under the name, beside the price', () => {
    const row = mountRowWithEstimate({ min: 6, max: 6 })

    expect(row.get('.name').text()).toBe('Wasser')
    expect(row.get('.facts .price').text()).toBe('2,00 €')
    expect(row.get('.facts .estimate').text()).toBe('~6 Min.')
  })

  it('keeps the facts line inside the add-one target, so one tap covers the whole block', () => {
    const row = mountRowWithEstimate({ min: 6, max: 6 })

    expect(row.get('.add').find('.facts').exists()).toBe(true)
  })

  it('writes just as short in English', () => {
    const row = mountRowWithEstimate({ min: 6, max: 6 }, 'en')

    expect(row.get('.name').text()).toBe('Wasser')
    expect(row.get('.estimate').text()).toBe('~6 min')
  })

  it('says right away when there is nothing to wait for', () => {
    const row = mountRowWithEstimate({ min: 0, max: 0 })

    expect(row.get('.estimate').text()).toBe('~0 Min.')
  })

  it('names the span between the quickest and the slowest station', () => {
    const row = mountRowWithEstimate({ min: 10, max: 62 })

    expect(row.get('.estimate').text()).toBe('~10 - 62 Min.')
  })

  it('names the same span in English', () => {
    const row = mountRowWithEstimate({ min: 10, max: 62 }, 'en')

    expect(row.get('.estimate').text()).toBe('~10 - 62 min')
  })

  it('writes the price alone when the item carries no time of its own', () => {
    const row = mountRowWithEstimate(null)

    expect(row.get('.name').text()).toBe('Wasser')
    expect(row.get('.facts .price').text()).toBe('2,00 €')
    expect(row.find('.estimate').exists()).toBe(false)
  })

  it('writes no time on a sold out item, because nobody can order it and wait for it', () => {
    const row = mountRowWithEstimate({ min: 6, max: 6 }, 'de', false)

    expect(row.get('.name').text()).toBe('Wasser')
    expect(row.find('.estimate').exists()).toBe(false)
    expect(row.get('.facts .price').text()).toBe('2,00 €')
    expect(row.get('.facts .sold-out').text()).toBe('Ausverkauft')
  })
})
