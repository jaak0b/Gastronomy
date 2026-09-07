import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useAdminCategoriesStore } from '../../src/stores/admin/categories'

const DRINKS = {
  categoryId: '11111111-1111-1111-1111-111111111111',
  name: 'Getränke',
  colourHex: '#C62828',
  sortOrder: 1,
  isActive: true,
}

const FOOD = {
  categoryId: '22222222-2222-2222-2222-222222222222',
  name: 'Speisen',
  colourHex: '#6D4C41',
  sortOrder: 2,
  isActive: true,
}

interface Call {
  url: string
  method: string
  body: unknown
}

function answerWith(payloadFor: (url: string, method: string) => unknown, status = 200): Call[] {
  const calls: Call[] = []
  vi.stubGlobal(
    'fetch',
    vi.fn(async (url: string, init?: RequestInit) => {
      const method = init?.method ?? 'GET'
      calls.push({
        url,
        method,
        body: init?.body === undefined ? null : JSON.parse(init.body as string),
      })
      const isRead = method === 'GET'
      return new Response(JSON.stringify(payloadFor(url, method)), {
        status: isRead ? 200 : status,
      })
    }),
  )
  return calls
}

function theTwoCategories() {
  return answerWith(() => ({ categories: [DRINKS, FOOD] }))
}

