import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { enableAutoUnmount, flushPromises } from '@vue/test-utils'
import {
  allOnScreen,
  dialogText,
  inputOf,
  isOnScreen,
  onScreen,
  pressInDialog,
  refusalReasonsOnScreen,
  waitUntil,
} from '../../../support/dom'
import { centreOf, rightClickAt, shapeWithId, tapAt } from '../../../support/konvaDrawing'
import {
  HALL,
  HALL_WITH_TWO_TABLES,
  TABLE_A1,
  TABLE_A1_SHAPE_ID,
  TABLE_A2_SHAPE_ID,
  TENT,
  editorLaptop,
  mountEditor,
  theDrawing,
} from './tablePlanEditorFixture'

enableAutoUnmount(afterEach)

const STILL_OPEN =
  'Ein Tisch kann bei einem aktiven Fest erst ausgeblendet werden, wenn dort alles zubereitet und kassiert ist.'

const MENU_ENTRIES = '[data-test="table-plan-menu"] .v-list-item'

beforeEach(() => {
  setActivePinia(createPinia())
  document.body.innerHTML = ''
})

afterEach(() => {
  vi.unstubAllGlobals()
  vi.restoreAllMocks()
})

async function rightClickTheShape(id: string): Promise<void> {
  const stage = theDrawing()
  await rightClickAt(stage, centreOf(shapeWithId(stage, id)))
  await waitUntil(() => expect(isOnScreen('[data-test="table-plan-menu"]')).toBe(true))
}

function entryText(entry: HTMLElement): string {
  return entry.querySelector('.v-list-item-title')?.textContent?.trim() ?? ''
}

function entryIcon(entry: HTMLElement): string {
  const icon = entry.querySelector('.v-list-item__prepend .v-icon')
  return [...(icon?.classList ?? [])].find((name) => name.startsWith('mdi-')) ?? ''
}

function menuEntry(testName: string): HTMLElement {
  return onScreen(`[data-test="${testName}"]`)
}

describe('the context menu on a table', () => {
  it('shows Kopieren, Bearbeiten and Ausblenden, each with its icon before the text', async () => {
    editorLaptop()
    await mountEditor()

    await rightClickTheShape(TABLE_A1_SHAPE_ID)

    expect(allOnScreen(MENU_ENTRIES).map((entry) => [entryIcon(entry), entryText(entry)])).toEqual([
      ['mdi-content-copy', 'Kopieren'],
      ['mdi-pencil', 'Bearbeiten'],
      ['mdi-delete', 'Ausblenden'],
    ])
  })

  it('opens the dialog of the table from Bearbeiten', async () => {
    editorLaptop()
    await mountEditor()
    await rightClickTheShape(TABLE_A1_SHAPE_ID)

    menuEntry('edit-table-from-menu').click()
    await flushPromises()

    expect([dialogText('[data-test="form-dialog-title"]'), inputOf('[data-test="table-plan-name-field"]').value]).toEqual([
      'Tisch bearbeiten',
      'A1',
    ])
  })

  it('asks to hide the table from Ausblenden, as the toolbar does', async () => {
    const laptop = editorLaptop()
    await mountEditor()
    await rightClickTheShape(TABLE_A1_SHAPE_ID)
    menuEntry('remove-table-from-menu').click()

    await pressInDialog('[data-test="confirm"]')

    expect(laptop.writes()).toEqual([
      { url: '/api/admin/festivals/fest-1/tables/table-a1/hide', method: 'POST', body: undefined },
    ])
  })

  it('disables Ausblenden with the reason while the table still has items to hand out or settle', async () => {
    editorLaptop({ areas: [{ ...HALL, tables: [{ ...TABLE_A1, hasItemsToHandOutOrSettleAtTheRunningFestival: true }] }, TENT] })
    await mountEditor()
    await rightClickTheShape(TABLE_A1_SHAPE_ID)

    menuEntry('remove-table-from-menu-wrapper').dispatchEvent(new MouseEvent('mouseenter'))

    expect(menuEntry('remove-table-from-menu').classList.contains('v-list-item--disabled')).toBe(true)
    await waitUntil(() => expect(refusalReasonsOnScreen()).toContain(STILL_OPEN))
  })

  it('leaves Bearbeiten and Ausblenden out while several tables are selected', async () => {
    editorLaptop({ areas: [HALL_WITH_TWO_TABLES, TENT] })
    await mountEditor()
    await tapAt(theDrawing(), centreOf(shapeWithId(theDrawing(), TABLE_A1_SHAPE_ID)))
    await tapAt(theDrawing(), centreOf(shapeWithId(theDrawing(), TABLE_A2_SHAPE_ID)), { ctrlKey: true })

    await rightClickTheShape(TABLE_A1_SHAPE_ID)

    expect(allOnScreen(MENU_ENTRIES).map((entry) => entryText(entry))).toEqual(['Kopieren'])
  })
})

describe('the context menu on an empty spot', () => {
  it('shows Einfügen with its icon before the text', async () => {
    editorLaptop()
    await mountEditor()
    await tapAt(theDrawing(), centreOf(shapeWithId(theDrawing(), TABLE_A1_SHAPE_ID)))
    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'c', ctrlKey: true, bubbles: true, cancelable: true }))

    await rightClickAt(theDrawing(), { x: 456, y: 381 })
    await waitUntil(() => expect(isOnScreen('[data-test="paste-table"]')).toBe(true))

    expect(allOnScreen(MENU_ENTRIES).map((entry) => [entryIcon(entry), entryText(entry)])).toEqual([
      ['mdi-content-paste', 'Einfügen'],
    ])
  })
})
