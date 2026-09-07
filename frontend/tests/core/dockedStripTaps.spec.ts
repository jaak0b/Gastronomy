import { describe, expect, it } from 'vitest'
import {
  MILLISECONDS_A_DOCKED_STRIP_IGNORES_AFTER_A_SCROLL,
  PIXELS_A_FINGER_MAY_TRAVEL_AND_STILL_BE_A_TAP,
  theFingerLeftTheSpotItPressed,
  theTapReachesTheButton,
} from '../../src/core/dockedStripTaps'

const A_SCROLL_ENDED_AT = 10_000

describe('a tap that lands on a docked strip', () => {
  it('reaches the button when the waiter has not scrolled at all', () => {
    expect(
      theTapReachesTheButton({
        atMillisecond: A_SCROLL_ENDED_AT,
        lastScrollAtMillisecond: null,
        theFingerLeftTheSpotItPressed: false,
      }),
    ).toBe(true)
  })

  it('reaches the button once the waiter has stopped, looked and pressed', () => {
    expect(
      theTapReachesTheButton({
        atMillisecond: A_SCROLL_ENDED_AT + 2000,
        lastScrollAtMillisecond: A_SCROLL_ENDED_AT,
        theFingerLeftTheSpotItPressed: false,
      }),
    ).toBe(true)
  })

  it('is ignored while it arrives on the heels of the list still flying', () => {
    expect(
      theTapReachesTheButton({
        atMillisecond: A_SCROLL_ENDED_AT + 100,
        lastScrollAtMillisecond: A_SCROLL_ENDED_AT,
        theFingerLeftTheSpotItPressed: false,
      }),
    ).toBe(false)
  })

  it('reaches the button the moment the waiting time is over', () => {
    expect(
      theTapReachesTheButton({
        atMillisecond: A_SCROLL_ENDED_AT + MILLISECONDS_A_DOCKED_STRIP_IGNORES_AFTER_A_SCROLL,
        lastScrollAtMillisecond: A_SCROLL_ENDED_AT,
        theFingerLeftTheSpotItPressed: false,
      }),
    ).toBe(true)
  })

  it('is ignored when the finger moved before it lifted, however long ago the list stopped', () => {
    expect(
      theTapReachesTheButton({
        atMillisecond: A_SCROLL_ENDED_AT + 60_000,
        lastScrollAtMillisecond: A_SCROLL_ENDED_AT,
        theFingerLeftTheSpotItPressed: true,
      }),
    ).toBe(false)
  })

  it('is ignored when the finger moved and nothing was ever scrolled', () => {
    expect(
      theTapReachesTheButton({
        atMillisecond: A_SCROLL_ENDED_AT,
        lastScrollAtMillisecond: null,
        theFingerLeftTheSpotItPressed: true,
      }),
    ).toBe(false)
  })
})

describe('how far a finger may travel and still be pressing a button', () => {
  it('counts a finger that barely trembled as one that stayed put', () => {
    expect(theFingerLeftTheSpotItPressed({ fromX: 100, fromY: 200, toX: 102, toY: 203 })).toBe(false)
  })

  it('counts a finger that was dragged up the screen as one that left', () => {
    expect(theFingerLeftTheSpotItPressed({ fromX: 100, fromY: 200, toX: 100, toY: 260 })).toBe(true)
  })

  it('measures the travel sideways as well, so a swipe across the strip counts', () => {
    expect(theFingerLeftTheSpotItPressed({ fromX: 100, fromY: 200, toX: 40, toY: 200 })).toBe(true)
  })

  it('lets a finger travel the whole allowance without counting as a drag', () => {
    expect(
      theFingerLeftTheSpotItPressed({
        fromX: 100,
        fromY: 200,
        toX: 100 + PIXELS_A_FINGER_MAY_TRAVEL_AND_STILL_BE_A_TAP,
        toY: 200,
      }),
    ).toBe(false)
  })
})
