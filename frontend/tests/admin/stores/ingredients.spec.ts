import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useAdminIngredientsStore } from '../../../src/admin/stores/ingredients'

const FLOUR = { ingredientId: 'ingredient-mehl', name: 'Mehl', unit: 'gram', isActive: true }
const BUN = { ingredientId: 'ingredient-broetchen', name: 'Brötchen', unit: 'piece', isActive: true }

interface Call {
  url: string
  method: string
  body: unknown
}

interface Laptop {
  listed?: unknown[]
  writeStatus?: number
  writeAnswer?: unknown
}

function stubLaptop(laptop: Laptop = {}): Call[] {
  const calls: Call[] = []
  vi.stubGlobal(
    'fetch',
    vi.fn(async (url: string, init?: RequestInit) => {
      const method = init?.method ?? 'GET'
      calls.push({
        url,
        method,
        body: init?.body === undefined ? null : JSON.parse(String(init.body)),
      })
      if (method === 'GET') {
        return new Response(JSON.stringify({ ingredients: laptop.listed ?? [FLOUR] }), {
          status: 200,
        })
      }
      return new Response(JSON.stringify(laptop.writeAnswer ?? {}), {
        status: laptop.writeStatus ?? 200,
      })
    }),
  )
  return calls
}

beforeEach(() => {
  setActivePinia(createPinia())
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('the ingredient list of the admin', () => {
  it('holds what the laptop lists', async () => {
    stubLaptop({ listed: [BUN, FLOUR] })
    const ingredients = useAdminIngredientsStore()

    await ingredients.load()

    expect(ingredients.ingredients).toEqual([BUN, FLOUR])
    expect(ingredients.loadFailed).toBe(false)
  })

  it('says so when the list cannot be read', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => new Response('{}', { status: 500 })))
    const ingredients = useAdminIngredientsStore()

    await ingredients.load()

    expect(ingredients.loadFailed).toBe(true)
  })

  it('keeps the newer answer when an older read arrives later', async () => {
    let answerTheFirstRead: (response: Response) => void = () => undefined
    let reads = 0
    vi.stubGlobal(
      'fetch',
      vi.fn(() => {
        reads += 1
        if (reads === 1) {
          return new Promise<Response>((resolve) => {
            answerTheFirstRead = resolve
          })
        }
        return Promise.resolve(
          new Response(JSON.stringify({ ingredients: [BUN, FLOUR] }), { status: 200 }),
        )
      }),
    )
    const ingredients = useAdminIngredientsStore()

    const firstRead = ingredients.load()
    await ingredients.load()
    answerTheFirstRead(new Response(JSON.stringify({ ingredients: [] }), { status: 200 }))
    await firstRead

    expect(ingredients.ingredients).toEqual([BUN, FLOUR])
  })
})

describe('writing an ingredient', () => {
  it('creates it with the name and unit as given and shows it at once', async () => {
    const calls = stubLaptop({ writeStatus: 201, writeAnswer: BUN })
    const ingredients = useAdminIngredientsStore()

    const result = await ingredients.create({ name: 'Brötchen', unit: 'piece' })

    expect(result).toEqual({ kind: 'ok', value: BUN })
    expect(calls).toEqual([
      { url: '/api/admin/ingredients', method: 'POST', body: { name: 'Brötchen', unit: 'piece' } },
    ])
    expect(ingredients.ingredients).toEqual([BUN])
  })

  it('renames it and reads the list again', async () => {
    const calls = stubLaptop({ writeAnswer: { ingredientId: FLOUR.ingredientId } })
    const ingredients = useAdminIngredientsStore()

    const result = await ingredients.save(FLOUR.ingredientId, { name: 'Weizenmehl', unit: 'gram' })

    expect(result).toEqual({ kind: 'ok', value: null })
    expect(calls).toEqual([
      {
        url: '/api/admin/ingredients/ingredient-mehl',
        method: 'PUT',
        body: { name: 'Weizenmehl', unit: 'gram' },
      },
      { url: '/api/admin/ingredients', method: 'GET', body: null },
    ])
  })

  it('deactivates and activates it through their own routes', async () => {
    const calls = stubLaptop({ writeAnswer: { ingredientId: FLOUR.ingredientId } })
    const ingredients = useAdminIngredientsStore()

    await ingredients.setActive(FLOUR.ingredientId, false)
    await ingredients.setActive(FLOUR.ingredientId, true)

    expect(calls.filter((call) => call.method === 'POST').map((call) => call.url)).toEqual([
      '/api/admin/ingredients/ingredient-mehl/deactivate',
      '/api/admin/ingredients/ingredient-mehl/activate',
    ])
  })

  it('hands back the reason the laptop named when it refuses', async () => {
    stubLaptop({
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
    stubLaptop({ writeStatus: 500 })
    const ingredients = useAdminIngredientsStore()

    const result = await ingredients.save(FLOUR.ingredientId, { name: 'Mehl', unit: 'gram' })

    expect(result).toEqual({
      kind: 'failed',
      message: { key: 'errors.admin.actionFailed', parameters: {}, count: null },
    })
  })
})
