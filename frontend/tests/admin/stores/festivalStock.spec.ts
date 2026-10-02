import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useAdminFestivalStockStore } from '../../../src/admin/stores/festivalStock'

const FLOUR_STOCK = {
  ingredientId: 'ingredient-mehl',
  name: 'Mehl',
  unit: 'gram',
  isActive: true,
  availableAmount: 5000,
  usedAmount: 1200,
  runsOutAtUtc: '2026-07-18T20:30:00Z',
}

interface Call {
  url: string
  method: string
  body: unknown
}

function stubLaptop(writeStatus = 200, writeAnswer: unknown = {}): Call[] {
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
        return new Response(JSON.stringify({ ingredients: [FLOUR_STOCK] }), { status: 200 })
      }
      return new Response(JSON.stringify(writeAnswer), { status: writeStatus })
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

describe('the stock of a festival', () => {
  it('holds what the laptop lists for that festival', async () => {
    const calls = stubLaptop()
    const stock = useAdminFestivalStockStore()

    await stock.loadForFestival('fest-1')

    expect(calls.map((call) => call.url)).toEqual(['/api/admin/festivals/fest-1/ingredients'])
    expect(stock.ingredients).toEqual([FLOUR_STOCK])
  })

  it('says so when the stock cannot be read', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => new Response('{}', { status: 500 })))
    const stock = useAdminFestivalStockStore()

    await stock.loadForFestival('fest-1')

    expect(stock.loadFailed).toBe(true)
  })

  it('drops the rows of another festival as soon as a different festival is opened', async () => {
    stubLaptop()
    const stock = useAdminFestivalStockStore()
    await stock.loadForFestival('fest-1')
    vi.stubGlobal('fetch', vi.fn(() => new Promise<Response>(() => undefined)))

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
    const calls = stubLaptop(200, { ingredientId: 'ingredient-mehl' })
    const stock = useAdminFestivalStockStore()
    await stock.loadForFestival('fest-1')
    calls.length = 0

    const result = await stock.setAvailableAmount('fest-1', 'ingredient-mehl', 5000)

    expect(result).toEqual({ kind: 'ok', value: null })
    expect(calls).toEqual([
      {
        url: '/api/admin/festivals/fest-1/ingredients/ingredient-mehl',
        method: 'PUT',
        body: { availableAmount: 5000 },
      },
      { url: '/api/admin/festivals/fest-1/ingredients', method: 'GET', body: null },
    ])
  })

  it('sends null for an unlimited stock', async () => {
    const calls = stubLaptop(200, { ingredientId: 'ingredient-mehl' })
    const stock = useAdminFestivalStockStore()

    await stock.setAvailableAmount('fest-1', 'ingredient-mehl', null)

    expect(calls[0].body).toEqual({ availableAmount: null })
  })

  it('hands back the reason the laptop named when it refuses', async () => {
    stubLaptop(422, {
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
