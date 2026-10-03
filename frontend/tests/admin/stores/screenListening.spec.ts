import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { fireHubEvent, forgetHubEvents, hubEventsRegistered } from '../../support/hubConnection'
import { stubLaptop, answer, type StubbedLaptop } from '../../support/laptop'

vi.mock('@microsoft/signalr', async () => (await import('../../support/hubConnection')).signalrModuleFake())

const { useConnectionStore } = await import('../../../src/shared/stores/connection')
const { useAdminEnrolmentStore } = await import('../../../src/admin/stores/enrolment')
const { useAdminCategoriesStore } = await import('../../../src/admin/stores/categories')
const { useAdminStaffStore } = await import('../../../src/admin/stores/staff')
const { useAdminStationsStore } = await import('../../../src/admin/stores/stations')
const { useAdminItemsStore } = await import('../../../src/admin/stores/items')
const { useAdminIngredientsStore } = await import('../../../src/admin/stores/ingredients')
const { useAdminFestivalStockStore } = await import('../../../src/admin/stores/festivalStock')

const EMPTY_LISTS = { staffMembers: [], stations: [] }

function listsLaptop(): StubbedLaptop {
  return stubLaptop().answersEverythingElse(answer(EMPTY_LISTS))
}

describe('the waiter list of the admin', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    forgetHubEvents()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('is reloaded while the screen that asked for it is open', async () => {
    const laptop = listsLaptop()
    useAdminStaffStore().listen()

    await useConnectionStore().refetchAll()

    expect(laptop.urls()).toEqual(['/api/admin/staff-members'])
  })

  it('is read again when the laptop says the configuration changed', async () => {
    const laptop = listsLaptop()
    useAdminStaffStore().listen()
    await useConnectionStore().connect({})
    laptop.calls.length = 0

    fireHubEvent('ConfigurationChanged')

    await vi.waitFor(() => expect(laptop.urls()).toEqual(['/api/admin/staff-members']))
  })

  it('is left alone once the admin has moved to another screen', async () => {
    const laptop = listsLaptop()
    const stopListening = useAdminStaffStore().listen()

    stopListening()
    await useConnectionStore().refetchAll()

    expect(laptop.urls()).toEqual([])
  })
})

describe('the station list of the admin', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    forgetHubEvents()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('is reloaded while the screen that asked for it is open', async () => {
    const laptop = listsLaptop()
    useAdminStationsStore().listen()

    await useConnectionStore().refetchAll()

    expect(laptop.urls()).toEqual(['/api/admin/stations'])
  })

  it('is read again when the laptop says the configuration changed', async () => {
    const laptop = listsLaptop()
    useAdminStationsStore().listen()
    await useConnectionStore().connect({})
    laptop.calls.length = 0

    fireHubEvent('ConfigurationChanged')

    await vi.waitFor(() => expect(laptop.urls()).toEqual(['/api/admin/stations']))
  })

  it('is left alone once the admin has moved to another screen', async () => {
    const laptop = listsLaptop()
    const stopListening = useAdminStationsStore().listen()

    stopListening()
    await useConnectionStore().refetchAll()

    expect(laptop.urls()).toEqual([])
  })
})

describe('the category list of the admin', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    forgetHubEvents()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('is reloaded while the screen that asked for it is open', async () => {
    const laptop = listsLaptop()
    useAdminCategoriesStore().listen()

    await useConnectionStore().refetchAll()

    expect(laptop.urls()).toEqual(['/api/admin/categories'])
  })

  it('is read again when the laptop says the configuration changed', async () => {
    const laptop = listsLaptop()
    useAdminCategoriesStore().listen()
    await useConnectionStore().connect({})
    laptop.calls.length = 0

    fireHubEvent('ConfigurationChanged')

    await vi.waitFor(() => expect(laptop.urls()).toEqual(['/api/admin/categories']))
  })

  it('is left alone once the admin has moved to another screen', async () => {
    const laptop = listsLaptop()
    const stopListening = useAdminCategoriesStore().listen()

    stopListening()
    await useConnectionStore().refetchAll()

    expect(laptop.urls()).toEqual([])
  })
})

