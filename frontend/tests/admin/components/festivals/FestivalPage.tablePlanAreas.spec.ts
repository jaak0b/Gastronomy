import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { enableAutoUnmount, flushPromises } from '@vue/test-utils'
import {
  clickOn,
  dialogText,
  inputOf,
  isOnScreen,
  onScreen,
  pressInDialog,
  textOnScreen,
  typeIn,
  waitForDialog,
  waitUntil,
} from '../../../support/dom'
import { saveTheTablePlanNameDialog } from '../../../support/formDialogs'
import { refusal, type StubbedLaptop } from '../../../support/laptop'
import { FESTIVAL_ID, festivalLaptop, mountPage } from './festivalPageFixture'
import { AREAS_PATH, HALL, OLD_TABLES, TENT, areaRow } from './festivalTablePlanFixture'

enableAutoUnmount(afterEach)

const ACTION_FAILED = 'Die Aktion ist fehlgeschlagen. Versuchen Sie es noch einmal, sonst laden Sie die Seite neu.'

const AREA_NAME_TAKEN =
  'Ein Tischplan dieses Festes hat diesen Namen schon, egal ob er sichtbar oder ausgeblendet ist. Wählen Sie einen anderen Namen.'

async function openThePageWith(laptop: StubbedLaptop): Promise<StubbedLaptop> {
  mountPage()
  await vi.waitFor(() => expect(isOnScreen('[data-test="festival-area-row"]')).toBe(true))
  laptop.forgetCalls()
  return laptop
}

function writesTo(laptop: StubbedLaptop, path: string) {
  return laptop.writes().filter((write) => write.url.startsWith(path))
}

function refusalOnTheRowOf(areaId: string): string | undefined {
  return areaRow(areaId).querySelector('[data-test="refusal"]')?.textContent?.trim()
}

