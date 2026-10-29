import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { enableAutoUnmount } from '@vue/test-utils'
import {
  aBrowserWithoutUserActivation,
  pressBack,
  theAppWasLeftByBack,
  theNextMoveThroughTheHistory,
} from './support/browserHistory'
import { clickOn, isOnScreen, textOnScreen, waitUntil } from './support/dom'
import {
  THE_AREA_LIST,
  THE_PLAN_OF_HALLE,
  THE_SHEET_OF_TISCH_12,
  aLaptopWithTwoAreas,
  aWaiterPhoneOnTheAreaList,
  aWaiterPhoneTabOpenedAtHome,
  aWasserOnTheOrderForTisch12,
  openTheArea,
  takeTheOrder,
  tapTheTable,
  whatThePhoneShows,
} from './waiterPhoneBackPressFixture'

vi.mock('@microsoft/signalr', async () => (await import('./support/hubConnection')).signalrModuleFake())

enableAutoUnmount(afterEach)

beforeEach(() => {
  aWaiterPhoneTabOpenedAtHome()
  aLaptopWithTwoAreas()
})

afterEach(() => {
  aBrowserWithoutUserActivation()
})

describe('back on the waiter phone', () => {
  it('returns from the sheet of a table to the plan of the same area, without loading the app again', async () => {
    await aWaiterPhoneOnTheAreaList()
    await openTheArea('area-halle')
    await tapTheTable('table-12')

    await pressBack()

    expect(whatThePhoneShows()).toEqual(THE_PLAN_OF_HALLE)
  })

  it('goes from the order screen to the sheet of the table, to the plan of the area and to the area list, one step each', async () => {
    await aWaiterPhoneOnTheAreaList()
    await openTheArea('area-halle')
    await tapTheTable('table-12')
    await takeTheOrder()

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

  it('reaches the doors from the area list, the first screen', async () => {
    await aWaiterPhoneOnTheAreaList()
    await openTheArea('area-halle')
    await pressBack()

    await pressBack()

    expect(theAppWasLeftByBack()).toBe(true)
  })

  it('closes an open dialog and stays on the screen', async () => {
    await aWaiterPhoneOnTheAreaList()
    await openTheArea('area-halle')
    await clickOn('[data-test="settings"]')
    await waitUntil(() => expect(isOnScreen('[data-test="settings-sheet"]')).toBe(true))

    await pressBack()

    expect({ settings: isOnScreen('[data-test="settings-sheet"]'), shows: whatThePhoneShows() }).toEqual({
      settings: false,
      shows: THE_PLAN_OF_HALLE,
    })
  })

  it('goes on up from the plan after the way up from the order screen, instead of back to the order screen', async () => {
    await aWaiterPhoneOnTheAreaList()
    await openTheArea('area-halle')
    await tapTheTable('table-12')
    await takeTheOrder()
    const theWayUp = theNextMoveThroughTheHistory()
    await clickOn('[data-test="header-back"]')
    await theWayUp

    await pressBack()

    expect(whatThePhoneShows()).toEqual(THE_AREA_LIST)
  })

  it('keeps the unsent order exactly as it was when the waiter leaves the order screen by back', async () => {
    const laptop = aLaptopWithTwoAreas()
    await aWaiterPhoneOnTheAreaList()
    await aWasserOnTheOrderForTisch12()
    await openTheArea('area-halle')
    await tapTheTable('table-12')
    await takeTheOrder()
    const theOrderBefore = localStorage.getItem('draftOrder')

    await pressBack()

    expect({ order: localStorage.getItem('draftOrder'), sent: laptop.callsTo('POST', '/api/orders') }).toEqual({
      order: theOrderBefore,
      sent: [],
    })
  })
})

describe('the way up to the list of areas', () => {
  it('names the list of areas over the plan of an area', async () => {
    await aWaiterPhoneOnTheAreaList()

    await openTheArea('area-halle')

    expect(textOnScreen('[data-test="header-back"]').trim()).toBe('Tischpläne')
  })

  it('adds no step, so back from the area list reaches the doors instead of the area', async () => {
    await aWaiterPhoneOnTheAreaList()
    await openTheArea('area-halle')
    const theWayUp = theNextMoveThroughTheHistory()
    await clickOn('[data-test="header-back"]')
    await theWayUp

    await pressBack()

    expect(theAppWasLeftByBack()).toBe(true)
  })
})