describe('the item list of the admin', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    forgetHubEvents()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('is reloaded while the screen that asked for it is open', async () => {
    const laptop = listsLaptop()
    useAdminItemsStore().listen()

    await useConnectionStore().refetchAll()

    expect(laptop.urls()).toEqual(['/api/admin/items'])
  })

  it('stays on the festival whose page is open when it is read again', async () => {
    const laptop = listsLaptop()
    const items = useAdminItemsStore()
    items.listen()
    await items.loadAtTheFestival('fest-1')
    laptop.calls.length = 0

    await useConnectionStore().refetchAll()

    expect(laptop.urls()).toEqual(['/api/admin/items?festivalId=fest-1'])
  })

  it('is read again when the laptop says the configuration changed', async () => {
    const laptop = listsLaptop()
    useAdminItemsStore().listen()
    await useConnectionStore().connect({})
    laptop.calls.length = 0

    fireHubEvent('ConfigurationChanged')

    await vi.waitFor(() => expect(laptop.urls()).toEqual(['/api/admin/items']))
  })

  it('is left alone once the admin has moved to another screen', async () => {
    const laptop = listsLaptop()
    const stopListening = useAdminItemsStore().listen()

    stopListening()
    await useConnectionStore().refetchAll()

    expect(laptop.urls()).toEqual([])
  })
})

describe('the ingredient list of the admin', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    forgetHubEvents()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('is reloaded while the screen that asked for it is open', async () => {
    const laptop = listsLaptop()
    useAdminIngredientsStore().listen()

    await useConnectionStore().refetchAll()

    expect(laptop.urls()).toEqual(['/api/admin/ingredients'])
  })

  it('is read again when the laptop says the configuration changed', async () => {
    const laptop = listsLaptop()
    useAdminIngredientsStore().listen()
    await useConnectionStore().connect({})
    laptop.calls.length = 0

    fireHubEvent('ConfigurationChanged')

    await vi.waitFor(() => expect(laptop.urls()).toEqual(['/api/admin/ingredients']))
  })

  it('is left alone once the admin has moved to another screen', async () => {
    const laptop = listsLaptop()
    const stopListening = useAdminIngredientsStore().listen()

    stopListening()
    await useConnectionStore().refetchAll()

    expect(laptop.urls()).toEqual([])
  })
})

describe('the stock of the festival whose page is open', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    forgetHubEvents()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  async function listenOnTheFestivalPage(): Promise<{ laptop: StubbedLaptop; stop: () => void }> {
    const laptop = listsLaptop()
    const stock = useAdminFestivalStockStore()
    const stop = stock.listen()
    await stock.loadForFestival('fest-1')
    laptop.calls.length = 0
    return { laptop, stop }
  }

  it('is reloaded on a reconnect', async () => {
    const { laptop } = await listenOnTheFestivalPage()

    await useConnectionStore().refetchAll()

    expect(laptop.urls()).toEqual(['/api/admin/festivals/fest-1/ingredients'])
  })

  it('is read again when the laptop says the configuration changed', async () => {
    const { laptop } = await listenOnTheFestivalPage()
    await useConnectionStore().connect({})
    laptop.calls.length = 0

    fireHubEvent('ConfigurationChanged')

    await vi.waitFor(() => expect(laptop.urls()).toEqual(['/api/admin/festivals/fest-1/ingredients']))
  })

  it('is read again when the laptop says the orders changed, because orders use up the stock', async () => {
    const { laptop } = await listenOnTheFestivalPage()
    await useConnectionStore().connect({})
    laptop.calls.length = 0

    fireHubEvent('OrdersChanged')

    await vi.waitFor(() => expect(laptop.urls()).toEqual(['/api/admin/festivals/fest-1/ingredients']))
  })

  it('is left alone once the admin has moved to another screen', async () => {
    const { laptop, stop } = await listenOnTheFestivalPage()

    stop()
    await useConnectionStore().refetchAll()

    expect(laptop.urls()).toEqual([])
  })
})

describe('the handler that answers a finished enrolment', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    hubEventsRegistered.length = 0
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('sits on the hub while the screen that asked for it is open', async () => {
    listsLaptop()
    useAdminEnrolmentStore().listen(() => undefined)

    await useConnectionStore().connect({})

    expect(hubEventsRegistered).toEqual(['EnrolmentCompleted'])
  })

  it('is taken off the hub once the admin has moved to another screen', async () => {
    listsLaptop()
    const stopListening = useAdminEnrolmentStore().listen(() => undefined)

    stopListening()
    await useConnectionStore().connect({})

    expect(hubEventsRegistered).toEqual([])
  })
})
