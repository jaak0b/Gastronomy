import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { answer, emptyAnswer, inTurn, refusal } from '../../../support/laptop'
import {
  dialogText,
  iconActionLabelsOnScreen,
  isOnScreen,
  onScreen,
  openDialog,
  pressInDialog,
  refusalReasonsOnScreen,
  textOnScreen,
  typeInto,
  waitForDialog,
  waitUntil,
} from '../../../support/dom'
import { festivalsLaptop, mountList, OVER, OVER_ROW, RUNNING, RUNNING_ROW } from './festivalsListFixture'

const NAME_DIALOG = '[data-test="form-dialog"]'

const NAME_FIELD = `${NAME_DIALOG} [data-test="confirm-delete-name"]`

const DELETE_BUTTON = `${NAME_DIALOG} [data-test="form-save"]`

async function pressTheTrashOfHerbstfest(): Promise<void> {
  onScreen(`${OVER_ROW} [data-test="delete"]`).click()
  await waitForDialog()
}

async function continueToTheNameDialog(): Promise<void> {
  await pressTheTrashOfHerbstfest()
  await pressInDialog('[data-test="confirm"]')
  await waitUntil(() => expect(isOnScreen(NAME_DIALOG)).toBe(true))
}

function deleteButtonIsOff(): boolean {
  return (onScreen(DELETE_BUTTON) as HTMLButtonElement).disabled
}

