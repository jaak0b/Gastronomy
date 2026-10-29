import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { enableAutoUnmount } from '@vue/test-utils'
import { aHold, answer, heldUntil } from './support/laptop'
import {
  aBrowserWithoutUserActivation,
  pressBack,
  theNextMoveThroughTheHistory,
} from './support/browserHistory'
import { clickOn, waitUntil } from './support/dom'
import {
  AN_ACCEPTED_ORDER,
  THE_AREA_LIST,
  THE_PLAN_OF_HALLE,
  THE_SHEET_OF_TISCH_12,
  aLaptopWithTwoAreas,
  aWaiterPhoneTabOpenedAtHome,
  theSummaryOfTheOrderForTisch12InHalle,
  whatThePhoneShows,
} from './waiterPhoneBackPressFixture'

vi.mock('@microsoft/signalr', async () => (await import('./support/hubConnection')).signalrModuleFake())

enableAutoUnmount(afterEach)

beforeEach(() => {
  aWaiterPhoneTabOpenedAtHome()
})

afterEach(() => {
  aBrowserWithoutUserActivation()
})

async function sendToSettleLater(): Promise<void> {
  const theWayHome = theNextMoveThroughTheHistory()
  await clickOn('[data-test="send-and-settle-later"]')
  await theWayHome
}

describe('sending an order to settle later', () => {
  it('lands on the plan of the area the order was taken in', async () => {
    aLaptopWithTwoAreas()
    await theSummaryOfTheOrderForTisch12InHalle()

    await sendToSettleLater()

    expect(whatThePhoneShows()).toEqual(THE_PLAN_OF_HALLE)
  })

  it('leaves the order flow once the laptop accepts, also when back was pressed while the order was on its way', async () => {
    const hold = aHold()
    aLaptopWithTwoAreas(heldUntil(hold.released, answer(AN_ACCEPTED_ORDER)))
    await theSummaryOfTheOrderForTisch12InHalle()
    await clickOn('[data-test="send-and-settle-later"]')
    await pressBack()

    hold.release()

    await waitUntil(() => expect(whatThePhoneShows()).toEqual(THE_PLAN_OF_HALLE))
  })

  it('lands on the plan of the area the order was taken in, however often back was pressed while it was on its way', async () => {
    const hold = aHold()
    aLaptopWithTwoAreas(heldUntil(hold.released, answer(AN_ACCEPTED_ORDER)))
    await theSummaryOfTheOrderForTisch12InHalle()
    await clickOn('[data-test="send-and-settle-later"]')
    await pressBack()
    await pressBack()
    await pressBack()

    hold.release()

    await waitUntil(() => expect(whatThePhoneShows()).toEqual(THE_PLAN_OF_HALLE))
  })

  it('goes from the plan of the area to the area list on back, never into the sent order', async () => {
    aLaptopWithTwoAreas()
    await theSummaryOfTheOrderForTisch12InHalle()
    await sendToSettleLater()

    await pressBack()

    expect(whatThePhoneShows()).toEqual(THE_AREA_LIST)
  })

  it('never shows the summary of the sent order when the waiter goes forward again', async () => {
    aLaptopWithTwoAreas()
    await theSummaryOfTheOrderForTisch12InHalle()
    await sendToSettleLater()

    const theWayForward = theNextMoveThroughTheHistory()
    window.history.go(3)
    await theWayForward

    await waitUntil(() => expect(whatThePhoneShows()).toEqual(THE_PLAN_OF_HALLE))
  })
})

describe('sending an order to settle it right away', () => {
  it('goes back from the settle view to the sheet of the table, the plan of the area and the area list, never into the sent order', async () => {
    aLaptopWithTwoAreas()
    await theSummaryOfTheOrderForTisch12InHalle()
    const theWayToTheSettleView = theNextMoveThroughTheHistory()
    await clickOn('[data-test="send-and-settle"]')
    await theWayToTheSettleView

    await pressBack()
    const afterTheFirst = whatThePhoneShows()
    await pressBack()
    const afterTheSecond = whatThePhoneShows()
    await pressBack()
    const afterTheThird = whatThePhoneShows()

    expect([afterTheFirst, afterTheSecond, afterTheThird]).toEqual([
      THE_SHEET_OF_TISCH_12,
      THE_PLAN_OF_HALLE,
      THE_AREA_LIST,
    ])
  })
})
