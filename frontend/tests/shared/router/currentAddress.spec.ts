import { describe, expect, it } from 'vitest'
import { currentAddress } from '../../../src/shared/router/currentAddress'

describe('the address of the page on display', () => {
  it('is its path with the query and the fragment, as the boot script compares it', () => {
    window.history.replaceState({}, '', '/admin/festivals/fest-1?tab=stock#verlauf')

    expect(currentAddress()).toBe('/admin/festivals/fest-1?tab=stock#verlauf')
  })
})
