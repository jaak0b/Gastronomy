import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { enableAutoUnmount, mount } from '@vue/test-utils'
import { defineComponent, h, nextTick, ref } from 'vue'

import { testPlugins } from '../../../support/plugins'
import ItemRow from '../../../../src/phone/components/catalog/ItemRow.vue'
import { useKeyboardInset } from '../../../../src/phone/composables/useKeyboardInset'
import { CatalogItemView } from '../../../../src/shared/api/generatedSchemas'
import type { EstimateRange } from '../../../../src/phone/core/estimates'
import type { ItemPosition } from '../../../../src/phone/core/itemPositions'

enableAutoUnmount(afterEach)

function item(isAvailable: boolean): CatalogItemView {
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

function stationNameFor(stationId: string): string {
  return stationId === 'station-bar' ? 'Theke' : stationId
}

function mountRow(isAvailable: boolean, positions: ItemPosition[]) {
  return mount(ItemRow, {
    props: {
      item: item(isAvailable),
      positions,
      language: 'de' as const,
      estimateRange: null,
      stationNameFor,
    },
    global: { plugins: testPlugins('de') },
    attachTo: document.body,
  })
}

function mountRowForAnItemAtSeveralStations(isAvailable: boolean, positions: ItemPosition[]) {
  return mount(ItemRow, {
    props: {
      item: { ...item(isAvailable), stationIds: ['station-kueche', 'station-bar'] },
      positions,
      language: 'de' as const,
      estimateRange: null,
      stationNameFor,
    },
    global: { plugins: testPlugins('de') },
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
    expect(row.get('.unit-price').text()).toBe('2,00 €')
  })

  it('adds one of the item and opens nothing when the name is tapped', async () => {
    const row = mountRow(true, [])

    await row.get('.add').trigger('click')

    expect(row.emitted('add')).toHaveLength(1)
    expect(document.querySelector('.note-dialog')).toBeNull()
  })

  it('puts the plain portions and the noted portion each on a row of their own', () => {
    const row = mountRow(true, [plain(0), plain(1), noted(2, 'ohne Eis')])

    const rows = row.findAll('.note-group')
    expect(rows).toHaveLength(2)
    expect(rows[0].get('.group-count').text()).toBe('2')
    expect(rows[0].get('.group-label').text()).toBe('Theke')
  })

  it('takes the most recently added plain portion off again', async () => {
    const row = mountRow(true, [plain(0), plain(4)])

    await row.get('.note-group .group-remove').trigger('click')

    expect(row.emitted('removeOne')).toEqual([[4]])
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

describe('the note control on an article with nothing ordered yet', () => {
  it('sits inside the header and renders no rows area', () => {
    const row = mountRow(true, [])

    expect(row.get('.item-head').find('.add-note').exists()).toBe(true)
    expect(row.find('.note-group').exists()).toBe(false)
  })
})

describe('the note button placement once rows are on the order', () => {
  it('stays in the header for Schnitzel, an item with several rows underneath', () => {
    const row = mount(ItemRow, {
      props: {
        item: schnitzel(),
        positions: [at(0, 'station-schank', 'Schank'), at(1, 'station-kueche', 'Küche')],
        language: 'de' as const,
        estimateRange: null,
        stationNameFor,
      },
      global: { plugins: testPlugins('de') },
      attachTo: document.body,
    })

    expect(row.get('.item-head').find('.add-note').exists()).toBe(true)
  })
})

describe('the default station shown on a plain row', () => {
  function wurstel(): CatalogItemView {
    return {
      id: 'item-wurstel',
      name: 'Würstel',
      categoryId: 'category-essen',
      priceCents: 250,
      sortOrder: 1,
      isAvailable: true,
      stationIds: ['station-bar'],
      isQueueIndependent: false,
    }
  }

  it('names the station the plain-only item is routed to, on its one row', () => {
    const positions: ItemPosition[] = Array.from({ length: 15 }, (_unused, index) => ({
      index,
      note: null,
      hasAStationChoice: false,
      stationId: 'station-bar',
      stationName: null,
    }))
    const row = mount(ItemRow, {
      props: {
        item: wurstel(),
        positions,
        language: 'de' as const,
        estimateRange: null,
        stationNameFor,
      },
      global: { plugins: testPlugins('de') },
      attachTo: document.body,
    })

    const rows = row.findAll('.note-group')
    expect(rows).toHaveLength(1)
    expect(rows[0].get('.group-count').text()).toBe('15')
    expect(rows[0].get('.group-station-fixed').text()).toBe('Theke')
    expect(rows[0].find('.group-remove').exists()).toBe(true)
    expect(rows[0].find('.group-add').exists()).toBe(true)
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

describe('the keyboard inset a screen measures', () => {
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

  it('gives every screen its own listener and takes it back when that screen goes', async () => {
    vi.stubGlobal('innerHeight', 800)
    const keyboard = theKeyboard(400)
    vi.stubGlobal('visualViewport', keyboard)
    const probes = mountTwoProbes()

    expect(keyboard.addEventListener).toHaveBeenCalledTimes(2)
    expect(keyboard.addEventListener).toHaveBeenCalledWith('resize', expect.any(Function))

    probes.firstIsThere.value = false
    await nextTick()

    expect(keyboard.removeEventListener).toHaveBeenCalledTimes(1)
    expect(keyboard.removeEventListener).toHaveBeenCalledWith(
      'resize',
      keyboard.addEventListener.mock.calls[0][1],
    )

    probes.secondIsThere.value = false
    await nextTick()

    expect(keyboard.removeEventListener).toHaveBeenCalledTimes(2)
    expect(keyboard.removeEventListener).toHaveBeenCalledWith(
      'resize',
      keyboard.addEventListener.mock.calls[1][1],
    )
  })

  it('shows the same inset in two screens measuring the same viewport', async () => {
    vi.stubGlobal('innerHeight', 800)
    const keyboard = theKeyboard(400)
    vi.stubGlobal('visualViewport', keyboard)
    mountTwoProbes()
    await nextTick()

    const probes = document.querySelectorAll('.inset-probe')

    expect(probes).toHaveLength(2)
    expect(probes[0].textContent).toBe('400')
    expect(probes[1].textContent).toBe('400')
  })
})

describe('the portions that carry a note', () => {
  it('stands under the item on a line of its own', () => {
    const row = mountRow(true, [plain(0), noted(1, 'ohne Eis')])

    const groups = row.findAll('.note-group')

    expect(groups).toHaveLength(2)
    expect(groups[1].get('.group-note').text()).toBe('ohne Eis')
    expect(groups[1].get('.group-count').text()).toBe('1')
  })

  it('labels the note in English too', () => {
    const row = mountRowWithEstimate(null, 'en', true, [noted(0, 'no ice')])

    expect(row.get('.group-note').text()).toBe('no ice')
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

    expect(row.get('.note-group .group-label').text()).toBe('Bar innen')
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
    expect(label).toContain('Bar innen')
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

  it('gives each station its own row when units of one article differ only by station', () => {
    const row = mountRow(true, [
      { index: 0, note: null, hasAStationChoice: true, stationId: 'station-1', stationName: 'Bar innen' },
      { index: 1, note: null, hasAStationChoice: true, stationId: 'station-2', stationName: 'Bar aussen' },
    ])

    expect(row.findAll('.note-group')).toHaveLength(2)
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
  return mount(ItemRow, {
    props: {
      item: item(isAvailable),
      positions,
      language: locale,
      estimateRange: range,
      stationNameFor,
    },
    global: { plugins: testPlugins(locale) },
    attachTo: document.body,
  })
}

describe('the waiting time written on an item row', () => {
  it('sits on the facts line under the name, the price beside the name', () => {
    const row = mountRowWithEstimate({ min: 6, max: 6 })

    expect(row.get('.name').text()).toBe('Wasser')
    expect(row.get('.unit-price').text()).toBe('2,00 €')
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
    expect(row.get('.unit-price').text()).toBe('2,00 €')
    expect(row.find('.estimate').exists()).toBe(false)
  })

  it('writes no time on a sold out item, because nobody can order it and wait for it', () => {
    const row = mountRowWithEstimate({ min: 6, max: 6 }, 'de', false)

    expect(row.get('.name').text()).toBe('Wasser')
    expect(row.find('.estimate').exists()).toBe(false)
    expect(row.get('.unit-price').text()).toBe('2,00 €')
    expect(row.get('.facts .sold-out').text()).toBe('Ausverkauft')
  })
})

function schnitzel(): CatalogItemView {
  return {
    id: 'item-schnitzel',
    name: 'Schnitzel',
    categoryId: 'category-essen',
    priceCents: 300,
    sortOrder: 1,
    isAvailable: true,
    stationIds: ['station-schank', 'station-kueche'],
    isQueueIndependent: false,
  }
}

function at(index: number, stationId: string, stationName: string, note: string | null = null): ItemPosition {
  return { index, note, hasAStationChoice: true, stationId, stationName }
}

describe('the counts and totals on an article', () => {
  it('sums every row on the header and prices each row on its own', () => {
    const row = mount(ItemRow, {
      props: {
        item: schnitzel(),
        positions: [
          at(0, 'station-schank', 'Schank'),
          at(1, 'station-schank', 'Schank'),
          at(2, 'station-kueche', 'Küche'),
          at(3, 'station-schank', 'Schank'),
          at(4, 'station-schank', 'Schank'),
          at(5, 'station-schank', 'Schank', 'ABCD'),
          at(6, 'station-schank', 'Schank'),
        ],
        language: 'en' as const,
        estimateRange: null,
        stationNameFor,
      },
      global: { plugins: testPlugins('en') },
      attachTo: document.body,
    })

    expect(row.get('.unit-price').text()).toBe('7 × €3.00')
    expect(row.get('.article-total').text()).toBe('€21.00')
    const rows = row.findAll('.note-group')
    expect(rows.map((entry) => entry.get('.group-count').text())).toEqual(['5', '1', '1'])
    expect(rows.map((entry) => entry.get('.group-station').text())).toEqual(['Schank', 'Küche', 'Schank'])
    expect(rows[2].get('.group-station').text()).toBe('Schank')
    expect(rows[2].get('.group-note').text()).toBe('ABCD')
  })

  it('puts every plain portion on the one row the header total still sums up', () => {
    const row = mount(ItemRow, {
      props: {
        item: { ...item(true), priceCents: 400 },
        positions: [plain(0), plain(1), plain(2)],
        language: 'en' as const,
        estimateRange: null,
        stationNameFor,
      },
      global: { plugins: testPlugins('en') },
      attachTo: document.body,
    })

    expect(row.get('.unit-price').text()).toBe('3 × €4.00')
    expect(row.get('.article-total').text()).toBe('€12.00')
    const rows = row.findAll('.note-group')
    expect(rows).toHaveLength(1)
    expect(rows[0].get('.group-count').text()).toBe('3')
    expect(rows[0].get('.group-station-fixed').text()).toBe('Theke')
  })

  it('shows the plain unit price and no total while the article is not on the order', () => {
    const row = mountRow(true, [])

    expect(row.get('.unit-price').text()).toBe('2,00 €')
    expect(row.find('.article-total').exists()).toBe(false)
  })
})
