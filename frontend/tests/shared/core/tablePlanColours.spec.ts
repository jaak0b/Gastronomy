import { describe, expect, it } from 'vitest'
import {
  DARK_TABLE_PLAN_COLOURS,
  LIGHT_TABLE_PLAN_COLOURS,
} from '../../../src/shared/core/tablePlanColours'

describe('the colours a table plan takes from the app theme', () => {
  it('are the neutral and status tokens of the light theme in light mode', () => {
    expect(LIGHT_TABLE_PLAN_COLOURS).toEqual({
      drawing: '#ffffff',
      ink: '#1f2328',
      table: '#ebedf0',
      station: '#1f2937',
      stationInk: '#ffffff',
      handOutFill: { none: '#efcaca', some: '#dbceb3', all: '#c0dccd' },
      settlementBorder: { none: '#c94f4f', some: '#875a00', all: '#2e8b57' },
    })
  })

  it('are the neutral and status tokens of the dark theme in dark mode', () => {
    expect(DARK_TABLE_PLAN_COLOURS).toEqual({
      drawing: '#252a32',
      ink: '#e6e8eb',
      table: '#2e343d',
      station: '#e6e8eb',
      stationInk: '#1b1e24',
      handOutFill: { none: '#5d3e43', some: '#665d3b', all: '#315249' },
      settlementBorder: { none: '#e06c6c', some: '#ffd54f', all: '#4caf7d' },
    })
  })
})
