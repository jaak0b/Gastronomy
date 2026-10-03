import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useAdminItemsStore } from '../../../src/admin/stores/items'
import { stubLaptopAnswering, answer } from '../../support/laptop'

const AN_ITEM = {
  name: 'Bratwurst',
  categoryId: 'category-speisen',
  sortOrder: 1,
  productionMinutes: null,
  isQueueIndependent: false,
}

const CREATED_ITEM = {
  itemId: 'item-neu',
  name: 'Bratwurst',
  categoryId: 'category-speisen',
  sortOrder: 1,
  isActive: true,
  productionMinutes: null,
  isQueueIndependent: false,
  ingredients: [],
  atTheFestival: null,
}

function refuseWith(status: number, body: unknown) {
  vi.stubGlobal(
    'fetch',
    vi.fn(async (_url: string, init?: RequestInit) =>
      (init?.method ?? 'GET') === 'GET'
        ? new Response(JSON.stringify({ items: [] }), { status: 200 })
        : new Response(JSON.stringify(body), { status }),
    ),
  )
}

function dropTheConnection() {
  vi.stubGlobal(
    'fetch',
    vi.fn(async (_url: string, init?: RequestInit) => {
      if ((init?.method ?? 'GET') !== 'GET') {
        throw new TypeError('Failed to fetch')
      }
      return new Response(JSON.stringify({ items: [] }), { status: 200 })
    }),
  )
}

describe('an item the laptop would not save', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('says the action did not work when the laptop named no reason', async () => {
    refuseWith(500, {})
    const items = useAdminItemsStore()

    const result = await items.save(AN_ITEM)

    expect(result).toEqual({
      kind: 'failed',
      message: { key: 'errors.admin.actionFailed', parameters: {}, count: null },
    })
  })

  it('keeps the reason the laptop named', async () => {
    refuseWith(400, {
      code: 'ValidationFailed',
      messageKey: 'errors.admin.items.nameMissing',
      parameters: {},
      details: null,
    })
    const items = useAdminItemsStore()

    const result = await items.save(AN_ITEM)

    expect(result).toEqual({
      kind: 'failed',
      message: { key: 'errors.admin.items.nameMissing', parameters: {}, count: null },
    })
  })

  it('says the action did not work when the laptop cannot be reached at all', async () => {
    dropTheConnection()
    const items = useAdminItemsStore()

    const result = await items.save(AN_ITEM)

    expect(result).toEqual({
      kind: 'failed',
      message: { key: 'errors.admin.actionFailed', parameters: {}, count: null },
    })
  })
})

describe('an item the laptop would not switch on or off', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('says the action did not work when the laptop named no reason', async () => {
    refuseWith(500, {})
    const items = useAdminItemsStore()

    const result = await items.setActive('item-1', false)

    expect(result).toEqual({
      kind: 'failed',
      message: { key: 'errors.admin.actionFailed', parameters: {}, count: null },
    })
  })

  it('says the action did not work when the laptop cannot be reached at all', async () => {
    dropTheConnection()
    const items = useAdminItemsStore()

    const result = await items.setActive('item-1', false)

    expect(result).toEqual({
      kind: 'failed',
      message: { key: 'errors.admin.actionFailed', parameters: {}, count: null },
    })
  })

  it('keeps the reason the laptop named', async () => {
    refuseWith(422, {
      code: 'UnprocessableEntity',
      messageKey: 'errors.admin.items.categoryIsOff',
      parameters: {},
      details: null,
    })
    const items = useAdminItemsStore()

    const result = await items.setActive('item-1', true)

    expect(result).toEqual({
      kind: 'failed',
      message: { key: 'errors.admin.items.categoryIsOff', parameters: {}, count: null },
    })
  })
})

describe('an article created while a list read is on the way', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('stays in the list when the slower read answers first', async () => {
    let releaseThePost = (): void => {}
    const thePost = new Promise<Response>((carryOn) => {
      releaseThePost = () => carryOn(new Response(JSON.stringify(CREATED_ITEM), { status: 201 }))
    })
    vi.stubGlobal(
      'fetch',
      vi.fn(async (_url: string, init?: RequestInit) =>
        (init?.method ?? 'GET') === 'POST'
          ? await thePost
          : new Response(JSON.stringify({ items: [] }), { status: 200 }),
      ),
    )
    const items = useAdminItemsStore()

    const created = items.create(AN_ITEM)
    await items.load()
    releaseThePost()
    await created

    expect(items.items.map((item) => item.itemId)).toEqual(['item-neu'])
  })

  it('does not show the created article in a list of another festival', async () => {
    let releaseThePost = (): void => {}
    const thePost = new Promise<Response>((carryOn) => {
      releaseThePost = () => carryOn(new Response(JSON.stringify(CREATED_ITEM), { status: 201 }))
    })
    vi.stubGlobal(
      'fetch',
      vi.fn(async (_url: string, init?: RequestInit) =>
        (init?.method ?? 'GET') === 'POST'
          ? await thePost
          : new Response(JSON.stringify({ items: [] }), { status: 200 }),
      ),
    )
    const items = useAdminItemsStore()

    await items.loadAtTheFestival('fest-1')
    const created = items.create(AN_ITEM)
    await items.loadAtTheFestival('fest-2')
    releaseThePost()
    await created

    expect(items.items).toEqual([])
  })
})

