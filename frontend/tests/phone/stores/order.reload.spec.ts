import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useOrderStore } from '../../../src/phone/stores/order'
import { DRAFT_STORAGE_KEY, saveSendProgress } from '../../../src/phone/core/draftCart'
import { answerWith } from './orderFixture'
import { stubLaptop, noConnection } from '../../support/laptop'

describe('an order in progress that could not be read back', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('is reported to the server instead of disappearing in silence', () => {
    localStorage.setItem(DRAFT_STORAGE_KEY, 'not json')

    const order = useOrderStore()

    expect(order.draftWasLost).toBe(true)
  })

  it('leaves the basket empty, because there is nothing left to put back', () => {
    localStorage.setItem(DRAFT_STORAGE_KEY, 'not json')

    const order = useOrderStore()

    expect(order.draft.lines).toEqual([])
  })

  it('is not reported when the phone simply had nothing stored', () => {
    const order = useOrderStore()

    expect(order.draftWasLost).toBe(false)
  })

  it('is not reported when the order in progress came back in full', () => {
    localStorage.setItem(
      DRAFT_STORAGE_KEY,
      JSON.stringify({
        festivalId: null,
        tableName: 'Tisch 12',
        clientOrderId: null,
        deliveryModes: {},
        lines: [],
      }),
    )

    const order = useOrderStore()

    expect(order.draftWasLost).toBe(false)
  })

  it('stops being reported once the server has tapped the notice away', () => {
    localStorage.setItem(DRAFT_STORAGE_KEY, 'not json')
    const order = useOrderStore()

    order.dismissDraftLoss()

    expect(order.draftWasLost).toBe(false)
  })

  it('stops being reported once the server starts entering the order again', () => {
    localStorage.setItem(DRAFT_STORAGE_KEY, 'not json')
    const order = useOrderStore()

    order.addItem({
      catalogItemId: 'item-1',
      note: null,
      stationId: null,
      name: 'Bratwurst',
    })

    expect(order.draftWasLost).toBe(false)
  })

  it('is not raised by the empty draft that follows a sent order', async () => {
    answerWith(0)
    const order = useOrderStore()

    await order.send('leaveOpen')

    expect(order.draftWasLost).toBe(false)
  })
})

describe('an order the page was still sending when it was loaded again', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  function anOrderLeftOnItsWay() {
    localStorage.setItem(
      DRAFT_STORAGE_KEY,
      JSON.stringify({
        festivalId: null,
        tableName: 'Tisch 4',
        lines: [
          {
            catalogItemId: 'item-wasser',
            note: null,
            stationId: 'station-bar',
            name: 'Wasser',
            stationName: 'Bar',
          },
        ],
        clientOrderId: 'c0ffee00-1111-4111-8111-111111111111',
        deliveryModes: {},
      }),
    )
    saveSendProgress({
      state: 'sending',
      attempts: 1,
      failure: null,
      unresolvedAttempt: {
        clientOrderId: 'c0ffee00-1111-4111-8111-111111111111',
        tableName: 'Tisch 4',
        items: [
          {
            catalogItemId: 'item-wasser',
            unitPriceCents: 200,
            note: null,
            stationId: 'station-bar',
          },
        ],
        deliveryModes: [],
      },
    })
  }

  it('is reported as failed, because the answer to that attempt died with the page', () => {
    anOrderLeftOnItsWay()

    const order = useOrderStore()

    expect(order.sendState).toBe('failed')
  })

  it('stays closed for changes, so the next table is not built into it', () => {
    anOrderLeftOnItsWay()

    const order = useOrderStore()

    expect(order.changesAreRefused).toBe(true)
  })

  it('says the attempt was cut off instead of blaming the WiFi', () => {
    anOrderLeftOnItsWay()

    const order = useOrderStore()

    expect(order.failure).toEqual({ key: 'phone.review.errors.sendInterrupted' })
  })

  it('comes back with the identity of the attempt, so a retry cannot create a second order', () => {
    anOrderLeftOnItsWay()

    const order = useOrderStore()

    expect(order.draft.clientOrderId).toBe('c0ffee00-1111-4111-8111-111111111111')
  })
})

describe('an order whose send failed, after the page is loaded again', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  async function aFailedSend() {
    stubLaptop().answersEverythingElse(noConnection())
    const order = useOrderStore()
    order.setTable('Tisch 2')
    await order.send('leaveOpen')
  }

  it('is still refused every change, because the freeze has to survive a reload', async () => {
    await aFailedSend()

    setActivePinia(createPinia())
    const afterTheReload = useOrderStore()

    expect(afterTheReload.changesAreRefused).toBe(true)
  })

  it('keeps the count of attempts, so the second failure is still the second one', async () => {
    await aFailedSend()

    setActivePinia(createPinia())
    const afterTheReload = useOrderStore()
    await afterTheReload.sendAgain()

    expect(afterTheReload.onlyWritingItDownIsLeft).toBe(true)
  })
})
