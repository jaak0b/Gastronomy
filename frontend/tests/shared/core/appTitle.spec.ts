import { describe, expect, it } from 'vitest'
import { screenTitle } from '../../src/core/appTitle'
import type { ScreenName } from '../../src/core/landing'

const words = (key: string): string => key

describe('the browser title of a screen', () => {
  it('names the station while the tablet works at it', () => {
    expect(screenTitle('station', 'Küche', words)).toBe('Küche')
  })

  it('falls back to the app name while the station name is unknown', () => {
    expect(screenTitle('station', null, words)).toBe('GastronomyApp')
    expect(screenTitle('station', '', words)).toBe('GastronomyApp')
  })

  it('names the ordering screens through the waiter key', () => {
    const waiterScreens: ScreenName[] = ['catalog', 'review', 'openItems']

    expect(waiterScreens.map((screen) => screenTitle(screen, null, words))).toEqual([
      'app.title.waiter',
      'app.title.waiter',
      'app.title.waiter',
    ])
  })

  it('names the admin screen through the admin key', () => {
    expect(screenTitle('admin', null, words)).toBe('app.title.admin')
  })

  it('keeps the app name on every screen that has no name of its own', () => {
    expect(screenTitle('enrolQr', null, words)).toBe('GastronomyApp')
    expect(screenTitle('welcome', null, words)).toBe('GastronomyApp')
    expect(screenTitle('startingUp', null, words)).toBe('GastronomyApp')
  })
})
