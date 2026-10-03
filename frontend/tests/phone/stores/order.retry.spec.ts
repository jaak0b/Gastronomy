import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useCatalogStore } from '../../../src/phone/stores/catalog'
import { SEND_TIMEOUT_MS } from '../../../src/phone/core/sendTimeout'
import { useOrderStore } from '../../../src/phone/stores/order'
import { menuWithWaterAtTheBar } from './orderFixture'
import { stubLaptop, answer, refusal, inTurn, neverAnswers, type LaptopReply } from '../../support/laptop'

describe('an order the laptop answered only after it had already stayed silent once', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    useCatalogStore().catalog = menuWithWaterAtTheBar()
    localStorage.clear()
    vi.useFakeTimers()
  })

  afterEach(() => {
    vi.useRealTimers()
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

  function aLaptopThatSaysNothingAndThenCannotSave() {
    stubLaptop().answersEverythingElse(inTurn(neverAnswers(), answer({}, 503)))
  }

  async function anOrderTheLaptopNeverAnsweredAndThenRefused() {
    aLaptopThatSaysNothingAndThenCannotSave()
    const order = useOrderStore()
    order.addItem(waterLine())
    order.setTable('Tisch 3')
    const firstAttempt = order.send('leaveOpen')
    await vi.advanceTimersByTimeAsync(SEND_TIMEOUT_MS)
    await firstAttempt

    await order.sendAgain()
    return order
  }

  it('takes no change, because the laptop may still hold the order from the attempt it never answered', async () => {
    const order = await anOrderTheLaptopNeverAnsweredAndThenRefused()

    expect(order.changesAreRefused).toBe(true)
  })

  it('offers the paper route at once, because the refusal names nothing the waiter may change', async () => {
    const order = await anOrderTheLaptopNeverAnsweredAndThenRefused()

    expect(order.onlyWritingItDownIsLeft).toBe(true)
  })

  it('keeps every line of the order the silent attempt may have carried away', async () => {
    const order = await anOrderTheLaptopNeverAnsweredAndThenRefused()

    order.dropLine(0)

    expect(order.draft.lines).toHaveLength(1)
  })

  it('stays closed for changes after a reload, because the silence is written down with the order', async () => {
    await anOrderTheLaptopNeverAnsweredAndThenRefused()

    setActivePinia(createPinia())
    const afterTheReload = useOrderStore()

    expect(afterTheReload.changesAreRefused).toBe(true)
  })

  it('opens the next order for changes once the waiter has written this one down', async () => {
    const order = await anOrderTheLaptopNeverAnsweredAndThenRefused()

    order.startNextOrderAfterWritingItDown()

    expect(order.changesAreRefused).toBe(false)
  })
})

describe('an order the laptop refused with a reason after it had stayed silent once', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    useCatalogStore().catalog = menuWithWaterAtTheBar()
    localStorage.clear()
    vi.useFakeTimers()
  })

  afterEach(() => {
    vi.useRealTimers()
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

  function aLaptopThatSaysNothingAndThenAnswersWith(secondAnswer: LaptopReply) {
    stubLaptop().answersEverythingElse(inTurn(neverAnswers(), secondAnswer))
  }


  async function anOrderTheLaptopNeverAnsweredAndThenAnsweredWith(secondAnswer: LaptopReply) {
    aLaptopThatSaysNothingAndThenAnswersWith(secondAnswer)
    const order = useOrderStore()
    order.addItem(waterLine())
    order.setTable('Tisch 3')
    const firstAttempt = order.send('leaveOpen')
    await vi.advanceTimersByTimeAsync(SEND_TIMEOUT_MS)
    await firstAttempt

    await order.sendAgain()
    return order
  }

  it('takes changes again, because the reason proves the laptop never took the order', async () => {
    const order = await anOrderTheLaptopNeverAnsweredAndThenAnsweredWith(
      refusal('errors.order.itemSoldOut', { status: 422, code: 'UnprocessableEntity' }),
    )

    expect(order.changesAreRefused).toBe(false)
  })

  it('takes changes again for a 400 reason too', async () => {
    const order = await anOrderTheLaptopNeverAnsweredAndThenAnsweredWith(
      refusal('errors.order.unknownItem', { status: 400, code: 'UnprocessableEntity' }),
    )

    expect(order.changesAreRefused).toBe(false)
  })

  it('keeps the reason the laptop gave, so the waiter can put it right', async () => {
    const order = await anOrderTheLaptopNeverAnsweredAndThenAnsweredWith(
      refusal('errors.order.itemSoldOut', { status: 422, code: 'UnprocessableEntity' }),
    )

    expect(order.failure?.key).toBe('errors.order.itemSoldOut')
  })

  it('keeps the paper route away, because the waiter has something to fix', async () => {
    const order = await anOrderTheLaptopNeverAnsweredAndThenAnsweredWith(
      refusal('errors.order.itemSoldOut', { status: 422, code: 'UnprocessableEntity' }),
    )

    expect(order.onlyWritingItDownIsLeft).toBe(false)
  })

  it('stays open for changes after a reload, because the reason was written down', async () => {
    await anOrderTheLaptopNeverAnsweredAndThenAnsweredWith(
      refusal('errors.order.itemSoldOut', { status: 422, code: 'UnprocessableEntity' }),
    )

    setActivePinia(createPinia())
    const afterTheReload = useOrderStore()

    expect(afterTheReload.changesAreRefused).toBe(false)
  })

  it('keeps the words the refusal fills in after a reload, so its notice still names the item', async () => {
    await anOrderTheLaptopNeverAnsweredAndThenAnsweredWith(
      refusal('errors.order.itemSoldOut', { status: 422, code: 'UnprocessableEntity', parameters: {
        name: 'Wasser',
        catalogItemId: 'item-wasser',
      } }),
    )

    setActivePinia(createPinia())
    const afterTheReload = useOrderStore()

    expect(afterTheReload.failure).toEqual({
      key: 'errors.order.itemSoldOut',
      parameters: { name: 'Wasser', catalogItemId: 'item-wasser' },
    })
  })

  it('stays closed when a 400 arrives without the laptop wording', async () => {
    const order = await anOrderTheLaptopNeverAnsweredAndThenAnsweredWith(
      answer({}, 400),
    )

    expect(order.changesAreRefused).toBe(true)
  })
})
