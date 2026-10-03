import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useAdminCategoriesStore } from '../../../src/admin/stores/categories'
import { stubLaptop, answer, noConnection, stubLaptopAnswering } from '../../support/laptop'
import { DRINKS, FOOD } from './categoriesFixture'

describe('renaming and recolouring a category', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('sends the new name and the new colour to the address of that category', async () => {
    const laptop = stubLaptopAnswering((_url, method) =>
      method === 'GET' ? { categories: [DRINKS] } : DRINKS,
    )
    const categories = useAdminCategoriesStore()

    await categories.save({
      categoryId: DRINKS.categoryId,
      name: 'Kaffee',
      colourHex: '#6D4C41',
    })

    expect(laptop.calls[0]).toMatchObject({
      url: `/api/admin/categories/${DRINKS.categoryId}`,
      method: 'PUT',
      body: { name: 'Kaffee', colourHex: '#6D4C41' },
    })
  })

  it('keeps the reason the laptop refused it', async () => {
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

    const saved = await categories.save({
      categoryId: DRINKS.categoryId,
      name: 'Speisen',
      colourHex: '#C62828',
    })

    expect(saved).toEqual({
      kind: 'failed',
      message: { key: 'errors.admin.categories.nameTaken', parameters: {}, count: null },
    })
  })
})

describe('moving a category', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('names the direction at the address of that category', async () => {
    const laptop = stubLaptopAnswering((_url, method) =>
      method === 'GET' ? { categories: [DRINKS, FOOD] } : { categories: [FOOD, DRINKS] },
    )
    const categories = useAdminCategoriesStore()

    await categories.move(FOOD.categoryId, 'up')

    expect(laptop.calls[0]).toMatchObject({
      url: `/api/admin/categories/${FOOD.categoryId}/move`,
      method: 'POST',
      body: { direction: 'up' },
    })
  })

  it('takes the new order from the answer instead of working it out again', async () => {
    const laptop = stubLaptopAnswering((_url, method) =>
      method === 'GET' ? { categories: [DRINKS, FOOD] } : { categories: [FOOD, DRINKS] },
    )
    const categories = useAdminCategoriesStore()

    await categories.move(FOOD.categoryId, 'up')

    expect(categories.categories.map((category) => category.name)).toEqual([
      'Speisen',
      'Getränke',
    ])
    expect(laptop.calls.map((call) => call.method)).toEqual(['POST'])
  })

  it('takes the order from a fresh read when a list read overtook the move', async () => {
    let releaseTheMove = (): void => {}
    const theMove = new Promise<Response>((carryOn) => {
      releaseTheMove = () =>
        carryOn(new Response(JSON.stringify({ categories: [FOOD, DRINKS] }), { status: 200 }))
    })
    let reads = 0
    let posts = 0
    vi.stubGlobal(
      'fetch',
      vi.fn(async (_url: string, init?: RequestInit) => {
        if ((init?.method ?? 'GET') === 'POST') {
          posts += 1
          return await theMove
        }
        reads += 1
        return new Response(
          JSON.stringify({ categories: reads >= 3 ? [FOOD, DRINKS] : [DRINKS, FOOD] }),
          { status: 200 },
        )
      }),
    )
    const categories = useAdminCategoriesStore()
    await categories.load()

    const moved = categories.move(FOOD.categoryId, 'up')
    await vi.waitFor(() => expect(posts).toBe(1))
    await categories.load()
    releaseTheMove()
    await moved

    expect(categories.categories.map((category) => category.name)).toEqual([
      'Speisen',
      'Getränke',
    ])
  })

  it('says the action did not work when the laptop cannot be reached', async () => {
    stubLaptop().answersEverythingElse(noConnection())
    const categories = useAdminCategoriesStore()

    const result = await categories.move(FOOD.categoryId, 'down')

    expect(result).toEqual({
      kind: 'failed',
      message: { key: 'errors.admin.actionFailed', parameters: {}, count: null },
    })
  })

  it('waits for one move to be answered before it sends the next', async () => {
    const directions: string[] = []
    const answers: (() => void)[] = []
    vi.stubGlobal(
      'fetch',
      vi.fn(async (_url: string, init?: RequestInit) => {
        if ((init?.method ?? 'GET') !== 'POST') {
          return new Response(JSON.stringify({ categories: [DRINKS, FOOD] }), { status: 200 })
        }
        directions.push(JSON.parse(init?.body as string).direction)
        await new Promise<void>((answer) => answers.push(answer))
        return new Response(JSON.stringify({ categories: [FOOD, DRINKS] }), { status: 200 })
      }),
    )
    const categories = useAdminCategoriesStore()

    const firstMove = categories.move(FOOD.categoryId, 'up')
    const secondMove = categories.move(FOOD.categoryId, 'down')
    await vi.waitFor(() => expect(answers).toHaveLength(1))

    expect(directions).toEqual(['up'])

    answers[0]()
    await vi.waitFor(() => expect(answers).toHaveLength(2))
    answers[1]()
    await firstMove
    await secondMove

    expect(directions).toEqual(['up', 'down'])
  })
})

describe('switching a category off', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('is asked for at the address of that category', async () => {
    const laptop = stubLaptopAnswering((_url, method) =>
      method === 'GET' ? { categories: [DRINKS] } : { ...DRINKS, isActive: false },
    )
    const categories = useAdminCategoriesStore()

    await categories.setActive(DRINKS.categoryId, false)

    expect(laptop.calls[0]).toMatchObject({
      url: `/api/admin/categories/${DRINKS.categoryId}/deactivate`,
      method: 'POST',
    })
  })

  it('switches it on again at its own address', async () => {
    const laptop = stubLaptopAnswering((_url, method) =>
      method === 'GET' ? { categories: [DRINKS] } : DRINKS,
    )
    const categories = useAdminCategoriesStore()

    await categories.setActive(DRINKS.categoryId, true)

    expect(laptop.calls[0]).toMatchObject({
      url: `/api/admin/categories/${DRINKS.categoryId}/activate`,
      method: 'POST',
    })
  })

  it('keeps the reason the laptop refused it', async () => {
    stubLaptopAnswering(
      () => ({
        code: 'Conflict',
        messageKey: 'errors.admin.categories.hasActiveItems',
        parameters: {},
        details: null,
      }),
      409,
    )
    const categories = useAdminCategoriesStore()

    const result = await categories.setActive(DRINKS.categoryId, false)

    expect(result).toEqual({
      kind: 'failed',
      message: { key: 'errors.admin.categories.hasActiveItems', parameters: {}, count: null },
    })
  })
})
