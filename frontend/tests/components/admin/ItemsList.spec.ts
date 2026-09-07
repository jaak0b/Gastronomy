import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import ItemsList from '../../../src/components/admin/items/ItemsList.vue'
import NewItemDialog from '../../../src/components/admin/items/NewItemDialog.vue'
import ItemForm from '../../../src/components/admin/items/ItemForm.vue'
import { useAdminItemsStore } from '../../../src/stores/admin/items'
import { pressInDialog, testPlugins, waitForDialog } from '../../support/plugins'

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
      stationIds: [STATION_ID],
    },
  ],
}

const ONE_STATION = {
  stations: [
    {
      stationId: STATION_ID,
      name: 'Küche',
      sortOrder: 1,
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
  return mount(ItemsList, { global: { plugins: testPlugins() }, attachTo: document.body })
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

const THREE_ITEMS = {
  items: [
    { ...ONE_ITEM.items[0], itemId: 'aaaa1111-2222-4333-8444-555566667777', name: 'Wasser', categoryName: 'Getränke' },
    { ...ONE_ITEM.items[0], itemId: 'bbbb1111-2222-4333-8444-555566667777', name: 'Schnitzel', categoryName: 'Speisen' },
    { ...ONE_ITEM.items[0], itemId: 'cccc1111-2222-4333-8444-555566667777', name: 'Bier', categoryName: 'Getränke' },
  ],
}

describe('the item list', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('heads each category and sorts the categories by name', async () => {
    stubFetchWith(THREE_ITEMS)

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.item-row').exists()).toBe(true))

    expect(list.findAll('.category-heading').map((element) => element.text())).toEqual([
      'Getränke',
      'Speisen',
    ])
  })

  it('sorts the items inside a category by name', async () => {
    stubFetchWith(THREE_ITEMS)

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.item-row').exists()).toBe(true))

    expect(list.findAll('.item-row .name').map((element) => element.text())).toEqual([
      'Bier',
      'Wasser',
      'Schnitzel',
    ])
  })

  it('keeps the name and the buttons of an item on one line', async () => {
    stubFetchWith(ONE_ITEM)

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.item-row').exists()).toBe(true))

    const line = list.get('.item-row .item-line')

    expect(line.find('.name').exists()).toBe(true)
    expect(line.find('.sold-out-toggle').exists()).toBe(true)
    expect(line.find('.edit').exists()).toBe(true)
    expect(line.find('.deactivate').exists()).toBe(true)
  })
})

describe('deactivating an item', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('asks before it happens', async () => {
    const urls = stubFetchWith(ONE_ITEM)

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.item-row').exists()).toBe(true))
    await list.get('.deactivate').trigger('click')

    await waitForDialog()

    expect(urls.some((url) => url.endsWith('/deactivate'))).toBe(false)
  })

  it('says that the orders already placed are kept', async () => {
    stubFetchWith(ONE_ITEM)

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.item-row').exists()).toBe(true))
    await list.get('.deactivate').trigger('click')

    await waitForDialog()

    expect(document.querySelector('.confirm-body')!.textContent).toContain('bleiben gespeichert')
  })

  it('deactivates it once the question is answered with yes', async () => {
    const urls = stubFetchWith(ONE_ITEM)

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.item-row').exists()).toBe(true))
    await list.get('.deactivate').trigger('click')
    await pressInDialog('.confirm')

    await vi.waitFor(() => expect(urls).toContain(`/api/admin/items/${ITEM_ID}/deactivate`))
  })
})

describe('an item that is deactivated', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('is left out of the list until the admin asks to see deactivated entries', async () => {
    stubFetchWith(DEACTIVATED_ITEM)

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.show-deactivated').exists()).toBe(true))

    expect(list.find('.item-row').exists()).toBe(false)
  })

  it('offers no sold-out toggle, because nobody can order it', async () => {
    stubFetchWith(DEACTIVATED_ITEM)

    const list = mountList()
    await list.get('.show-deactivated input').setValue(true)
    await vi.waitFor(() => expect(list.find('.item-row').exists()).toBe(true))

    expect(list.find('.sold-out-toggle').exists()).toBe(false)
  })

  it('offers to activate it again without asking a question first', async () => {
    const urls = stubFetchWith(DEACTIVATED_ITEM)

    const list = mountList()
    await list.get('.show-deactivated input').setValue(true)
    await vi.waitFor(() => expect(list.find('.item-row').exists()).toBe(true))
    await list.get('.reactivate').trigger('click')

    await vi.waitFor(() => expect(urls).toContain(`/api/admin/items/${ITEM_ID}/activate`))
  })
})

describe('the sold-out button beside an item', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('marks that item sold out at its own address', async () => {
    const urls = stubFetch()

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.item-row').exists()).toBe(true))
    await list.get('.sold-out-toggle').trigger('click')

    await vi.waitFor(() =>
      expect(urls).toContain(`/api/admin/items/${ITEM_ID}/availability`),
    )
  })
})

describe('the station checkboxes on an item', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('show the station an item is already assigned to as ticked', async () => {
    stubFetch()

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.item-row').exists()).toBe(true))
    await list.get('.edit').trigger('click')
    await vi.waitFor(() => expect(list.find('.assignment-editor').exists()).toBe(true))

    const checkbox = list.get('.assignment-editor input[type="checkbox"]')
    expect((checkbox.element as HTMLInputElement).checked).toBe(true)
  })

})

describe('adding an item', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
  })

  it('asks for the item in a dialog instead of on the page', async () => {
    stubFetch()

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.new-item').exists()).toBe(true))

    expect(document.querySelector('.new-item-dialog')).toBeNull()

    await list.get('.new-item').trigger('click')

    await vi.waitFor(() => expect(document.querySelector('.new-item-dialog')).not.toBeNull())
  })

  it('drops the complaint about a missing station once the edit form is closed again', async () => {
    stubFetch()

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.edit').exists()).toBe(true))
    await list.get('.edit').trigger('click')

    await list.findComponent(ItemForm).vm.$emit('save', {
      itemId: ITEM_ID,
      name: 'Bratwurst',
      categoryName: 'Speisen',
      priceCents: 350,
      productionMinutes: null,
      stationIds: [],
    })
    await vi.waitFor(() => expect(useAdminItemsStore().errorMessage).not.toBeNull())

    await list.get('.edit').trigger('click')

    expect(list.find('.admin-items .error').exists()).toBe(false)
  })

  it('drops the complaint about a missing station once the dialog is cancelled', async () => {
    stubFetch()

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.new-item').exists()).toBe(true))
    await list.get('.new-item').trigger('click')
    await vi.waitFor(() => expect(document.querySelector('.new-item-dialog')).not.toBeNull())

    await list.findComponent(NewItemDialog).vm.$emit('save', {
      itemId: null,
      name: 'Pommes',
      categoryName: 'Speisen',
      priceCents: 300,
      productionMinutes: null,
      stationIds: [],
    })
    await vi.waitFor(() => expect(useAdminItemsStore().errorMessage).not.toBeNull())

    await list.findComponent(NewItemDialog).vm.$emit('cancel')
    await vi.waitFor(() => expect(document.querySelector('.new-item-dialog')).toBeNull())

    expect(list.find('.admin-items .error').exists()).toBe(false)
  })

})