describe('a new item the laptop creates', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('hands back the article from the answer, so it can be placed right away', async () => {
    stubLaptopAnswering((_url, method) => (method === 'GET' ? { items: [] } : CREATED_ITEM))
    const items = useAdminItemsStore()

    const created = await items.create(AN_ITEM)

    expect(created).toEqual({ kind: 'ok', value: CREATED_ITEM })
  })

  it('writes only the article and does not read the list again', async () => {
    const laptop = stubLaptopAnswering((_url, method) => (method === 'GET' ? { items: [] } : CREATED_ITEM))
    const items = useAdminItemsStore()

    await items.create(AN_ITEM)

    expect(laptop.calls).toEqual([
      {
        url: '/api/admin/items',
        method: 'POST',
        body: {
          name: 'Bratwurst',
          categoryId: 'category-speisen',
          sortOrder: 1,
          productionMinutes: null,
          isQueueIndependent: false,
        },
      },
    ])
    expect(items.items).toEqual([CREATED_ITEM])
  })

  it('keeps the created article in the list even when the list cannot be read afterwards', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async (_url: string, init?: RequestInit) => {
        if ((init?.method ?? 'GET') === 'POST') {
          return new Response(JSON.stringify(CREATED_ITEM), { status: 201 })
        }
        throw new TypeError('Failed to fetch')
      }),
    )
    const items = useAdminItemsStore()

    await items.create(AN_ITEM)

    expect(items.items.map((item) => item.name)).toEqual(['Bratwurst'])
  })

  it('keeps the reason the laptop refused it and appends nothing', async () => {
    stubLaptopAnswering(
      () => ({
        code: 'Conflict',
        messageKey: 'errors.admin.items.nameTaken',
        parameters: {},
        details: null,
      }),
      409,
    )
    const items = useAdminItemsStore()

    const created = await items.create(AN_ITEM)

    expect(created).toEqual({
      kind: 'failed',
      message: { key: 'errors.admin.items.nameTaken', parameters: {}, count: null },
    })
    expect(items.items).toEqual([])
  })
})

describe('the recipe of an article', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('sets the amount of an ingredient and reads the articles again', async () => {
    const laptop = stubLaptopAnswering((_url, method) =>
      method === 'GET' ? { items: [] } : { itemId: 'item-neu' },
    )
    const items = useAdminItemsStore()

    const result = await items.setIngredientAmount('item-neu', 'ingredient-mehl', 1500)

    expect(result).toEqual({ kind: 'ok', value: null })
    expect(laptop.calls).toEqual([
      {
        url: '/api/admin/items/item-neu/ingredients/ingredient-mehl',
        method: 'PUT',
        body: { amount: 1500 },
      },
      { url: '/api/admin/items', method: 'GET', body: undefined },
    ])
  })

  it('removes an ingredient from the recipe and reads the articles again', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async (_url: string, init?: RequestInit) =>
        (init?.method ?? 'GET') === 'GET'
          ? new Response(JSON.stringify({ items: [] }), { status: 200 })
          : new Response(null, { status: 204 }),
      ),
    )
    const items = useAdminItemsStore()

    const result = await items.removeIngredient('item-neu', 'ingredient-mehl')

    expect(result).toEqual({ kind: 'ok', value: null })
    const fetched = vi.mocked(fetch).mock.calls.map(([url, init]) => `${init?.method ?? 'GET'} ${url}`)
    expect(fetched).toEqual([
      'DELETE /api/admin/items/item-neu/ingredients/ingredient-mehl',
      'GET /api/admin/items',
    ])
  })

  it('hands back the reason the laptop named when it refuses an amount', async () => {
    refuseWith(422, {
      code: 'UnprocessableEntity',
      messageKey: 'errors.admin.ingredients.amountInvalid',
      parameters: {},
      details: null,
    })
    const items = useAdminItemsStore()

    const result = await items.setIngredientAmount('item-neu', 'ingredient-mehl', 0)

    expect(result).toEqual({
      kind: 'failed',
      message: { key: 'errors.admin.ingredients.amountInvalid', parameters: {}, count: null },
    })
  })
})
