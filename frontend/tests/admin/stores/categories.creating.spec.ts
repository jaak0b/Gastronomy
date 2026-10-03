import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useAdminCategoriesStore } from '../../../src/admin/stores/categories'
import { stubLaptop, answer, noConnection, stubLaptopAnswering } from '../../support/laptop'
import { DRINKS, FOOD, CREATED_CATEGORY, theTwoCategories } from './categoriesFixture'

describe('the category list of the laptop', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('is asked for at its own address', async () => {
    const laptop = theTwoCategories()
    const categories = useAdminCategoriesStore()

    await categories.load()

    expect(laptop.calls.map((call) => call.url)).toEqual(['/api/admin/categories'])
  })

  it('keeps the order the laptop sent, because the laptop owns the order', async () => {
    stubLaptopAnswering(() => ({ categories: [FOOD, DRINKS] }))
    const categories = useAdminCategoriesStore()

    await categories.load()

    expect(categories.categories.map((category) => category.name)).toEqual([
      'Speisen',
      'Getränke',
    ])
  })

  it('says that loading failed when the laptop cannot be reached', async () => {
    stubLaptop().answersEverythingElse(noConnection())
    const categories = useAdminCategoriesStore()

    await categories.load()

    expect(categories.loadFailed).toBe(true)
  })
})

describe('a new category', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('is sent with its name and its colour', async () => {
    const laptop = stubLaptopAnswering((_url, method) =>
      method === 'GET' ? { categories: [DRINKS] } : DRINKS,
    )
    const categories = useAdminCategoriesStore()

    await categories.create({ name: 'Getränke', colourHex: '#C62828' })

    expect(laptop.calls[0]).toMatchObject({
      url: '/api/admin/categories',
      method: 'POST',
      body: { name: 'Getränke', colourHex: '#C62828' },
    })
  })

  it('hands back the category the laptop created, so it can be picked right away', async () => {
    stubLaptopAnswering((_url, method) => (method === 'GET' ? { categories: [DRINKS] } : DRINKS))
    const categories = useAdminCategoriesStore()

    const created = await categories.create({ name: 'Getränke', colourHex: '#C62828' })

    expect(created).toEqual({ kind: 'ok', value: DRINKS })
  })

  it('takes the created category from the answer instead of reading the whole list again', async () => {
    const laptop = stubLaptopAnswering((_url, method) =>
      method === 'GET' ? { categories: [DRINKS] } : DRINKS,
    )
    const categories = useAdminCategoriesStore()

    await categories.create({ name: 'Getränke', colourHex: '#C62828' })

    expect(laptop.calls.map((call) => call.method)).toEqual(['POST'])
    expect(categories.categories).toEqual([DRINKS])
  })

  it('keeps the reason the laptop refused it and hands nothing back', async () => {
    stubLaptopAnswering(
      () => ({
        code: 'Conflict',
        messageKey: 'errors.admin.categories.nameTaken',
        parameters: {},
        details: null,
      }),
      409,
    )
    const categories = useAdminCategoriesStore()

    const created = await categories.create({ name: 'Getränke', colourHex: '#C62828' })

    expect(created).toEqual({
      kind: 'failed',
      message: { key: 'errors.admin.categories.nameTaken', parameters: {}, count: null },
    })
  })

  it('says the action did not work when the laptop named no reason', async () => {
    stubLaptopAnswering(() => ({}), 500)
    const categories = useAdminCategoriesStore()

    const created = await categories.create({ name: 'Getränke', colourHex: '#C62828' })

    expect(created).toEqual({
      kind: 'failed',
      message: { key: 'errors.admin.actionFailed', parameters: {}, count: null },
    })
  })

  it('stays in the list even when the list cannot be read again afterwards', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async (_url: string, init?: RequestInit) => {
        if ((init?.method ?? 'GET') === 'POST') {
          return new Response(JSON.stringify(DRINKS), { status: 201 })
        }
        throw new TypeError('Failed to fetch')
      }),
    )
    const categories = useAdminCategoriesStore()

    await categories.create({ name: 'Getränke', colourHex: '#C62828' })

    expect(categories.categories.map((category) => category.name)).toEqual(['Getränke'])
  })
})

describe('a category created while a list read is on the way', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('stays in the list when the slower read answers first', async () => {
    let releaseThePost = (): void => {}
    const thePost = new Promise<Response>((carryOn) => {
      releaseThePost = () =>
        carryOn(new Response(JSON.stringify(CREATED_CATEGORY), { status: 201 }))
    })
    vi.stubGlobal(
      'fetch',
      vi.fn(async (_url: string, init?: RequestInit) =>
        (init?.method ?? 'GET') === 'POST'
          ? await thePost
          : new Response(JSON.stringify({ categories: [] }), { status: 200 }),
      ),
    )
    const categories = useAdminCategoriesStore()

    const created = categories.create({ name: 'Getränke', colourHex: '#C62828' })
    await categories.load()
    releaseThePost()
    await created

    expect(categories.categories.map((category) => category.categoryId)).toEqual(['category-neu'])
  })
})
