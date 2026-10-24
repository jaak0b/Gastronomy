import { describe, expect, it } from 'vitest'
import { statusTintCssOf, statusTintOf, statusTokenOf } from '../../../src/shared/core/statusTints'
import { appTheme } from '../../../src/shared/theme'

describe('the status colour of a share of items', () => {
  it.each([
    ['none', 'error'],
    ['some', 'warning'],
    ['all', 'success'],
  ] as const)('is the %s share in %s', (state, token) => {
    expect(statusTokenOf(state)).toBe(token)
  })
})

describe('the tint of a status colour', () => {
  it('mixes thirty percent of the status colour into the surface for a drawing', () => {
    expect(statusTintOf(appTheme.themes.light, 'error')).toBe('#efcaca')
  })

  it('rounds a channel that lands half way up, so the tint matches the one the browser mixes', () => {
    expect(statusTintOf(appTheme.themes.light, 'warning')).toBe('#dbceb3')
  })

  it('mixes the same thirty percent into the surface for a screen element', () => {
    expect(statusTintCssOf('success')).toBe(
      'color-mix(in srgb, rgb(var(--v-theme-success)) 30%, rgb(var(--v-theme-surface)))',
    )
  })
})
