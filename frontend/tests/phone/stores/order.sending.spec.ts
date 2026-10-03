import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useCatalogStore } from '../../../src/phone/stores/catalog'
import { useOrderStore, ARRIVAL_NOTICE_MS } from '../../../src/phone/stores/order'
import { DRAFT_STORAGE_KEY, SEND_PROGRESS_STORAGE_KEY, restoreDraft, restoreSendProgress } from '../../../src/phone/core/draftCart'
import { menuWithWaterAtTheBar, answerWith } from './orderFixture'
import { stubLaptop, neverAnswers, noConnection } from '../../support/laptop'

describe('the notice that an order has arrived', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    vi.useFakeTimers()
  })

  afterEach(() => {
    vi.useRealTimers()
    vi.unstubAllGlobals()
  })

  it('takes itself off the screen so the server is not left tapping it away', async () => {
    answerWith(0)
    const order = useOrderStore()

    await order.send('leaveOpen')
    expect(order.sendState).toBe('accepted')

    vi.advanceTimersByTime(ARRIVAL_NOTICE_MS)

    expect(order.sendState).toBe('idle')
  })

})

describe('the identity an order carries', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('is there from the moment the order exists, long before anybody presses send', () => {
    const order = useOrderStore()

    expect(order.draft.clientOrderId).not.toBeNull()
  })

  it('is written down at once, so a page that dies mid-send comes back as the same order', () => {
    const order = useOrderStore()

    const stored = JSON.parse(localStorage.getItem(DRAFT_STORAGE_KEY) ?? '{}')

    expect(stored.clientOrderId).toBe(order.draft.clientOrderId)
  })

  it('is a new one for the order that follows an accepted one', async () => {
    answerWith(0)
    const order = useOrderStore()
    const sentOrder = order.draft.clientOrderId

    await order.send('leaveOpen')

    expect(order.draft.clientOrderId).not.toBe(sentOrder)
  })

  it('is a new one for the order that follows one written down on paper', async () => {
    stubLaptop().answersEverythingElse(noConnection())
    const order = useOrderStore()
    const writtenDownOrder = order.draft.clientOrderId
    await order.send('leaveOpen')

    order.startNextOrderAfterWritingItDown()

    expect(order.draft.clientOrderId).not.toBe(writtenDownOrder)
  })
})

describe('the table name an order goes out with', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    useCatalogStore().catalog = menuWithWaterAtTheBar()
    localStorage.clear()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('has no spaces around it, so the open list cannot hold the same table twice', async () => {
    answerWith(0)
    const order = useOrderStore()
    order.addItem({
      catalogItemId: 'item-wasser',
      note: null,
      stationId: 'station-bar',
      name: 'Wasser',
    })
    order.setTable(' Tisch 3 ')

    await order.send('leaveOpen')

    const sent = JSON.parse((vi.mocked(fetch).mock.calls[0][1] as RequestInit).body as string)
    expect(sent.tableName).toBe('Tisch 3')
  })
})

