import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useOrderStore } from '../../../src/phone/stores/order'
import { restoreSendProgress, saveDraft, saveSendProgress } from '../../../src/phone/core/draftCart'
import { stubLaptop, answer, neverAnswers, noConnection } from '../../support/laptop'

describe('an order whose send failed', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  function unreachableLaptop() {
    stubLaptop().answersEverythingElse(noConnection())
  }

  it('takes no further change, because the laptop may already hold the order as it was sent', async () => {
    unreachableLaptop()
    const order = useOrderStore()

    await order.send('leaveOpen')

    expect(order.changesAreRefused).toBe(true)
  })

  it('stays closed for changes while the retry is on its way', async () => {
    unreachableLaptop()
    const order = useOrderStore()
    await order.send('leaveOpen')

    const retry = order.sendAgain()
    const refusedWhileSending = order.changesAreRefused
    await retry

    expect(refusedWhileSending).toBe(true)
  })

  it('takes changes again as long as nothing has been sent', () => {
    const order = useOrderStore()

    expect(order.changesAreRefused).toBe(false)
  })

  it('opens the next order for changes once the waiter has written this one down', async () => {
    unreachableLaptop()
    const order = useOrderStore()
    await order.send('leaveOpen')

    order.startNextOrderAfterWritingItDown()

    expect(order.changesAreRefused).toBe(false)
  })

  it('empties the basket when the waiter has written the order down on paper', async () => {
    unreachableLaptop()
    const order = useOrderStore()
    order.addItem({
      catalogItemId: 'item-wasser',
      note: null,
      stationId: 'station-bar',
      name: 'Wasser',
    })
    await order.send('leaveOpen')

    order.startNextOrderAfterWritingItDown()

    expect(order.draft.lines).toEqual([])
  })

  it('says nothing about paper after the first failure', async () => {
    unreachableLaptop()
    const order = useOrderStore()

    await order.send('leaveOpen')

    expect(order.onlyWritingItDownIsLeft).toBe(false)
  })

  it('says paper is the way out once the second attempt has failed too', async () => {
    unreachableLaptop()
    const order = useOrderStore()
    await order.send('leaveOpen')

    await order.sendAgain()

    expect(order.onlyWritingItDownIsLeft).toBe(true)
  })
})

describe('an attempt the laptop never answers', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    vi.useFakeTimers()
    stubLaptop().answersEverythingElse(neverAnswers())
  })

  afterEach(() => {
    vi.useRealTimers()
    vi.unstubAllGlobals()
  })

  it('is still shown as on its way nine seconds in', async () => {
    const order = useOrderStore()

    void order.send('leaveOpen')
    await vi.advanceTimersByTimeAsync(9_000)

    expect(order.sendState).toBe('sending')
  })

  it('gives up after ten seconds rather than leaving the waiter watching the sending line', async () => {
    const order = useOrderStore()

    const attempt = order.send('leaveOpen')
    await vi.advanceTimersByTimeAsync(10_000)
    await attempt

    expect(order.sendState).toBe('failed')
  })

  it('says the laptop could not be reached, which is what giving up means here', async () => {
    const order = useOrderStore()

    const attempt = order.send('leaveOpen')
    await vi.advanceTimersByTimeAsync(10_000)
    await attempt

    expect(order.failure?.key).toBe('phone.review.errors.sendFailed')
  })
})

