import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { refusal } from '../../../support/laptop'
import {
  dialogText,
  isOnScreen,
  onScreen,
  openDialog,
  pressInDialog,
  refusalReasonsOnScreen,
  waitForDialog,
} from '../../../support/dom'
import {
  hover,
  ingredientIdsOfRowsHolding,
  ingredientRow,
  loadIngredientsAndMountDialog,
  recipeLaptop,
} from './itemIngredientsDialogFixture'

const MILK_ROW = ingredientRow('ingredient-milch')

const CREAM_ROW = ingredientRow('ingredient-sahne')

async function pressTheTrashOfMilk(): Promise<void> {
  onScreen(`${MILK_ROW} [data-test="delete-ingredient"]`).click()
  await waitForDialog()
}

beforeEach(() => {
  setActivePinia(createPinia())
  document.body.innerHTML = ''
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('deleting an ingredient no article uses', () => {
  it('asks first in German, naming the ingredient, and sends nothing yet', async () => {
    const laptop = recipeLaptop()
    await loadIngredientsAndMountDialog()

    await pressTheTrashOfMilk()

    expect(dialogText('[data-test="confirm-title"]')).toBe('Zutat löschen?')
    expect(dialogText('[data-test="confirm-body"]').trim()).toBe('Die Zutat Milch wird endgültig gelöscht.')
    expect(dialogText('[data-test="confirm"]').trim()).toBe('Zutat löschen')
    expect(laptop.writes()).toEqual([])
  })

  it('asks in English', async () => {
    recipeLaptop()
    await loadIngredientsAndMountDialog('en')

    await pressTheTrashOfMilk()

    expect(dialogText('[data-test="confirm-title"]')).toBe('Delete this ingredient?')
    expect(dialogText('[data-test="confirm-body"]').trim()).toBe('The ingredient Milch is deleted permanently.')
    expect(dialogText('[data-test="confirm"]').trim()).toBe('Delete ingredient')
  })

  it('deletes it once the admin confirms', async () => {
    const laptop = recipeLaptop()
    await loadIngredientsAndMountDialog()
    await pressTheTrashOfMilk()

    await pressInDialog('[data-test="confirm"]')

    await vi.waitFor(() =>
      expect(laptop.writes().map((call) => `${call.method} ${call.url}`)).toEqual([
        'DELETE /api/admin/ingredients/ingredient-milch',
      ]),
    )
  })

  it('keeps it when the admin cancels the question', async () => {
    const laptop = recipeLaptop()
    await loadIngredientsAndMountDialog()
    await pressTheTrashOfMilk()

    await pressInDialog('[data-test="cancel"]')

    await vi.waitFor(() => expect(openDialog()).toBeNull())
    expect(laptop.writes()).toEqual([])
  })

  it('names under that row why the laptop refused the delete', async () => {
    recipeLaptop().answers('DELETE', /ingredients\/ingredient-milch$/, refusal('common.errors.actionFailed', { status: 409 }))
    await loadIngredientsAndMountDialog()
    await pressTheTrashOfMilk()

    await pressInDialog('[data-test="confirm"]')

    await vi.waitFor(() =>
      expect(onScreen(`${MILK_ROW} [data-test="refusal"]`).textContent?.trim()).toBe('Die Aktion ist fehlgeschlagen.'),
    )
  })

  it('stops naming an earlier refusal under that row once the admin asks to delete again', async () => {
    recipeLaptop().answers('DELETE', /ingredients\/ingredient-milch$/, refusal('common.errors.actionFailed', { status: 409 }))
    await loadIngredientsAndMountDialog()
    await pressTheTrashOfMilk()
    await pressInDialog('[data-test="confirm"]')
    await vi.waitFor(() => expect(isOnScreen(`${MILK_ROW} [data-test="refusal"]`)).toBe(true))

    await pressTheTrashOfMilk()

    expect(isOnScreen(`${MILK_ROW} [data-test="refusal"]`)).toBe(false)
  })
})

describe('an ingredient an article uses', () => {
  it('offers the trash disabled, whether this recipe or another one uses it', async () => {
    recipeLaptop()

    await loadIngredientsAndMountDialog()

    expect(ingredientIdsOfRowsHolding('[data-test="delete-ingredient"]:disabled')).toEqual([
      'ingredient-broetchen',
      'ingredient-mehl',
      'ingredient-sahne',
    ])
  })

  it('names the articles in German', async () => {
    recipeLaptop()
    await loadIngredientsAndMountDialog()

    await hover(`${ingredientRow('ingredient-broetchen')} [data-test="delete-ingredient-wrapper"]`)

    await vi.waitFor(() =>
      expect(refusalReasonsOnScreen()).toContain('Die Zutat wird noch bei den Artikeln Bratwurst, Pfannkuchen verwendet.'),
    )
  })

  it('names the articles in English', async () => {
    recipeLaptop()
    await loadIngredientsAndMountDialog('en')

    await hover(`${CREAM_ROW} [data-test="delete-ingredient-wrapper"]`)

    await vi.waitFor(() =>
      expect(refusalReasonsOnScreen()).toContain('The ingredient is still used by the items Kaiserschmarrn.'),
    )
  })
})