describe('an order the waiter has already pressed send on', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    useCatalogStore().catalog = menuWithWaterAtTheBar()
    localStorage.clear()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  function waterLine() {
    return {
      catalogItemId: 'item-wasser',
      note: null,
      stationId: 'station-bar',
      name: 'Wasser',
    }
  }

  function orderOnItsWayToTheLaptop() {
    stubLaptop().answersEverythingElse(neverAnswers())
    const order = useOrderStore()
    order.addItem(waterLine())
    order.setTable('Tisch 7')
    void order.send('leaveOpen')
    return order
  }

  it('is closed for changes from the press onwards, not only once the send has failed', () => {
    const order = orderOnItsWayToTheLaptop()

    expect(order.changesAreRefused).toBe(true)
  })

  it('takes no further item, because the laptop may already hold the order as it was sent', () => {
    const order = orderOnItsWayToTheLaptop()

    order.addItem({ ...waterLine(), catalogItemId: 'item-bier', name: 'Bier' })

    expect(order.draft.lines).toHaveLength(1)
  })

  it('takes no line off the order', () => {
    const order = orderOnItsWayToTheLaptop()

    order.dropLine(0)

    expect(order.draft.lines).toHaveLength(1)
  })

  it('takes no note on one of its lines', () => {
    const order = orderOnItsWayToTheLaptop()

    order.noteLine(0, 'ohne Eis')

    expect(order.draft.lines[0].note).toBeNull()
  })

  it('takes no other station for one of its lines', () => {
    const order = orderOnItsWayToTheLaptop()

    order.chooseStation(0, 'station-kueche')

    expect(order.draft.lines[0].stationId).toBe('station-bar')
  })

  it('takes no other table name', () => {
    const order = orderOnItsWayToTheLaptop()

    order.setTable('Tisch 9')

    expect(order.draft.tableName).toBe('Tisch 7')
  })

  it('takes no other delivery choice for a station', () => {
    const order = orderOnItsWayToTheLaptop()

    order.chooseDeliveryMode('station-bar', 'asItComes')

    expect(order.deliveryModeAt('station-bar')).toBe('together')
  })

  it('does not clear the lines that cannot be ordered', () => {
    const order = orderOnItsWayToTheLaptop()

    order.dropLinesThatCannotBeOrdered()

    expect(order.draft.lines).toHaveLength(1)
  })

  it('counts the attempt as it starts, so a tab that dies mid-send is not a free try', () => {
    const order = orderOnItsWayToTheLaptop()

    expect(order.attemptsMade).toBe(1)
  })

  it('writes the attempt down before the request leaves, so a reload still sees it', () => {
    orderOnItsWayToTheLaptop()

    const posted = JSON.parse((vi.mocked(fetch).mock.calls[0][1] as RequestInit).body as string)
    const stored = restoreSendProgress()

    expect(stored.state).toBe('sending')
    expect(stored.unresolvedAttempt).toEqual(posted)
  })

  it('stays on its way when the arrival notice of an earlier order is tapped away', () => {
    const order = orderOnItsWayToTheLaptop()

    order.dismissConfirmation()

    expect(order.sendState).toBe('sending')
  })
})

describe('an order the laptop accepted', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('leaves the next order open for changes', async () => {
    answerWith(0)
    const order = useOrderStore()

    await order.send('leaveOpen')

    expect(order.changesAreRefused).toBe(false)
  })

  it('leaves nothing in storage that would lock the next order after a reload', async () => {
    answerWith(0)
    const order = useOrderStore()

    await order.send('leaveOpen')

    expect(localStorage.getItem(SEND_PROGRESS_STORAGE_KEY)).toBeNull()
  })

  it('lets the next table be typed in straight away', async () => {
    answerWith(0)
    const order = useOrderStore()
    await order.send('leaveOpen')

    order.setTable('Tisch 8')

    expect(order.draft.tableName).toBe('Tisch 8')
  })
})

describe('the station name a line is given when it is added', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  function twoBarsAndTheDrinksTheyPour() {
    const catalog = useCatalogStore()
    catalog.catalog = {
      festival: null,
      categories: [],
      items: [
        {
          id: 'item-wasser',
          name: 'Wasser',
          categoryId: 'category-getraenke',
          priceCents: 200,
          sortOrder: 1,
          isAvailable: true,
          stationIds: ['station-theke-innen'],
          productionMinutes: null,
          isQueueIndependent: false,
        },
        {
          id: 'item-bier',
          name: 'Bier',
          categoryId: 'category-getraenke',
          priceCents: 420,
          sortOrder: 2,
          isAvailable: true,
          stationIds: ['station-theke-innen', 'station-theke-aussen'],
          productionMinutes: null,
          isQueueIndependent: false,
        },
      ],
      stations: [
        { id: 'station-theke-innen', name: 'Theke innen', sortOrder: 1 },
        { id: 'station-theke-aussen', name: 'Theke aussen', sortOrder: 2 },
      ],
    }
    return catalog
  }

  it('is the name of the one bar that pours it, which the waiter was never asked about', () => {
    twoBarsAndTheDrinksTheyPour()
    const order = useOrderStore()

    order.addItem({
      catalogItemId: 'item-wasser',
      note: null,
      stationId: 'station-theke-innen',
      name: 'Wasser',
    })

    expect(restoreDraft().draft.lines[0].stationName).toBe('Theke innen')
  })

  it('is the name of the bar the waiter picked when the phone asked', () => {
    twoBarsAndTheDrinksTheyPour()
    const order = useOrderStore()
    order.addItem({
      catalogItemId: 'item-bier',
      note: null,
      stationId: null,
      name: 'Bier',
    })

    order.chooseStation(0, 'station-theke-aussen')

    expect(restoreDraft().draft.lines[0].stationName).toBe('Theke aussen')
  })
})