describe('the category list of the laptop', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('is asked for at its own address', async () => {
    const calls = theTwoCategories()
    const categories = useAdminCategoriesStore()

    await categories.load()

    expect(calls.map((call) => call.url)).toEqual(['/api/admin/categories'])
  })

  it('keeps the order the laptop sent, because the laptop owns the order', async () => {
    answerWith(() => ({ categories: [FOOD, DRINKS] }))
    const categories = useAdminCategoriesStore()

    await categories.load()

    expect(categories.categories.map((category) => category.name)).toEqual([
      'Speisen',
      'Getränke',
    ])
  })

  it('says that loading failed when the laptop cannot be reached', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => {
        throw new TypeError('Failed to fetch')
      }),
    )
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
    const calls = answerWith((_url, method) =>
      method === 'GET' ? { categories: [DRINKS] } : DRINKS,
    )
    const categories = useAdminCategoriesStore()

    await categories.create({ name: 'Getränke', colourHex: '#C62828' })

    expect(calls[0]).toMatchObject({
      url: '/api/admin/categories',
      method: 'POST',
      body: { name: 'Getränke', colourHex: '#C62828' },
    })
  })

  it('hands back the category the laptop created, so it can be picked right away', async () => {
    answerWith((_url, method) => (method === 'GET' ? { categories: [DRINKS] } : DRINKS))
    const categories = useAdminCategoriesStore()

    const created = await categories.create({ name: 'Getränke', colourHex: '#C62828' })

    expect(created?.categoryId).toBe(DRINKS.categoryId)
  })

  it('takes the created category from the answer instead of reading the whole list again', async () => {
    const calls = answerWith((_url, method) =>
      method === 'GET' ? { categories: [DRINKS] } : DRINKS,
    )
    const categories = useAdminCategoriesStore()

    await categories.create({ name: 'Getränke', colourHex: '#C62828' })

    expect(calls.map((call) => call.method)).toEqual(['POST'])
    expect(categories.categories).toEqual([DRINKS])
  })

  it('keeps the reason the laptop refused it and hands nothing back', async () => {
    answerWith(
      () => ({
        code: 'Conflict',
        messageKey: 'admin.categoryNameTaken',
        parameters: {},
        details: null,
      }),
      409,
    )
    const categories = useAdminCategoriesStore()

    const created = await categories.create({ name: 'Getränke', colourHex: '#C62828' })

    expect(created).toBeNull()
    expect(categories.errorMessage?.key).toBe('admin.categoryNameTaken')
  })

  it('says the action did not work when the laptop named no reason', async () => {
    answerWith(() => ({}), 500)
    const categories = useAdminCategoriesStore()

    await categories.create({ name: 'Getränke', colourHex: '#C62828' })

    expect(categories.errorMessage?.key).toBe('admin.actionFailed')
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

describe('renaming and recolouring a category', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('sends the new name and the new colour to the address of that category', async () => {
    const calls = answerWith((_url, method) =>
      method === 'GET' ? { categories: [DRINKS] } : DRINKS,
    )
    const categories = useAdminCategoriesStore()

    await categories.save({
      categoryId: DRINKS.categoryId,
      name: 'Kaffee',
      colourHex: '#6D4C41',
    })

    expect(calls[0]).toMatchObject({
      url: `/api/admin/categories/${DRINKS.categoryId}`,
      method: 'PUT',
      body: { name: 'Kaffee', colourHex: '#6D4C41' },
    })
  })

  it('keeps the reason the laptop refused it', async () => {
    answerWith(
      () => ({
        code: 'Conflict',
        messageKey: 'admin.categoryNameTaken',
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

    expect(saved).toBe(false)
    expect(categories.errorMessage?.key).toBe('admin.categoryNameTaken')
  })

  it('is forgotten again once the admin closes the refusal', async () => {
    answerWith(() => ({}), 500)
    const categories = useAdminCategoriesStore()

    await categories.save({ categoryId: DRINKS.categoryId, name: '', colourHex: '#C62828' })
    categories.forgetError()

    expect(categories.errorMessage).toBeNull()
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
    const calls = answerWith((_url, method) =>
      method === 'GET' ? { categories: [DRINKS, FOOD] } : { categories: [FOOD, DRINKS] },
    )
    const categories = useAdminCategoriesStore()

    await categories.move(FOOD.categoryId, 'up')

    expect(calls[0]).toMatchObject({
      url: `/api/admin/categories/${FOOD.categoryId}/move`,
      method: 'POST',
      body: { direction: 'up' },
    })
  })

  it('takes the new order from the answer instead of working it out again', async () => {
    const calls = answerWith((_url, method) =>
      method === 'GET' ? { categories: [DRINKS, FOOD] } : { categories: [FOOD, DRINKS] },
    )
    const categories = useAdminCategoriesStore()

    await categories.move(FOOD.categoryId, 'up')

    expect(categories.categories.map((category) => category.name)).toEqual([
      'Speisen',
      'Getränke',
    ])
    expect(calls.map((call) => call.method)).toEqual(['POST'])
  })

  it('says the action did not work when the laptop cannot be reached', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => {
        throw new TypeError('Failed to fetch')
      }),
    )
    const categories = useAdminCategoriesStore()

    await categories.move(FOOD.categoryId, 'down')

    expect(categories.errorMessage?.key).toBe('admin.actionFailed')
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
    const calls = answerWith((_url, method) =>
      method === 'GET' ? { categories: [DRINKS] } : { ...DRINKS, isActive: false },
    )
    const categories = useAdminCategoriesStore()

    await categories.setActive(DRINKS.categoryId, false)

    expect(calls[0]).toMatchObject({
      url: `/api/admin/categories/${DRINKS.categoryId}/deactivate`,
      method: 'POST',
    })
  })

  it('switches it on again at its own address', async () => {
    const calls = answerWith((_url, method) =>
      method === 'GET' ? { categories: [DRINKS] } : DRINKS,
    )
    const categories = useAdminCategoriesStore()

    await categories.setActive(DRINKS.categoryId, true)

    expect(calls[0]).toMatchObject({
      url: `/api/admin/categories/${DRINKS.categoryId}/activate`,
      method: 'POST',
    })
  })

  it('keeps the reason the laptop refused it', async () => {
    answerWith(
      () => ({
        code: 'Conflict',
        messageKey: 'admin.categoryHasActiveItems',
        parameters: {},
        details: null,
      }),
      409,
    )
    const categories = useAdminCategoriesStore()

    await categories.setActive(DRINKS.categoryId, false)

    expect(categories.errorMessage?.key).toBe('admin.categoryHasActiveItems')
  })
})
