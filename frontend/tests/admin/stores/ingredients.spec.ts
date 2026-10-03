import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useAdminIngredientsStore } from '../../../src/admin/stores/ingredients'
import { aHold, answer, heldUntil, inTurn, stubLaptop, type StubbedLaptop } from '../../support/laptop'

const FLOUR = { ingredientId: 'ingredient-mehl', name: 'Mehl', unit: 'gram', isActive: true }
const BUN = { ingredientId: 'ingredient-broetchen', name: 'Brötchen', unit: 'piece', isActive: true }


interface IngredientsScenario {
  listed?: unknown[]
  writeStatus?: number
  writeAnswer?: unknown
}

function ingredientsLaptop(scenario: IngredientsScenario = {}): StubbedLaptop {
  return stubLaptop()
    .answersEverythingElse(answer(scenario.writeAnswer ?? {}, scenario.writeStatus ?? 200))
    .answers('GET', /./, answer({ ingredients: scenario.listed ?? [FLOUR] }))
}


beforeEach(() => {
  setActivePinia(createPinia())
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('the ingredient list of the admin', () => {
  it('holds what the laptop lists', async () => {
    ingredientsLaptop({ listed: [BUN, FLOUR] })
    const ingredients = useAdminIngredientsStore()

    await ingredients.load()

    expect(ingredients.ingredients).toEqual([BUN, FLOUR])
    expect(ingredients.loadFailed).toBe(false)
  })

  it('says so when the list cannot be read', async () => {
    stubLaptop().answersEverythingElse(answer({}, 500))
    const ingredients = useAdminIngredientsStore()

    await ingredients.load()

    expect(ingredients.loadFailed).toBe(true)
  })

  it('keeps the newer answer when an older read arrives later', async () => {
    const theFirstRead = aHold()
    stubLaptop().answersEverythingElse(
      inTurn(
        heldUntil(theFirstRead.released, answer({ ingredients: [] })),
        answer({ ingredients: [BUN, FLOUR] }),
      ),
    )
    const ingredients = useAdminIngredientsStore()

    const firstRead = ingredients.load()
    await ingredients.load()
    theFirstRead.release()
    await firstRead

    expect(ingredients.ingredients).toEqual([BUN, FLOUR])
  })
})

describe('writing an ingredient', () => {
  it('creates it with the name and unit as given and shows it at once', async () => {
    const laptop = ingredientsLaptop({ writeStatus: 201, writeAnswer: BUN })
    const ingredients = useAdminIngredientsStore()

    const result = await ingredients.create({ name: 'Brötchen', unit: 'piece' })

    expect(result).toEqual({ kind: 'ok', value: BUN })
    expect(laptop.calls).toEqual([
      { url: '/api/admin/ingredients', method: 'POST', body: { name: 'Brötchen', unit: 'piece' } },
    ])
    expect(ingredients.ingredients).toEqual([BUN])
  })

  it('renames it and reads the list again', async () => {
    const laptop = ingredientsLaptop({ writeAnswer: { ingredientId: FLOUR.ingredientId } })
    const ingredients = useAdminIngredientsStore()

    const result = await ingredients.save(FLOUR.ingredientId, { name: 'Weizenmehl', unit: 'gram' })

    expect(result).toEqual({ kind: 'ok', value: null })
    expect(laptop.calls).toEqual([
      {
        url: '/api/admin/ingredients/ingredient-mehl',
        method: 'PUT',
        body: { name: 'Weizenmehl', unit: 'gram' },
      },
      { url: '/api/admin/ingredients', method: 'GET', body: undefined },
    ])
  })

  it('deactivates and activates it through their own routes', async () => {
    const laptop = ingredientsLaptop({ writeAnswer: { ingredientId: FLOUR.ingredientId } })
    const ingredients = useAdminIngredientsStore()

    await ingredients.setActive(FLOUR.ingredientId, false)
    await ingredients.setActive(FLOUR.ingredientId, true)

    expect(laptop.calls.filter((call) => call.method === 'POST').map((call) => call.url)).toEqual([
      '/api/admin/ingredients/ingredient-mehl/deactivate',
      '/api/admin/ingredients/ingredient-mehl/activate',
    ])
  })

  it('hands back the reason the laptop named when it refuses', async () => {
    ingredientsLaptop({
      writeStatus: 422,
      writeAnswer: { code: 'UnprocessableEntity', messageKey: 'errors.admin.ingredients.nameTaken', parameters: {}, details: null },
    })
    const ingredients = useAdminIngredientsStore()

    const result = await ingredients.create({ name: 'Mehl', unit: 'gram' })

    expect(result).toEqual({
      kind: 'failed',
      message: { key: 'errors.admin.ingredients.nameTaken', parameters: {}, count: null },
    })
    expect(ingredients.ingredients).toEqual([])
  })

  it('says the action did not work when the laptop named no reason', async () => {
    ingredientsLaptop({ writeStatus: 500 })
    const ingredients = useAdminIngredientsStore()

    const result = await ingredients.save(FLOUR.ingredientId, { name: 'Mehl', unit: 'gram' })

    expect(result).toEqual({
      kind: 'failed',
      message: { key: 'errors.admin.actionFailed', parameters: {}, count: null },
    })
  })
})
