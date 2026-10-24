import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import type { AdminIngredientView } from '../../../../src/shared/api/generatedSchemas'
import { pressInDialog, refusalReasonsOnScreen, waitForDialog } from '../../../support/dom'
import { refusal } from '../../../support/laptop'
import { BUN, MUSTARD, OLD_MILK, ingredientsListLaptop, mountList } from './ingredientsListFixture'

const MUSTARD_ROW = `[data-test="ingredient-row"][data-test-id="${MUSTARD.ingredientId}"]`

const BUN_ROW = `[data-test="ingredient-row"][data-test-id="${BUN.ingredientId}"]`

const MUSTARD_IN_TWO_ACTIVE_ARTICLES: AdminIngredientView = { ...MUSTARD, activeItemNames: ['Bratwurst', 'Currywurst'] }

beforeEach(() => {
  setActivePinia(createPinia())
  document.body.innerHTML = ''
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('deleting an ingredient', () => {
  it('asks before it happens', async () => {
    const laptop = ingredientsListLaptop()
    const list = mountList()
    await vi.waitFor(() => expect(list.find(MUSTARD_ROW).exists()).toBe(true))

    await list.get(`${MUSTARD_ROW} [data-test="deactivate"]`).trigger('click')

    await waitForDialog()
    expect(document.querySelector('[data-test="confirm-title"]')?.textContent).toBe('Zutat deaktivieren?')
    expect(laptop.writes()).toEqual([])
  })

  it('deactivates it once the question is answered with yes', async () => {
    const laptop = ingredientsListLaptop()
    const list = mountList()
    await vi.waitFor(() => expect(list.find(MUSTARD_ROW).exists()).toBe(true))
    await list.get(`${MUSTARD_ROW} [data-test="deactivate"]`).trigger('click')

    await pressInDialog('[data-test="confirm"]')

    await vi.waitFor(() =>
      expect(laptop.writes().map((call) => `${call.method} ${call.url}`)).toEqual([
        'POST /api/admin/ingredients/ingredient-senf/deactivate',
      ]),
    )
  })

  it('does nothing when the question is answered with no', async () => {
    const laptop = ingredientsListLaptop()
    const list = mountList()
    await vi.waitFor(() => expect(list.find(MUSTARD_ROW).exists()).toBe(true))
    await list.get(`${MUSTARD_ROW} [data-test="deactivate"]`).trigger('click')

    await pressInDialog('[data-test="cancel"]')

    expect(laptop.writes()).toEqual([])
  })

  it('shows on the page why the laptop refused it', async () => {
    ingredientsListLaptop({ writes: refusal('errors.admin.actionFailed') })
    const list = mountList()
    await vi.waitFor(() => expect(list.find(MUSTARD_ROW).exists()).toBe(true))
    await list.get(`${MUSTARD_ROW} [data-test="deactivate"]`).trigger('click')

    await pressInDialog('[data-test="confirm"]')

    await vi.waitFor(() =>
      expect(list.get('[data-test="refusal"]').text()).toBe(
        'Die Aktion ist fehlgeschlagen. Versuchen Sie es noch einmal, sonst laden Sie die Seite neu.',
      ),
    )
  })
})

describe('an ingredient an active article still uses', () => {
  it('cannot be deleted while another ingredient can', async () => {
    ingredientsListLaptop({ ingredients: [BUN, MUSTARD_IN_TWO_ACTIVE_ARTICLES] })

    const list = mountList()
    await vi.waitFor(() =>
      expect((list.get(`${MUSTARD_ROW} [data-test="deactivate"]`).element as HTMLButtonElement).disabled).toBe(true),
    )

    expect((list.get(`${BUN_ROW} [data-test="deactivate"]`).element as HTMLButtonElement).disabled).toBe(false)
  })

  it('names the active articles the laptop lists for it', async () => {
    ingredientsListLaptop({ ingredients: [BUN, MUSTARD_IN_TWO_ACTIVE_ARTICLES] })
    const list = mountList()
    await vi.waitFor(() =>
      expect((list.get(`${MUSTARD_ROW} [data-test="deactivate"]`).element as HTMLButtonElement).disabled).toBe(true),
    )

    await list.get(`${MUSTARD_ROW} [data-test="deactivate-wrapper"]`).trigger('mouseenter')

    await vi.waitFor(() =>
      expect(refusalReasonsOnScreen()).toContain(
        'Diese Zutat wird noch bei Bratwurst, Currywurst verwendet. Nehmen Sie sie dort zuerst heraus, unter "Artikel".',
      ),
    )
  })

  it('names the active articles in English', async () => {
    ingredientsListLaptop({ ingredients: [BUN, MUSTARD_IN_TWO_ACTIVE_ARTICLES] })
    const list = mountList('en')
    await vi.waitFor(() =>
      expect((list.get(`${MUSTARD_ROW} [data-test="deactivate"]`).element as HTMLButtonElement).disabled).toBe(true),
    )

    await list.get(`${MUSTARD_ROW} [data-test="deactivate-wrapper"]`).trigger('mouseenter')

    await vi.waitFor(() =>
      expect(refusalReasonsOnScreen()).toContain(
        'This ingredient is still used in Bratwurst, Currywurst. Take it out of those items first, under "Items".',
      ),
    )
  })
})

describe('a deactivated ingredient', () => {
  it('is activated again without a question first', async () => {
    const laptop = ingredientsListLaptop({ ingredients: [OLD_MILK] })
    const list = mountList()
    await list.get('[data-test="show-deactivated"] input').setValue(true)
    await vi.waitFor(() => expect(list.find('[data-test="reactivate"]').exists()).toBe(true))

    await list.get('[data-test="reactivate"]').trigger('click')

    await vi.waitFor(() =>
      expect(laptop.writes().map((call) => `${call.method} ${call.url}`)).toEqual([
        'POST /api/admin/ingredients/ingredient-milch/activate',
      ]),
    )
  })
})
