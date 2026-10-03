import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useAdminFestivalStockStore } from '../../../src/admin/stores/festivalStock'
import { stubLaptop, answer, neverAnswers, type StubbedLaptop } from '../../support/laptop'

const FLOUR_STOCK = {
  ingredientId: 'ingredient-mehl',
  name: 'Mehl',
  unit: 'gram',
  isActive: true,
  availableAmount: 5000,
  usedAmount: 1200,
  runsOutAtUtc: '2026-07-18T20:30:00Z',
}


function stockLaptop(writeStatus = 200, writeAnswer: unknown = {}): StubbedLaptop {
  return stubLaptop()
    .answersEverythingElse(answer(writeAnswer, writeStatus))
    .answers('GET', /./, answer({ ingredients: [FLOUR_STOCK] }))
}


beforeEach(() => {
  setActivePinia(createPinia())
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('the stock of a festival', () => {
  it('holds what the laptop lists for that festival', async () => {
    const laptop = stockLaptop()
    const stock = useAdminFestivalStockStore()

    await stock.loadForFestival('fest-1')

    expect(laptop.calls.map((call) => call.url)).toEqual(['/api/admin/festivals/fest-1/ingredients'])
    expect(stock.ingredients).toEqual([FLOUR_STOCK])
  })

  it('says so when the stock cannot be read', async () => {
    stubLaptop().answersEverythingElse(answer({}, 500))
    const stock = useAdminFestivalStockStore()

    await stock.loadForFestival('fest-1')

    expect(stock.loadFailed).toBe(true)
  })

  it('drops the rows of another festival as soon as a different festival is opened', async () => {
    stockLaptop()
    const stock = useAdminFestivalStockStore()
    await stock.loadForFestival('fest-1')
    stubLaptop().answersEverythingElse(neverAnswers())

    void stock.loadForFestival('fest-2')

    expect(stock.ingredients).toEqual([])
  })

  it('keeps the answer to a save when an older read arrives later', async () => {
    let answerTheFirstRead: (response: Response) => void = () => undefined
    let reads = 0
    vi.stubGlobal(
      'fetch',
      vi.fn((_url: string, init?: RequestInit) => {
        if ((init?.method ?? 'GET') !== 'GET') {
          return Promise.resolve(new Response('{}', { status: 200 }))
        }
        reads += 1
        if (reads === 1) {
          return new Promise<Response>((resolve) => {
            answerTheFirstRead = resolve
          })
        }
        return Promise.resolve(
          new Response(JSON.stringify({ ingredients: [FLOUR_STOCK] }), { status: 200 }),
        )
      }),
    )
    const stock = useAdminFestivalStockStore()

    const olderRead = stock.loadForFestival('fest-1')
    await stock.setAvailableAmount('fest-1', 'ingredient-mehl', 5000)
    answerTheFirstRead(
      new Response(JSON.stringify({ ingredients: [{ ...FLOUR_STOCK, availableAmount: null }] }), {
        status: 200,
      }),
    )
    await olderRead

    expect(stock.ingredients).toEqual([FLOUR_STOCK])
  })
})

describe('setting the available amount', () => {
  it('sends the amount in the base unit and reads the stock again', async () => {
    const laptop = stockLaptop(200, { ingredientId: 'ingredient-mehl' })
    const stock = useAdminFestivalStockStore()
    await stock.loadForFestival('fest-1')
    laptop.calls.length = 0

    const result = await stock.setAvailableAmount('fest-1', 'ingredient-mehl', 5000)

    expect(result).toEqual({ kind: 'ok', value: null })
    expect(laptop.calls).toEqual([
      {
        url: '/api/admin/festivals/fest-1/ingredients/ingredient-mehl',
        method: 'PUT',
        body: { availableAmount: 5000 },
      },
      { url: '/api/admin/festivals/fest-1/ingredients', method: 'GET', body: undefined },
    ])
  })

  it('sends null for an unlimited stock', async () => {
    const laptop = stockLaptop(200, { ingredientId: 'ingredient-mehl' })
    const stock = useAdminFestivalStockStore()

    await stock.setAvailableAmount('fest-1', 'ingredient-mehl', null)

    expect(laptop.calls[0].body).toEqual({ availableAmount: null })
  })

  it('hands back the reason the laptop named when it refuses', async () => {
    stockLaptop(422, {
      code: 'UnprocessableEntity',
      messageKey: 'errors.admin.ingredients.stockInvalid',
      parameters: {},
      details: null,
    })
    const stock = useAdminFestivalStockStore()

    const result = await stock.setAvailableAmount('fest-1', 'ingredient-mehl', 5)

    expect(result).toEqual({
      kind: 'failed',
      message: { key: 'errors.admin.ingredients.stockInvalid', parameters: {}, count: null },
    })
  })
})
