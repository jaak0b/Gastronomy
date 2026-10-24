import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { enableAutoUnmount } from '@vue/test-utils'
import { allOnScreen, clickOn, onScreen, textOnScreen, waitUntil } from '../../../support/dom'
import { noConnection } from '../../../support/laptop'
import {
  SCHANK_SHAPE_ID,
  editorLaptop,
  mountEditor,
  selectTheShape,
} from './tablePlanEditorFixture'

enableAutoUnmount(afterEach)

const KITCHEN_ROW = '[data-test="station-to-place"][data-test-id="station-kueche"]'

beforeEach(() => {
  setActivePinia(createPinia())
  document.body.innerHTML = ''
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('the stations of the festival that are on no plan yet', () => {
  it('are listed beside the drawing, and a station placed in any area is not', async () => {
    editorLaptop()

    await mountEditor()

    expect(allOnScreen('[data-test="station-to-place"] [data-test="station-name"]').map((name) => name.textContent?.trim())).toEqual([
      'Küche',
    ])
  })

  it('are put in the selected area below its lowest shape', async () => {
    const laptop = editorLaptop()
    await mountEditor()

    await clickOn(`${KITCHEN_ROW} [data-test="place-station"]`)

    expect(laptop.writes()).toEqual([
      {
        url: '/api/admin/festivals/fest-1/stations/station-kueche/shape',
        method: 'PUT',
        body: { areaId: 'area-halle', geometry: { x: 10, y: 80, width: 120, height: 40, rotation: 0 } },
      },
    ])
  })

  it('show the reason under the station when the laptop cannot be reached', async () => {
    editorLaptop({ writeReply: noConnection() })
    await mountEditor()

    await clickOn(`${KITCHEN_ROW} [data-test="place-station"]`)

    await waitUntil(() =>
      expect(onScreen(`${KITCHEN_ROW} [data-test="refusal"]`).textContent?.trim()).toBe(
        'Die Aktion ist fehlgeschlagen. Versuchen Sie es noch einmal, sonst laden Sie die Seite neu.',
      ),
    )
  })
})

describe('a station shape the admin taps in the drawing', () => {
  it('shows the name of its station', async () => {
    editorLaptop()
    await mountEditor()

    await selectTheShape(SCHANK_SHAPE_ID)

    expect(textOnScreen('[data-test="shape-details"] [data-test="station-name"]').trim()).toBe('Schank')
  })

  it('is taken off the plan', async () => {
    const laptop = editorLaptop()
    await mountEditor()
    await selectTheShape(SCHANK_SHAPE_ID)

    await clickOn('[data-test="take-station-off-the-plan"]')

    expect(laptop.writes()).toEqual([
      { url: '/api/admin/festivals/fest-1/stations/station-schank/shape', method: 'DELETE', body: undefined },
    ])
  })
})
