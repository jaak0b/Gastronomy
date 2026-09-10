import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import {
  PICKED_FESTIVAL_STORAGE_KEY,
  forgetPickedFestival,
  readPickedFestival,
  writePickedFestival,
} from '../../src/core/pickedFestival'

describe('the festival the admin has open', () => {
  beforeEach(() => {
    localStorage.clear()
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('is remembered under its own key, so a reload lands on the same festival', () => {
    writePickedFestival('fest-1')

    expect(localStorage.getItem(PICKED_FESTIVAL_STORAGE_KEY)).toBe('fest-1')
    expect(readPickedFestival()).toBe('fest-1')
  })

  it('is nothing once it has been forgotten', () => {
    writePickedFestival('fest-1')

    forgetPickedFestival()

    expect(readPickedFestival()).toBeNull()
  })

  it('is nothing when the browser refuses to hand its storage over', () => {
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new Error('storage is blocked')
    })

    expect(readPickedFestival()).toBeNull()
  })

  it('reports that it could not be remembered when the browser refuses to store it', () => {
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('storage is blocked')
    })

    expect(writePickedFestival('fest-1')).toBe(false)
  })
})