describe('an order the laptop could not save', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  function aLaptopThatCannotSave() {
    stubLaptop().answersEverythingElse(answer({}, 500))
  }

  it('takes changes again, because an answer arrived and no order was created', async () => {
    aLaptopThatCannotSave()
    const order = useOrderStore()

    await order.send('leaveOpen')

    expect(order.changesAreRefused).toBe(false)
  })

  it('says the laptop could not save it, rather than blaming the WiFi', async () => {
    aLaptopThatCannotSave()
    const order = useOrderStore()

    await order.send('leaveOpen')

    expect(order.failure?.key).toBe('errors.storage.databaseUnavailable')
  })

  it('counts no unanswered attempt, because the laptop answered', async () => {
    aLaptopThatCannotSave()
    const order = useOrderStore()

    await order.send('leaveOpen')

    expect(order.attemptsMade).toBe(0)
  })

  it('never sends the waiter to paper, however often the laptop answers that way', async () => {
    aLaptopThatCannotSave()
    const order = useOrderStore()
    await order.send('leaveOpen')

    await order.sendAgain()

    expect(order.onlyWritingItDownIsLeft).toBe(false)
  })

  it('stays open for changes after a reload, because no order was created', async () => {
    aLaptopThatCannotSave()
    const order = useOrderStore()
    await order.send('leaveOpen')

    setActivePinia(createPinia())
    const afterTheReload = useOrderStore()

    expect(afterTheReload.changesAreRefused).toBe(false)
  })
})

describe('an order sent from a phone the laptop no longer knows', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  function aLaptopThatNoLongerKnowsThisPhone(): void {
    stubLaptop().answersEverythingElse(answer({}, 401))
  }

  function anOrderOnThePhone(): void {
    saveDraft({
      festivalId: null,
      tableName: 'Tisch 5',
      lines: [{ catalogItemId: 'item-wasser', note: null, stationId: 'station-bar', name: 'Wasser', stationName: 'Bar' }],
      clientOrderId: 'c0ffee00-1111-4111-8111-111111111111',
      deliveryModes: {},
    })
  }

  function anOrderFrozenByAnAttemptTheLaptopNeverAnswered() {
    anOrderOnThePhone()
    saveSendProgress({
      state: 'sending',
      attempts: 1,
      failure: null,
      unresolvedAttempt: {
        clientOrderId: 'c0ffee00-1111-4111-8111-111111111111',
        tableName: 'Tisch 5',
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
    aLaptopThatNoLongerKnowsThisPhone()
    return useOrderStore()
  }

  it('leaves the sentence the waiter was already reading on the order', async () => {
    const order = anOrderFrozenByAnAttemptTheLaptopNeverAnswered()

    await order.sendAgain()

    expect(order.failure?.key).toBe('phone.review.errors.sendInterrupted')
  })

  it('leaves that sentence in storage, so it is still there once the phone is set up again', async () => {
    const order = anOrderFrozenByAnAttemptTheLaptopNeverAnswered()

    await order.sendAgain()

    expect(restoreSendProgress().failure?.key).toBe('phone.review.errors.sendInterrupted')
  })

  it('keeps the order frozen, because the silence before it is still unexplained', async () => {
    const order = anOrderFrozenByAnAttemptTheLaptopNeverAnswered()

    await order.sendAgain()

    expect(order.changesAreRefused).toBe(true)
  })

  it('counts no attempt, because the laptop never read the order', async () => {
    const order = anOrderFrozenByAnAttemptTheLaptopNeverAnswered()

    await order.sendAgain()

    expect(order.attemptsMade).toBe(1)
  })

  it('leaves the paper route where it was, so no dialog jumps in front of the waiter', async () => {
    const order = anOrderFrozenByAnAttemptTheLaptopNeverAnswered()

    await order.sendAgain()

    expect(order.onlyWritingItDownIsLeft).toBe(false)
  })

  it('leaves an order nothing had happened to open for changes', async () => {
    anOrderOnThePhone()
    aLaptopThatNoLongerKnowsThisPhone()
    const order = useOrderStore()

    await order.send('leaveOpen')

    expect(order.changesAreRefused).toBe(false)
  })

  it('says nothing about that order, because nothing about it was decided', async () => {
    anOrderOnThePhone()
    aLaptopThatNoLongerKnowsThisPhone()
    const order = useOrderStore()

    await order.send('leaveOpen')

    expect(order.failure).toBeNull()
    expect(order.sendState).toBe('idle')
  })

  it('counts no attempt on that order either', async () => {
    anOrderOnThePhone()
    aLaptopThatNoLongerKnowsThisPhone()
    const order = useOrderStore()

    await order.send('leaveOpen')

    expect(order.attemptsMade).toBe(0)
    expect(restoreSendProgress().attempts).toBe(0)
  })
})
