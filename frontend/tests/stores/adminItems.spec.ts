import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useAdminItemsStore } from '../../src/stores/admin/items'

function itemNamed(name: string, categoryName: string) {
  return {
    itemId: `item-${name}`,
    name,
    categoryName,
    priceCents: 250,
    sortOrder: 1,
    isActive: true,
    isAvailable: true,
    stationIds: [],
  }
}

const AN_ITEM = {
  name: 'Bratwurst',
  categoryName: 'Speisen',
  priceCents: 350,
  sortOrder: 1,
  stationIds: ['11111111-1111-1111-1111-111111111111'],
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

    await items.save(AN_ITEM)

    expect(items.errorMessage?.key).toBe('admin.actionFailed')
  })

  it('keeps the reason the laptop named', async () => {
    refuseWith(409, {
      code: 'Conflict',
      messageKey: 'admin.itemHasNoActiveStation',
      parameters: {},
      details: null,
    })
    const items = useAdminItemsStore()

    await items.save(AN_ITEM)

    expect(items.errorMessage?.key).toBe('admin.itemHasNoActiveStation')
  })

  it('says the action did not work when the laptop cannot be reached at all', async () => {
    dropTheConnection()
    const items = useAdminItemsStore()

    await items.save(AN_ITEM)

    expect(items.errorMessage?.key).toBe('admin.actionFailed')
  })

  it('still asks for a station before it sends anything', async () => {
    refuseWith(500, {})
    const items = useAdminItemsStore()

    await items.save({ ...AN_ITEM, stationIds: [] })

    expect(items.errorMessage?.key).toBe('admin.itemNeedsAStation')
  })
})

describe('an item the laptop would not switch on or off the menu', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('says the action did not work when the laptop named no reason', async () => {
    refuseWith(500, {})
    const items = useAdminItemsStore()

    await items.setActive('item-1', false)

    expect(items.errorMessage?.key).toBe('admin.actionFailed')
  })

  it('says the action did not work when the laptop cannot be reached at all', async () => {
    dropTheConnection()
    const items = useAdminItemsStore()

    await items.setActive('item-1', false)

    expect(items.errorMessage?.key).toBe('admin.actionFailed')
  })

  it('keeps the reason the laptop named', async () => {
    refuseWith(422, {
      code: 'UnprocessableEntity',
      messageKey: 'admin.itemHasNoActiveStation',
      parameters: {},
      details: null,
    })
    const items = useAdminItemsStore()

    await items.setActive('item-1', true)

    expect(items.errorMessage?.key).toBe('admin.itemHasNoActiveStation')
  })
})

describe('the categories already in use', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('offers each one once, in alphabetical order', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(
        async () =>
          new Response(
            JSON.stringify({
              items: [
                itemNamed('Wasser', 'Getränke'),
                itemNamed('Bratwurst', 'Essen'),
                itemNamed('Bier', 'Getränke'),
              ],
            }),
            { status: 200 },
          ),
      ),
    )
    const items = useAdminItemsStore()

    await items.load()

    expect(items.categoryNames).toEqual(['Essen', 'Getränke'])
  })
})