beforeEach(() => {
  setActivePinia(createPinia())
  localStorage.clear()
  document.body.innerHTML = ''
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('the first question before a festival is deleted', () => {
  it('names the trash "Fest löschen" when the admin points at it', async () => {
    festivalsLaptop([OVER])
    const list = mountList()
    await vi.waitFor(() => expect(list.find(`${OVER_ROW} [data-test="delete"]`).exists()).toBe(true))

    onScreen(`${OVER_ROW} [data-test="delete"]`).dispatchEvent(new MouseEvent('mouseenter'))

    await waitUntil(() => expect(iconActionLabelsOnScreen()).toContain('Fest löschen'))
  })

  it('names the festival and its data in German and sends nothing yet', async () => {
    const laptop = festivalsLaptop([OVER])
    const list = mountList()
    await vi.waitFor(() => expect(list.find(`${OVER_ROW} [data-test="delete"]`).exists()).toBe(true))

    await pressTheTrashOfHerbstfest()

    expect(dialogText('[data-test="confirm-title"]')).toBe('Fest löschen?')
    expect(dialogText('[data-test="confirm-body"]').trim()).toBe(
      'Das Fest „Herbstfest“ wird mit allen zugehörigen Daten endgültig gelöscht.',
    )
    expect(dialogText('[data-test="confirm"]').trim()).toBe('Weiter')
    expect(laptop.writes()).toEqual([])
  })

  it('asks in English', async () => {
    festivalsLaptop([OVER])
    const list = mountList('en')
    await vi.waitFor(() => expect(list.find(`${OVER_ROW} [data-test="delete"]`).exists()).toBe(true))

    await pressTheTrashOfHerbstfest()

    expect(dialogText('[data-test="confirm-title"]')).toBe('Delete festival?')
    expect(dialogText('[data-test="confirm-body"]').trim()).toBe(
      'The festival "Herbstfest" and all of its data will be deleted permanently.',
    )
    expect(dialogText('[data-test="confirm"]').trim()).toBe('Continue')
  })

  it('keeps the festival when the admin cancels it', async () => {
    const laptop = festivalsLaptop([OVER])
    const list = mountList()
    await vi.waitFor(() => expect(list.find(`${OVER_ROW} [data-test="delete"]`).exists()).toBe(true))
    await pressTheTrashOfHerbstfest()

    await pressInDialog('[data-test="cancel"]')

    await waitUntil(() => expect(openDialog()).toBeNull())
    expect(isOnScreen(NAME_DIALOG)).toBe(false)
    expect(laptop.writes()).toEqual([])
  })
})

describe('typing the name of the festival to confirm', () => {
  it('asks for the name in German, with Löschen switched off', async () => {
    festivalsLaptop([OVER])
    const list = mountList()
    await vi.waitFor(() => expect(list.find(`${OVER_ROW} [data-test="delete"]`).exists()).toBe(true))

    await continueToTheNameDialog()

    expect(textOnScreen(`${NAME_DIALOG} [data-test="form-dialog-title"]`)).toBe('Löschen bestätigen')
    expect(textOnScreen(`${NAME_DIALOG} [data-test="confirm-delete-body"]`).trim()).toBe(
      'Geben Sie zur Bestätigung den Namen des Festes ein.',
    )
    expect(textOnScreen(`${NAME_FIELD} label`)).toContain('Name des Festes')
    expect(textOnScreen(DELETE_BUTTON).trim()).toBe('Löschen')
    expect(deleteButtonIsOff()).toBe(true)
  })

  it('asks for the name in English', async () => {
    festivalsLaptop([OVER])
    const list = mountList('en')
    await vi.waitFor(() => expect(list.find(`${OVER_ROW} [data-test="delete"]`).exists()).toBe(true))

    await continueToTheNameDialog()

    expect(textOnScreen(`${NAME_DIALOG} [data-test="form-dialog-title"]`)).toBe('Confirm deletion')
    expect(textOnScreen(`${NAME_DIALOG} [data-test="confirm-delete-body"]`).trim()).toBe(
      "Type the festival's name to confirm.",
    )
    expect(textOnScreen(`${NAME_FIELD} label`)).toContain('Festival name')
    expect(textOnScreen(DELETE_BUTTON).trim()).toBe('Delete')
  })

  it('keeps Löschen switched off for the name written in other letter case', async () => {
    festivalsLaptop([OVER])
    const list = mountList()
    await vi.waitFor(() => expect(list.find(`${OVER_ROW} [data-test="delete"]`).exists()).toBe(true))
    await continueToTheNameDialog()

    typeInto(NAME_FIELD, 'herbstfest')

    await waitUntil(() => expect(deleteButtonIsOff()).toBe(true))
  })

  it('keeps Löschen switched off for a name that is only part of the festival name', async () => {
    festivalsLaptop([OVER])
    const list = mountList()
    await vi.waitFor(() => expect(list.find(`${OVER_ROW} [data-test="delete"]`).exists()).toBe(true))
    await continueToTheNameDialog()

    typeInto(NAME_FIELD, 'Herbst')

    await waitUntil(() => expect(deleteButtonIsOff()).toBe(true))
  })

  it('switches Löschen on for the exact name with spaces before and after it', async () => {
    festivalsLaptop([OVER])
    const list = mountList()
    await vi.waitFor(() => expect(list.find(`${OVER_ROW} [data-test="delete"]`).exists()).toBe(true))
    await continueToTheNameDialog()

    typeInto(NAME_FIELD, '  Herbstfest  ')

    await waitUntil(() => expect(deleteButtonIsOff()).toBe(false))
  })

  it('keeps the festival when the admin cancels the name dialog', async () => {
    const laptop = festivalsLaptop([OVER])
    const list = mountList()
    await vi.waitFor(() => expect(list.find(`${OVER_ROW} [data-test="delete"]`).exists()).toBe(true))
    await continueToTheNameDialog()
    typeInto(NAME_FIELD, 'Herbstfest')

    onScreen(`${NAME_DIALOG} [data-test="form-cancel"]`).click()

    await waitUntil(() => expect(isOnScreen(NAME_DIALOG)).toBe(false))
    expect(laptop.writes()).toEqual([])
  })
})

describe('deleting a festival that is over', () => {
  it('sends the deletion once the admin presses Löschen and drops the row the laptop no longer lists', async () => {
    const laptop = festivalsLaptop([OVER])
      .answers('GET', '/api/admin/festivals', inTurn(answer({ festivals: [OVER] }), answer({ festivals: [] })))
      .answers('DELETE', /./, emptyAnswer(204))
    const list = mountList()
    await vi.waitFor(() => expect(list.find(`${OVER_ROW} [data-test="delete"]`).exists()).toBe(true))
    await continueToTheNameDialog()
    typeInto(NAME_FIELD, 'Herbstfest')
    await waitUntil(() => expect(deleteButtonIsOff()).toBe(false))

    onScreen(DELETE_BUTTON).click()

    await waitUntil(() => expect(isOnScreen(NAME_DIALOG)).toBe(false))
    expect(laptop.writes().map((call) => `${call.method} ${call.url}`)).toEqual(['DELETE /api/admin/festivals/fest-2'])
    expect(list.find(OVER_ROW).exists()).toBe(false)
  })

  it('shows the reason inside the name dialog when the laptop refuses because the festival has become active', async () => {
    festivalsLaptop([OVER]).answers(
      'DELETE',
      /./,
      refusal('errors.admin.festivals.activeFestivalCannotBeDeleted', { status: 409, code: 'ActiveFestivalCannotBeDeleted' }),
    )
    const list = mountList()
    await vi.waitFor(() => expect(list.find(`${OVER_ROW} [data-test="delete"]`).exists()).toBe(true))
    await continueToTheNameDialog()
    typeInto(NAME_FIELD, 'Herbstfest')
    await waitUntil(() => expect(deleteButtonIsOff()).toBe(false))

    onScreen(DELETE_BUTTON).click()

    await waitUntil(() =>
      expect(textOnScreen(`${NAME_DIALOG} [data-test="refusal"]`)).toContain('Ein aktives Fest kann nicht gelöscht werden.'),
    )
  })

  it('shows the shared failure text inside the name dialog when the laptop gives no reason', async () => {
    festivalsLaptop([OVER]).answers('DELETE', /./, emptyAnswer(500))
    const list = mountList()
    await vi.waitFor(() => expect(list.find(`${OVER_ROW} [data-test="delete"]`).exists()).toBe(true))
    await continueToTheNameDialog()
    typeInto(NAME_FIELD, 'Herbstfest')
    await waitUntil(() => expect(deleteButtonIsOff()).toBe(false))

    onScreen(DELETE_BUTTON).click()

    await waitUntil(() =>
      expect(textOnScreen(`${NAME_DIALOG} [data-test="refusal"]`)).toContain('Die Aktion ist fehlgeschlagen.'),
    )
  })
})

describe('the trash of the festival that is running', () => {
  it('is switched off while the trash of a festival that is over is not', async () => {
    festivalsLaptop([RUNNING, OVER])
    const list = mountList()
    await vi.waitFor(() => expect(list.find(`${RUNNING_ROW} [data-test="delete"]`).exists()).toBe(true))

    expect((list.get(`${RUNNING_ROW} [data-test="delete"]`).element as HTMLButtonElement).disabled).toBe(true)
    expect((list.get(`${OVER_ROW} [data-test="delete"]`).element as HTMLButtonElement).disabled).toBe(false)
  })

  it('says why when the admin points at it', async () => {
    festivalsLaptop([RUNNING])
    const list = mountList()
    await vi.waitFor(() => expect(list.find(`${RUNNING_ROW} [data-test="delete-wrapper"]`).exists()).toBe(true))

    await list.get(`${RUNNING_ROW} [data-test="delete-wrapper"]`).trigger('mouseenter')

    await vi.waitFor(() => expect(refusalReasonsOnScreen()).toContain('Ein aktives Fest kann nicht gelöscht werden.'))
  })
})
