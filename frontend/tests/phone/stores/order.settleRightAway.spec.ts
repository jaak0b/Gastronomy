import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useCatalogStore } from '../../../src/phone/stores/catalog'
import { useOrderStore } from '../../../src/phone/stores/order'
import { useSessionStore } from '../../../src/shared/stores/session'
import { useOpenItemsStore } from '../../../src/phone/stores/openItems'
import { menuWithWaterAtTheBar, theLaptopPlacesTheOrderAfter } from './orderFixture'

describe('an accepted order the waiter settles right away', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    useCatalogStore().catalog = menuWithWaterAtTheBar()
    useSessionStore().deviceToken = 'token-here'
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  function anOrderForTableThree() {
    const order = useOrderStore()
    order.addItem({ catalogItemId: 'item-wasser', note: null, stationId: 'station-bar', name: 'Wasser' })
    order.setTable(' Tisch 3 ')
    return order
  }

  it('leaves the open items untouched, because only the screen that navigates may hand the order over', async () => {
    theLaptopPlacesTheOrderAfter(0)
    const order = anOrderForTableThree()

    await order.send('settleRightAway')

    const openItems = useOpenItemsStore()
    expect({ table: openItems.lookupName, ticked: openItems.selectedItemIds }).toEqual({
      table: null,
      ticked: [],
    })
  })

  it('offers the table as sent with every item of every station, exactly once', async () => {
    theLaptopPlacesTheOrderAfter(0)
    const order = anOrderForTableThree()

    await order.send('settleRightAway')

    expect([order.takeTheOrderAcceptedForSettling(), order.takeTheOrderAcceptedForSettling()]).toEqual([
      { tableName: 'Tisch 3', itemIds: ['new-1', 'new-2'] },
      null,
    ])
  })

  it('offers nothing to settle when the waiter settles later', async () => {
    theLaptopPlacesTheOrderAfter(0)
    const order = anOrderForTableThree()

    await order.send('leaveOpen')

    expect(order.takeTheOrderAcceptedForSettling()).toBeNull()
  })

  it('still offers the order to settle when only the retry got through', async () => {
    theLaptopPlacesTheOrderAfter(1)
    const order = anOrderForTableThree()
    await order.send('settleRightAway')

    await order.sendAgain()

    expect(order.takeTheOrderAcceptedForSettling()).toEqual({
      tableName: 'Tisch 3',
      itemIds: ['new-1', 'new-2'],
    })
  })

  it('discards an order nobody took to settle when the next order is sent', async () => {
    theLaptopPlacesTheOrderAfter(0)
    const order = anOrderForTableThree()
    await order.send('settleRightAway')
    anOrderForTableThree()

    await order.send('leaveOpen')

    expect(order.takeTheOrderAcceptedForSettling()).toBeNull()
  })
})
