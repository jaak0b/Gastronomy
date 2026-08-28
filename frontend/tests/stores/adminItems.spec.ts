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
