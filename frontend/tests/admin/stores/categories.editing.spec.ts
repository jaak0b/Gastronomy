import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useAdminCategoriesStore } from '../../../src/admin/stores/categories'
import {
  aHold,
  answer,
  heldUntil,
  inTurn,
  noConnection,
  stubLaptop,
  stubLaptopAnswering,
} from '../../support/laptop'

const EVERY_ADDRESS = /^\/api\//
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
    const theMove = aHold()
    const laptop = stubLaptop()
      .answers(
        'GET',
        EVERY_ADDRESS,
        inTurn(
          answer({ categories: [DRINKS, FOOD] }),
          answer({ categories: [DRINKS, FOOD] }),
          answer({ categories: [FOOD, DRINKS] }),
        ),
      )
      .answers('POST', EVERY_ADDRESS, heldUntil(theMove.released, answer({ categories: [FOOD, DRINKS] })))
    const categories = useAdminCategoriesStore()
    await categories.load()

    const moved = categories.move(FOOD.categoryId, 'up')
    await vi.waitFor(() => expect(laptop.writes()).toHaveLength(1))
    await categories.load()
    theMove.release()
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
    const firstAnswer = aHold()
    const secondAnswer = aHold()
    const laptop = stubLaptop()
      .answersEverythingElse(answer({ categories: [DRINKS, FOOD] }))
      .answers(
        'POST',
        EVERY_ADDRESS,
        inTurn(
          heldUntil(firstAnswer.released, answer({ categories: [FOOD, DRINKS] })),
          heldUntil(secondAnswer.released, answer({ categories: [FOOD, DRINKS] })),
        ),
      )
    const categories = useAdminCategoriesStore()

    const firstMove = categories.move(FOOD.categoryId, 'up')
    const secondMove = categories.move(FOOD.categoryId, 'down')
    await vi.waitFor(() => expect(laptop.writes()).toHaveLength(1))

    expect(laptop.writtenBodies()).toEqual([expect.objectContaining({ direction: 'up' })])

    firstAnswer.release()
    await vi.waitFor(() => expect(laptop.writes()).toHaveLength(2))
    secondAnswer.release()
    await firstMove
    await secondMove

    expect(laptop.writtenBodies()).toEqual([
      expect.objectContaining({ direction: 'up' }),
      expect.objectContaining({ direction: 'down' }),
    ])
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
