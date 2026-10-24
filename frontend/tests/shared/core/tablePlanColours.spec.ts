import { describe, expect, it } from 'vitest'
import {
  DARK_TABLE_PLAN_COLOURS,
  LIGHT_TABLE_PLAN_COLOURS,
} from '../../../src/shared/core/tablePlanColours'

describe('the colours a table plan takes from the app theme', () => {
  it('are the neutral tokens of the light theme in light mode', () => {
    expect(LIGHT_TABLE_PLAN_COLOURS).toEqual({
      drawing: '#ffffff',
      ink: '#1f2328',
      muted: 'rgba(31, 35, 40, 0.71)',
      table: '#ebedf0',
      station: '#1f2937',
      stationInk: '#ffffff',
    })
  })

  it('are the neutral tokens of the dark theme in dark mode', () => {
    expect(DARK_TABLE_PLAN_COLOURS).toEqual({
      drawing: '#252a32',
      ink: '#e6e8eb',
      muted: 'rgba(230, 232, 235, 0.66)',
      table: '#2e343d',
      station: '#e6e8eb',
      stationInk: '#1b1e24',
    })
  })
})