beforeEach(() => {
  setActivePinia(createPinia())
  window.history.replaceState({}, '', `/admin/festivals/${FESTIVAL_ID}`)
  document.body.innerHTML = ''
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('a new area on the festival page', () => {
  it('opens an empty dialog for a new table plan', async () => {
    await openThePageWith(festivalLaptop({ areas: [HALL, TENT] }))

    await clickOn('[data-test="new-entry"]', onScreen('[data-test="festival-table-plan"]'))
    await flushPromises()

    expect([dialogText('[data-test="form-dialog-title"]'), inputOf('[data-test="table-plan-name-field"]').value]).toEqual([
      'Neuer Tischplan',
      '',
    ])
  })

  it('takes a name of at most forty characters, like the name of a station', async () => {
    await openThePageWith(festivalLaptop({ areas: [HALL] }))

    await clickOn('[data-test="new-entry"]', onScreen('[data-test="festival-table-plan"]'))
    await flushPromises()

    expect(inputOf('[data-test="table-plan-name-field"]').getAttribute('maxlength')).toBe('40')
  })

  it('sends only the typed name, because the laptop puts a new area last', async () => {
    const laptop = await openThePageWith(festivalLaptop({ areas: [HALL, TENT, OLD_TABLES] }))
    await clickOn('[data-test="new-entry"]', onScreen('[data-test="festival-table-plan"]'))
    await flushPromises()

    await saveTheTablePlanNameDialog('Garten')

    expect(writesTo(laptop, AREAS_PATH)).toEqual([
      { url: AREAS_PATH, method: 'POST', body: { name: 'Garten' } },
    ])
  })

  it('cannot be saved while the name is blank', async () => {
    await openThePageWith(festivalLaptop({ areas: [HALL] }))
    await clickOn('[data-test="new-entry"]', onScreen('[data-test="festival-table-plan"]'))
    await flushPromises()

    await typeIn('[data-test="table-plan-name-field"]', '   ')

    expect(onScreen('[data-test="form-save"]').hasAttribute('disabled')).toBe(true)
  })

  it('shows the reason in the dialog when another area has the name', async () => {
    await openThePageWith(
      festivalLaptop({ areas: [HALL] }).answers('POST', AREAS_PATH, refusal('errors.admin.festivals.areaNameTaken')),
    )
    await clickOn('[data-test="new-entry"]', onScreen('[data-test="festival-table-plan"]'))
    await flushPromises()

    await saveTheTablePlanNameDialog('halle')

    await waitUntil(() =>
      expect(textOnScreen('[data-test="form-dialog"] [data-test="refusal"]').trim()).toBe(AREA_NAME_TAKEN),
    )
  })
})

describe('editing an area on the festival page', () => {
  it('opens the dialog with the name the laptop keeps', async () => {
    await openThePageWith(festivalLaptop({ areas: [HALL, TENT] }))

    await clickOn('[data-test="edit-entry"]', areaRow('area-zelt'))
    await flushPromises()

    expect([dialogText('[data-test="form-dialog-title"]'), inputOf('[data-test="table-plan-name-field"]').value]).toEqual([
      'Tischplan bearbeiten',
      'Zelt',
    ])
  })

  it('sends only the new name, because the laptop keeps the area in its place', async () => {
    const laptop = await openThePageWith(festivalLaptop({ areas: [HALL, TENT] }))
    await clickOn('[data-test="edit-entry"]', areaRow('area-zelt'))
    await flushPromises()

    await saveTheTablePlanNameDialog('Großes Zelt')

    expect(writesTo(laptop, AREAS_PATH)).toEqual([
      { url: `${AREAS_PATH}/area-zelt`, method: 'PUT', body: { name: 'Großes Zelt' } },
    ])
  })

  it('keeps the dialog open with the reason when another area has the name', async () => {
    await openThePageWith(
      festivalLaptop({ areas: [HALL, TENT] }).answers(
        'PUT',
        `${AREAS_PATH}/area-zelt`,
        refusal('errors.admin.festivals.areaNameTaken'),
      ),
    )
    await clickOn('[data-test="edit-entry"]', areaRow('area-zelt'))
    await flushPromises()

    await saveTheTablePlanNameDialog('Halle')

    await waitUntil(() =>
      expect(textOnScreen('[data-test="form-dialog"] [data-test="refusal"]').trim()).toBe(AREA_NAME_TAKEN),
    )
    expect(inputOf('[data-test="table-plan-name-field"]').value).toBe('Halle')
  })
})

describe('moving an area on the festival page', () => {
  it('moves it up at its own address the moment the arrow is pressed', async () => {
    const laptop = await openThePageWith(festivalLaptop({ areas: [HALL, TENT] }))

    await clickOn('[data-test="move-up"]', areaRow('area-zelt'))

    expect(writesTo(laptop, AREAS_PATH)).toEqual([
      { url: `${AREAS_PATH}/area-zelt/move`, method: 'POST', body: { direction: 'up', showsHiddenAreas: false } },
    ])
  })

  it('moves it down at its own address the moment the arrow is pressed', async () => {
    const laptop = await openThePageWith(festivalLaptop({ areas: [HALL, TENT] }))

    await clickOn('[data-test="move-down"]', areaRow('area-halle'))

    expect(writesTo(laptop, AREAS_PATH)).toEqual([
      { url: `${AREAS_PATH}/area-halle/move`, method: 'POST', body: { direction: 'down', showsHiddenAreas: false } },
    ])
  })

  it('tells the laptop that hidden areas are on screen once the box is ticked, so the move counts them', async () => {
    const laptop = await openThePageWith(festivalLaptop({ areas: [HALL, OLD_TABLES, TENT] }))
    await clickOn('[data-test="show-hidden-entries"] input')

    await clickOn('[data-test="move-up"]', areaRow('area-zelt'))

    expect(writesTo(laptop, AREAS_PATH)).toEqual([
      { url: `${AREAS_PATH}/area-zelt/move`, method: 'POST', body: { direction: 'up', showsHiddenAreas: true } },
    ])
  })

  it('shows the reason on the row when the laptop refuses the move', async () => {
    await openThePageWith(
      festivalLaptop({ areas: [HALL, TENT] }).answers(
        'POST',
        `${AREAS_PATH}/area-zelt/move`,
        refusal('errors.admin.actionFailed', { status: 404, code: 'NotFound' }),
      ),
    )

    await clickOn('[data-test="move-up"]', areaRow('area-zelt'))

    await waitUntil(() => expect(refusalOnTheRowOf('area-zelt')).toBe(ACTION_FAILED))
  })
})

describe('hiding and showing an area', () => {
  it('asks first, as the festival list does, and says what follows', async () => {
    const laptop = await openThePageWith(festivalLaptop({ areas: [HALL, TENT] }))

    await clickOn('[data-test="hide-entry"]', areaRow('area-zelt'))
    await waitForDialog()

    expect([dialogText('[data-test="confirm-title"]'), dialogText('[data-test="confirm-body"]')]).toEqual([
      'Tischplan ausblenden',
      'Die Kellner sehen diesen Tischplan dann nicht mehr.',
    ])
    expect(writesTo(laptop, AREAS_PATH)).toEqual([])
  })

  it('hides the area once the admin confirms', async () => {
    const laptop = await openThePageWith(festivalLaptop({ areas: [HALL, TENT] }))
    await clickOn('[data-test="hide-entry"]', areaRow('area-zelt'))

    await pressInDialog('[data-test="confirm"]')

    expect(writesTo(laptop, AREAS_PATH)).toEqual([
      { url: `${AREAS_PATH}/area-zelt/hide`, method: 'POST', body: undefined },
    ])
  })

  it('shows the reason on the row when the laptop still refuses to hide it', async () => {
    await openThePageWith(
      festivalLaptop({ areas: [HALL, TENT] }).answers(
        'POST',
        `${AREAS_PATH}/area-zelt/hide`,
        refusal('errors.admin.festivals.areaWithItemsToHandOutOrSettleStaysShown'),
      ),
    )
    await clickOn('[data-test="hide-entry"]', areaRow('area-zelt'))

    await pressInDialog('[data-test="confirm"]')

    await waitUntil(() =>
      expect(refusalOnTheRowOf('area-zelt')).toBe(
        'Ein Tischplan kann bei einem aktiven Fest erst ausgeblendet werden, wenn an seinen Tischen alles zubereitet und kassiert ist.',
      ),
    )
  })

  it('shows a hidden area again without asking', async () => {
    const laptop = await openThePageWith(festivalLaptop({ areas: [HALL, OLD_TABLES] }))
    await clickOn('[data-test="show-hidden-entries"] input')

    await clickOn('[data-test="show-entry"]', areaRow('area-alt'))

    expect(writesTo(laptop, AREAS_PATH)).toEqual([
      { url: `${AREAS_PATH}/area-alt/show`, method: 'POST', body: undefined },
    ])
  })
})
