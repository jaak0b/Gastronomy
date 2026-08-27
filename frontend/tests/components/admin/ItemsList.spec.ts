import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createI18n } from 'vue-i18n'
import ItemsList from '../../../src/components/admin/items/ItemsList.vue'
import de from '../../../src/locales/de.json'
import en from '../../../src/locales/en.json'

const ITEM_ID = '22222222-2222-2222-2222-222222222222'
const STATION_ID = '11111111-1111-1111-1111-111111111111'

const ONE_ITEM = {
  items: [
    {
      itemId: ITEM_ID,
      name: 'Bratwurst',
      categoryName: 'Speisen',
      priceCents: 350,
      sortOrder: 1,
      isActive: true,
      isAvailable: true,
      locationIds: [STATION_ID],
    },
  ],
}

const ONE_STATION = {
  locations: [
    {
      locationId: STATION_ID,
      name: 'Küche',
      sortOrder: 1,
      slipLanguage: 'de',
      isActive: true,
      accessKey: 'key-kueche',
      breakGlassUrl: 'http://192.168.1.20:5000/s/key-kueche',
      transportKind: 'Mock',
      host: null,
      port: 9100,
      isEnabled: true,
      isOnline: true,
      isPaperEnd: false,
      isCoverOpen: false,
      isFaulty: false,
    },
  ],
}

function stubFetch() {
  const urls: string[] = []
  vi.stubGlobal(
    'fetch',
    vi.fn(async (url: string) => {
      urls.push(url)
      const payload = url.includes('/api/admin/items') ? ONE_ITEM : ONE_STATION
      return new Response(JSON.stringify(payload), { status: 200 })
    }),
  )
  return urls
}

function mountList() {
  const i18n = createI18n({ legacy: false, locale: 'de', messages: { de, en } })
  return mount(ItemsList, { global: { plugins: [i18n] } })
}

const OFF_THE_MENU = {
  items: [{ ...ONE_ITEM.items[0], isActive: false }],
}

function stubFetchWith(items: unknown) {
  const urls: string[] = []
  vi.stubGlobal(
    'fetch',
    vi.fn(async (url: string) => {
      urls.push(url)
      const payload = url.includes('/api/admin/items') ? items : ONE_STATION
      return new Response(JSON.stringify(payload), { status: 200 })
    }),
  )
  return urls
}

const DEACTIVATED_ITEM = {
  items: [{ ...ONE_ITEM.items[0], isActive: false }],
}

describe('deactivating an item', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('asks before it happens', async () => {
    const urls = stubFetchWith(ONE_ITEM)

    const list = mountList()
    await vi.waitFor(() => expect(list.find('li').exists()).toBe(true))
    await list.get('.deactivate').trigger('click')

    expect(list.find('.confirm-dialog').exists()).toBe(true)
    expect(urls.some((url) => url.endsWith('/deactivate'))).toBe(false)
  })

  it('says that the orders already placed are kept', async () => {
    stubFetchWith(ONE_ITEM)

    const list = mountList()
    await vi.waitFor(() => expect(list.find('li').exists()).toBe(true))
    await list.get('.deactivate').trigger('click')

    expect(list.get('.confirm-body').text()).toContain('bleiben gespeichert')
  })

  it('deactivates it once the question is answered with yes', async () => {
    const urls = stubFetchWith(ONE_ITEM)

    const list = mountList()
    await vi.waitFor(() => expect(list.find('li').exists()).toBe(true))
    await list.get('.deactivate').trigger('click')
    await list.get('.confirm').trigger('click')

    await vi.waitFor(() => expect(urls).toContain(`/api/admin/items/${ITEM_ID}/deactivate`))
  })
})

describe('an item that is deactivated', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('is left out of the list until the admin asks to see deactivated entries', async () => {
    stubFetchWith(DEACTIVATED_ITEM)

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.show-deactivated').exists()).toBe(true))

    expect(list.find('li').exists()).toBe(false)
  })

  it('offers no sold-out toggle, because nobody can order it', async () => {
    stubFetchWith(DEACTIVATED_ITEM)

    const list = mountList()
    await list.get('.show-deactivated').setValue(true)
    await vi.waitFor(() => expect(list.find('li').exists()).toBe(true))

    expect(list.find('.sold-out-toggle').exists()).toBe(false)
  })

  it('offers to activate it again without asking a question first', async () => {
    const urls = stubFetchWith(DEACTIVATED_ITEM)

    const list = mountList()
    await list.get('.show-deactivated').setValue(true)
    await vi.waitFor(() => expect(list.find('li').exists()).toBe(true))
    await list.get('.reactivate').trigger('click')

    await vi.waitFor(() => expect(urls).toContain(`/api/admin/items/${ITEM_ID}/activate`))
  })
})

describe('the sold-out button beside an item', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('marks that item sold out at its own address', async () => {
    const urls = stubFetch()

    const list = mountList()
    await vi.waitFor(() => expect(list.find('li').exists()).toBe(true))
    await list.get('.sold-out-toggle').trigger('click')

    await vi.waitFor(() =>
      expect(urls).toContain(`/api/admin/items/${ITEM_ID}/availability`),
    )
  })
})

describe('the station checkboxes on an item', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('show the station an item is already assigned to as ticked', async () => {
    stubFetch()

    const list = mountList()
    await vi.waitFor(() => expect(list.find('li').exists()).toBe(true))
    await list.get('li button:nth-of-type(2)').trigger('click')
    await vi.waitFor(() => expect(list.find('.assignment-editor').exists()).toBe(true))

    const checkbox = list.get('.assignment-editor input[type="checkbox"]')
    expect((checkbox.element as HTMLInputElement).checked).toBe(true)
  })

  it('names the assigned station in the sentence below the boxes', async () => {
    stubFetch()

    const list = mountList()
    await vi.waitFor(() => expect(list.find('li').exists()).toBe(true))
    await list.get('li button:nth-of-type(2)').trigger('click')
    await vi.waitFor(() => expect(list.find('.assignment-editor').exists()).toBe(true))

    expect(list.get('.assignment-editor .preview').text()).toContain('Küche')
  })
})
