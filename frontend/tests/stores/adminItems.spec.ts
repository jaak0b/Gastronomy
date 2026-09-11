import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useAdminItemsStore } from '../../src/stores/admin/items'

const AN_ITEM = {
  name: 'Bratwurst',
  categoryId: 'category-speisen',
  sortOrder: 1,
  productionMinutes: null,
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
    refuseWith(400, {
      code: 'ValidationFailed',
      messageKey: 'admin.itemNameMissing',
      parameters: {},
      details: null,
    })
    const items = useAdminItemsStore()

    await items.save(AN_ITEM)

    expect(items.errorMessage?.key).toBe('admin.itemNameMissing')
  })

  it('says the action did not work when the laptop cannot be reached at all', async () => {
    dropTheConnection()
    const items = useAdminItemsStore()

    await items.save(AN_ITEM)

    expect(items.errorMessage?.key).toBe('admin.actionFailed')
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
      messageKey: 'admin.itemCategoryIsOff',
      parameters: {},
      details: null,
    })
    const items = useAdminItemsStore()

    await items.setActive('item-1', true)

    expect(items.errorMessage?.key).toBe('admin.itemCategoryIsOff')
  })
})
