import { describe, expect, it } from 'vitest'
import { keyboardInsetPx } from '../../../src/phone/core/keyboardInset'

describe('the room an on-screen keyboard takes from the layout viewport', () => {
  it('takes nothing while the keyboard is closed and both heights match', () => {
    expect(keyboardInsetPx(800, 800)).toBe(0)
  })

  it('is the difference between the two heights while the keyboard is open', () => {
    expect(keyboardInsetPx(800, 400)).toBe(400)
  })

  it('takes nothing while the visual viewport is taller than the layout viewport', () => {
    expect(keyboardInsetPx(800, 900)).toBe(0)
  })

  it('rounds a fractional difference to whole pixels', () => {
    expect(keyboardInsetPx(800.4, 400)).toBe(400)
    expect(keyboardInsetPx(800, 399.4)).toBe(401)
  })
})
