import { describe, expect, it } from 'vitest'
import { NOTHING_TO_LOAD, showsTheEmptyListText } from '../../../src/shared/core/emptyList'

describe('when a list shows the text of an empty list', () => {
  it('shows it once the list has loaded, did not fail and has no entry', () => {
    expect(showsTheEmptyListText({ hasLoaded: true, loadFailed: false }, 0)).toBe(true)
  })

  it('shows nothing before the first answer, because the list may still arrive', () => {
    expect(showsTheEmptyListText({ hasLoaded: false, loadFailed: false }, 0)).toBe(false)
  })

  it('shows nothing after a failed load, because the laptop never said the list is empty', () => {
    expect(showsTheEmptyListText({ hasLoaded: false, loadFailed: true }, 0)).toBe(false)
  })

  it('shows nothing when a reload failed after an earlier answer', () => {
    expect(showsTheEmptyListText({ hasLoaded: true, loadFailed: true }, 0)).toBe(false)
  })

  it('shows nothing while the list has an entry', () => {
    expect(showsTheEmptyListText({ hasLoaded: true, loadFailed: false }, 3)).toBe(false)
  })

  it('shows it for a list that nothing loads, as soon as it has no entry', () => {
    expect(showsTheEmptyListText(NOTHING_TO_LOAD, 0)).toBe(true)
  })
})
