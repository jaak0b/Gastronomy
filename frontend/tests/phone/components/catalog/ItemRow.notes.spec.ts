import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { enableAutoUnmount } from '@vue/test-utils'
import { nextTick } from 'vue'
import ItemNoteDialog from '../../../../src/phone/components/catalog/ItemNoteDialog.vue'
import { item, plain, noted, mountRow, mountRowForAnItemAtSeveralStations, dialogField, enterTheNote, confirmDialog, theKeyboard, mountRowWithEstimate, at } from './itemRowFixture'

enableAutoUnmount(afterEach)

beforeEach(() => {
  document.body.innerHTML = ''
})

describe('writing a note', () => {
  it('asks for the note first and adds nothing yet', async () => {
    const row = mountRow(true, [])

    await row.get('[data-test="add-note"]').trigger('click')

    expect(document.querySelector('[data-test="note-dialog"]')).not.toBeNull()
    expect(row.emitted('add')).toBeUndefined()
    expect(row.emitted('addWithANote')).toBeUndefined()
  })

  it('names the item it is asking about', async () => {
    const row = mountRow(true, [])

    await row.get('[data-test="add-note"]').trigger('click')

    expect(document.querySelector('[data-test="note-dialog-title"]')?.textContent).toContain('Wasser')
  })

  it('adds one portion carrying the note once it is confirmed', async () => {
    const row = mountRow(true, [])

    await row.get('[data-test="add-note"]').trigger('click')
    await enterTheNote('ohne Eis')
    await confirmDialog()

    expect(row.emitted('addWithANote')).toEqual([['ohne Eis']])
  })

  it('adds nothing when the server backs out', async () => {
    const row = mountRow(true, [])

    await row.get('[data-test="add-note"]').trigger('click')
    ;(document.querySelector('[data-test="note-dialog"] [data-test="note-cancel"]') as HTMLElement).click()
    await nextTick()

    expect(row.emitted('addWithANote')).toBeUndefined()
    expect(row.findComponent(ItemNoteDialog).props('isOpen')).toBe(false)
  })

  it('refuses an empty note, because a note nobody wrote says nothing', async () => {
    const row = mountRow(true, [])

    await row.get('[data-test="add-note"]').trigger('click')

    expect(
      (document.querySelector('[data-test="note-dialog"] [data-test="note-confirm"]') as HTMLButtonElement).disabled,
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

    await row.get('[data-test="add-note"]').trigger('click')

    const overlay = document.querySelector('[data-test="note-dialog-overlay"]') as HTMLElement
    expect(overlay.style.height).toBe('calc(100% - 400px)')
    expect(overlay.style.bottom).toBe('auto')
  })
})

describe('the portions that carry a note', () => {
  it('stands under the item on a line of its own', () => {
    const row = mountRow(true, [plain(0), noted(1, 'ohne Eis')])

    const groups = row.findAll('[data-test="note-group"]')

    expect(groups).toHaveLength(2)
    const notedGroups = groups.filter((entry) => entry.find('[data-test="group-note"]').exists())
    expect(notedGroups.map((entry) => entry.get('[data-test="group-note"]').text())).toEqual(['ohne Eis'])
    expect(notedGroups.map((entry) => entry.get('[data-test="group-count"]').text())).toEqual(['1'])
  })

  it('labels the note in English too', () => {
    const row = mountRowWithEstimate(null, 'en', true, [noted(0, 'no ice')])

    expect(row.get('[data-test="group-note"]').text()).toBe('no ice')
  })

  it('counts portions carrying the same note on one line', () => {
    const row = mountRow(true, [noted(0, 'ohne Eis'), noted(1, 'ohne Eis')])

    expect(row.get('[data-test="note-group"] [data-test="group-count"]').text()).toBe('2')
  })

  it('adds another portion carrying that same note at that same station', async () => {
    const row = mountRow(true, [noted(0, 'ohne Eis')])

    await row.get('[data-test="note-group"] [data-test="group-add"]').trigger('click')

    expect(row.emitted('addLikeGroup')).toEqual([['ohne Eis', 'station-bar']])
  })

  it('takes the most recently added portion of that note off again', async () => {
    const row = mountRow(true, [noted(0, 'ohne Eis'), noted(3, 'ohne Eis')])

    await row.get('[data-test="note-group"] [data-test="group-remove"]').trigger('click')

    expect(row.emitted('removeOne')).toEqual([[3]])
  })

  it('reopens the note for correcting, filled in as it stands', async () => {
    const row = mountRow(true, [noted(0, 'ohne Eis'), noted(2, 'ohne Eis')])

    await row.get('[data-test="note-group"] [data-test="group-note"]').trigger('click')

    expect(dialogField().value).toBe('ohne Eis')

    await enterTheNote('ohne Eis, bitte kalt')
    await confirmDialog()

    expect(row.emitted('renameNote')).toEqual([[[0, 2], 'ohne Eis, bitte kalt']])
    expect(row.emitted('addWithANote')).toBeUndefined()
  })

  it('names the station on a line whose item two stations could prepare', () => {
    const row = mountRow(true, [
      { index: 0, note: null, hasAStationChoice: true, stationId: 'station-1', stationName: 'Bar innen' },
    ])

    expect(row.get('[data-test="note-group"] [data-test="group-label"]').text()).toBe('Bar innen')
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

    const label = row.get('[data-test="note-group"] [data-test="group-label"]').text()
    expect(label).toContain('Bar innen')
    expect(label).toContain('ohne Eis')
  })

  it('adds another portion to a station-only group', async () => {
    const row = mountRow(true, [
      { index: 0, note: null, hasAStationChoice: true, stationId: 'station-bar', stationName: 'Bar innen' },
    ])

    await row.get('[data-test="note-group"] [data-test="group-add"]').trigger('click')

    expect(row.emitted('addLikeGroup')).toEqual([[null, 'station-bar']])
  })

  it('offers no plus on a sold out item', () => {
    const row = mountRow(false, [
      { index: 0, note: null, hasAStationChoice: true, stationId: 'station-bar', stationName: 'Bar innen' },
    ])

    expect(row.get('[data-test="note-group"] [data-test="group-add"]').attributes('disabled')).toBeDefined()
  })

  it('offers to change the station of that line', async () => {
    const row = mountRow(true, [
      { index: 0, note: null, hasAStationChoice: true, stationId: 'station-1', stationName: 'Bar innen' },
      { index: 1, note: null, hasAStationChoice: true, stationId: 'station-1', stationName: 'Bar innen' },
    ])

    await row.get('[data-test="note-group"] [data-test="group-station"]').trigger('click')

    expect(row.emitted('changeStation')).toEqual([[[0, 1]]])
  })

  it('offers no station control on an item only one station prepares', () => {
    const row = mountRow(true, [noted(0, 'ohne Eis')])

    expect(row.find('[data-test="group-station"]').exists()).toBe(false)
  })

  it('gives each station its own row when units of one article differ only by station', () => {
    const row = mountRow(true, [
      { index: 0, note: null, hasAStationChoice: true, stationId: 'station-1', stationName: 'Bar innen' },
      { index: 1, note: null, hasAStationChoice: true, stationId: 'station-2', stationName: 'Bar aussen' },
    ])

    expect(row.findAll('[data-test="note-group"]')).toHaveLength(2)
  })
})

describe('asking for a note on an item several stations could prepare', () => {
  it('sends the waiter to the station question instead of opening the note dialog', async () => {
    const row = mountRowForAnItemAtSeveralStations(true, [])

    await row.get('[data-test="add-note"]').trigger('click')

    expect(row.emitted('addWithANoteAtAStation')).toHaveLength(1)
    expect(document.querySelector('[data-test="note-dialog"]')).toBeNull()
  })
})

describe('the length of a note on one line', () => {
  it('stops where the laptop stops storing it', async () => {
    const row = mountRow(true, [])

    await row.get('[data-test="add-note"]').trigger('click')

    expect(dialogField().getAttribute('maxlength')).toBe('200')
  })
})
